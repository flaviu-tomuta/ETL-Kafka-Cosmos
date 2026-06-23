using System.Reflection;
using Amendment.Function.Functions;
using Amendment.Function.Pipeline;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.ErrorClassification;
using Shared.Models.Models;

namespace Amendment.Function.Tests.Functions;

public sealed class AmendmentKafkaFunctionTests
{
    private static KafkaRawEvent ValidEvent(string partyId = "party-1", string messageId = "msg-1") =>
        new(
            RawPayload: $"{{\"partyId\":\"{partyId}\"}}",
            Headers: new Dictionary<string, string> { ["messageId"] = messageId },
            Topic: "amendment",
            Partition: 0,
            Offset: 1L
        );

    // AC1: Kafka trigger configured with ConsumerGroup = "amendment-func"
    [Fact]
    public void Run_Method_HasKafkaTriggerWithAmendmentConsumerGroup()
    {
        MethodInfo? runMethod = typeof(AmendmentKafkaFunction).GetMethod("Run");
        Assert.NotNull(runMethod);

        ParameterInfo? messagesParam = runMethod.GetParameters()
            .FirstOrDefault(p => p.Name == "messages");
        Assert.NotNull(messagesParam);

        // Use dynamic reflection to avoid compile-time dependency on the Kafka extension assembly
        Attribute? kafkaAttr = messagesParam.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "KafkaTriggerAttribute");
        Assert.NotNull(kafkaAttr);

        PropertyInfo? consumerGroupProp = kafkaAttr.GetType().GetProperty("ConsumerGroup");
        Assert.NotNull(consumerGroupProp);
        Assert.Equal("amendment-func", consumerGroupProp.GetValue(kafkaAttr));
    }

    // AC2: IsDuplicateAsync called first; if true, pipeline is NOT called
    [Fact]
    public async Task ProcessBatchAsync_DuplicateMessage_PipelineIsNotCalled()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: true);
        FakePipeline pipeline = new();
        pipeline.QueueSuccess();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Equal(0, pipeline.CallCount);
    }

    // AC2: if duplicate, MarkProcessedAsync is NOT called again
    [Fact]
    public async Task ProcessBatchAsync_DuplicateMessage_MarkProcessedAsyncNotCalled()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: true);

        AmendmentKafkaFunction sut = BuildSut(idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Equal(0, idempotency.MarkProcessedCallCount);
    }

    // AC2: duplicate message is logged as DuplicateMessageSkipped
    [Fact]
    public async Task ProcessBatchAsync_DuplicateMessage_LogsDuplicateMessageSkipped()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: true);
        FakeLogger logger = new();

        AmendmentKafkaFunction sut = BuildSut(idempotency: idempotency, logger: logger);

        await sut.ProcessBatchAsync([ValidEvent("party-1", "msg-dup")]);

        Assert.Contains(logger.Informational, m => m.Contains("DuplicateMessageSkipped"));
    }

    // AC3: TopicRole in KafkaMessageContext is "amendment"
    [Fact]
    public async Task ProcessBatchAsync_NonDuplicateMessage_TopicRoleIsAmendment()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        CapturingPipeline pipeline = new();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.NotNull(pipeline.CapturedContext);
        Assert.Equal("amendment", pipeline.CapturedContext.TopicRole);
    }

    // Entry sequence: MarkProcessedAsync called after successful pipeline run
    [Fact]
    public async Task ProcessBatchAsync_SuccessfulMessage_CallsMarkProcessedAsync()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueSuccess();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent("party-1", "msg-1")]);

        Assert.Equal(1, idempotency.MarkProcessedCallCount);
        Assert.Contains("msg-1", idempotency.MarkedMessageIds);
    }

    // Entry sequence: MarkProcessedAsync NOT called when pipeline fails
    [Fact]
    public async Task ProcessBatchAsync_PipelineFails_MarkProcessedAsyncNotCalled()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new TimeoutException("timeout"));

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Equal(0, idempotency.MarkProcessedCallCount);
    }

    // AC4: OutOfMemoryException rethrows without routing to DLQ or retry
    [Fact]
    public async Task ProcessBatchAsync_OutOfMemoryException_RethrowsWithoutRouting()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new OutOfMemoryException());
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency,
            deadLetter: dlq, retry: retry);

        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            sut.ProcessBatchAsync([ValidEvent()]));

        Assert.Empty(dlq.Calls);
        Assert.Empty(retry.Calls);
    }

    // Batch error routing: permanent exception → dead-letter
    [Fact]
    public async Task ProcessBatchAsync_PermanentException_SendsToDeadLetter()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad json"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency,
            deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Single(dlq.Calls);
        Assert.Empty(retry.Calls);
        Assert.Equal(1, dlq.Calls[0].Attempt);
    }

    // Batch error routing: transient exception → retry queue
    [Fact]
    public async Task ProcessBatchAsync_TransientException_EnqueuesToRetry()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new TimeoutException("timeout"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency,
            deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Empty(dlq.Calls);
        Assert.Single(retry.Calls);
        Assert.Equal(1, retry.Calls[0].AttemptCount);
    }

    // Batch error routing: unknown exception treated as transient
    [Fact]
    public async Task ProcessBatchAsync_UnknownException_TreatedAsTransientAndEnqueuesToRetry()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new InvalidOperationException("unexpected"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency,
            deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Empty(dlq.Calls);
        Assert.Single(retry.Calls);
    }

    // BatchCompleted log with accurate counts (duplicates count as succeeded)
    [Fact]
    public async Task ProcessBatchAsync_MixedBatch_LogsBatchCompletedWithCorrectCounts()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        idempotency.QueueDuplicate(isDuplicate: true);
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueSuccess();
        pipeline.QueueException(new System.Text.Json.JsonException("bad"));
        FakeLogger logger = new();

        // 3 events: non-dup success, dup skip (succeeded), non-dup permanent fail
        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency,
            logger: logger);

        await sut.ProcessBatchAsync([
            ValidEvent("p1", "m1"),
            ValidEvent("p2", "m2"),
            ValidEvent("p3", "m3")
        ]);

        string? batchLog = logger.Warnings.FirstOrDefault(m => m.Contains("BatchCompleted"));
        Assert.NotNull(batchLog);
        Assert.Contains("total=3", batchLog);
        Assert.Contains("succeeded=2", batchLog);
        Assert.Contains("permanentFailures=1", batchLog);
        Assert.Contains("transientFailures=0", batchLog);
    }

    // Failure on one message does not halt others in the batch
    [Fact]
    public async Task ProcessBatchAsync_OneMessageFails_RemainingMessagesAreProcessed()
    {
        FakeIdempotencyService idempotency = new();
        idempotency.QueueDuplicate(isDuplicate: false);
        idempotency.QueueDuplicate(isDuplicate: false);
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad"));
        pipeline.QueueSuccess();

        AmendmentKafkaFunction sut = BuildSut(pipeline: pipeline, idempotency: idempotency);

        await sut.ProcessBatchAsync([ValidEvent("p1", "m1"), ValidEvent("p2", "m2")]);

        Assert.Equal(2, pipeline.CallCount);
    }

    private static AmendmentKafkaFunction BuildSut(
        IAmendmentPipeline? pipeline = null,
        FakeIdempotencyService? idempotency = null,
        FakeDeadLetterService? deadLetter = null,
        FakeRetryService? retry = null,
        FakeLogger? logger = null) =>
        new(
            pipeline ?? new FakePipeline(),
            idempotency ?? new FakeIdempotencyService(),
            new ErrorClassifier(),
            deadLetter ?? new FakeDeadLetterService(),
            retry ?? new FakeRetryService(),
            logger ?? new FakeLogger()
        );

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakePipeline : IAmendmentPipeline
    {
        private readonly Queue<Exception?> _queue = new();
        public int CallCount { get; private set; }

        public void QueueSuccess() => _queue.Enqueue(null);
        public void QueueException(Exception ex) => _queue.Enqueue(ex);

        public Task ProcessAsync(KafkaMessageContext context, string rawPayload)
        {
            CallCount++;
            Exception? ex = _queue.Count > 0 ? _queue.Dequeue() : null;
            if (ex is not null) throw ex;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingPipeline : IAmendmentPipeline
    {
        public KafkaMessageContext? CapturedContext { get; private set; }

        public Task ProcessAsync(KafkaMessageContext context, string rawPayload)
        {
            CapturedContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdempotencyService : IIdempotencyService
    {
        private readonly Queue<bool> _duplicateQueue = new();
        public int MarkProcessedCallCount { get; private set; }
        public List<string> MarkedMessageIds { get; } = [];

        public void QueueDuplicate(bool isDuplicate) => _duplicateQueue.Enqueue(isDuplicate);

        public Task<bool> IsDuplicateAsync(string messageId)
        {
            bool result = _duplicateQueue.Count > 0 && _duplicateQueue.Dequeue();
            return Task.FromResult(result);
        }

        public Task MarkProcessedAsync(string messageId)
        {
            MarkProcessedCallCount++;
            MarkedMessageIds.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeadLetterService : IDeadLetterService
    {
        public List<(KafkaMessageContext Context, string Payload, string Reason, int Attempt)> Calls { get; } = [];

        public Task SendAsync(KafkaMessageContext context, string originalPayload, string reason, int attempt)
        {
            Calls.Add((context, originalPayload, reason, attempt));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRetryService : IRetryService
    {
        public List<(KafkaMessageContext Context, string Payload, int AttemptCount)> Calls { get; } = [];

        public Task EnqueueAsync(KafkaMessageContext context, string originalPayload, int attemptCount)
        {
            Calls.Add((context, originalPayload, attemptCount));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger<AmendmentKafkaFunction>
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Informational { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            if (logLevel == LogLevel.Warning) Warnings.Add(message);
            if (logLevel == LogLevel.Error) Errors.Add(message);
            if (logLevel == LogLevel.Information) Informational.Add(message);
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
