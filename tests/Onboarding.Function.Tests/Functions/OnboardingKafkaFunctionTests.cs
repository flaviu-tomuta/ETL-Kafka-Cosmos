using Microsoft.Extensions.Logging;
using Onboarding.Function.Functions;
using Onboarding.Function.Pipeline;
using Shared.Models.Contracts;
using Shared.Models.ErrorClassification;
using Shared.Models.Models;

namespace Onboarding.Function.Tests.Functions;

public sealed class OnboardingKafkaFunctionTests
{
    private static KafkaRawEvent ValidEvent(string partyId = "party-1", string messageId = "msg-1") =>
        new(
            RawPayload: $"{{\"partyId\":\"{partyId}\"}}",
            Headers: new Dictionary<string, string> { ["messageId"] = messageId },
            Topic: "onboarding",
            Partition: 0,
            Offset: 1L
        );

    [Fact]
    public async Task ProcessBatchAsync_AllMessagesSucceed_LogsBatchCompletedWithCorrectCounts()
    {
        FakePipeline pipeline = new();
        pipeline.QueueSuccess();
        pipeline.QueueSuccess();
        FakeLogger logger = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, logger: logger);

        await sut.ProcessBatchAsync([ValidEvent("p1", "m1"), ValidEvent("p2", "m2")]);

        string? batchLog = logger.Warnings.FirstOrDefault(m => m.Contains("BatchCompleted"));
        Assert.NotNull(batchLog);
        Assert.Contains("total=2", batchLog);
        Assert.Contains("succeeded=2", batchLog);
        Assert.Contains("transientFailures=0", batchLog);
        Assert.Contains("permanentFailures=0", batchLog);
    }

    [Fact]
    public async Task ProcessBatchAsync_OneMessageThrowsPermanentException_OtherMessagesStillProcessed()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad json"));
        pipeline.QueueSuccess();
        FakeLogger logger = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, logger: logger);

        await sut.ProcessBatchAsync([ValidEvent("p1", "m1"), ValidEvent("p2", "m2")]);

        Assert.Equal(2, pipeline.CallCount);
        string? batchLog = logger.Warnings.FirstOrDefault(m => m.Contains("BatchCompleted"));
        Assert.NotNull(batchLog);
        Assert.Contains("succeeded=1", batchLog);
        Assert.Contains("permanentFailures=1", batchLog);
    }

    [Fact]
    public async Task ProcessBatchAsync_OneMessageThrowsTransientException_OtherMessagesStillProcessed()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new TimeoutException("timeout"));
        pipeline.QueueSuccess();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline);

        await sut.ProcessBatchAsync([ValidEvent("p1", "m1"), ValidEvent("p2", "m2")]);

        Assert.Equal(2, pipeline.CallCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_OutOfMemoryException_RethrowsWithoutRouting()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new OutOfMemoryException());
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, deadLetter: dlq, retry: retry);

        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            sut.ProcessBatchAsync([ValidEvent()]));

        Assert.Empty(dlq.Calls);
        Assert.Empty(retry.Calls);
    }

    [Fact]
    public async Task ProcessBatchAsync_PermanentException_SendsToDeadLetter()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Single(dlq.Calls);
        Assert.Empty(retry.Calls);
        Assert.Equal(1, dlq.Calls[0].Attempt);
    }

    [Fact]
    public async Task ProcessBatchAsync_TransientException_EnqueuesToRetry()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new TimeoutException("timeout"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Empty(dlq.Calls);
        Assert.Single(retry.Calls);
        Assert.Equal(1, retry.Calls[0].AttemptCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_UnknownException_TreatedAsTransientAndEnqueuesToRetry()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new InvalidOperationException("unexpected"));
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, deadLetter: dlq, retry: retry);

        await sut.ProcessBatchAsync([ValidEvent()]);

        Assert.Empty(dlq.Calls);
        Assert.Single(retry.Calls);
    }

    [Fact]
    public async Task ProcessBatchAsync_BatchCompleted_AlwaysWrittenEvenWhenAllFail()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad"));
        pipeline.QueueException(new TimeoutException());
        FakeLogger logger = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, logger: logger);

        await sut.ProcessBatchAsync([ValidEvent("p1", "m1"), ValidEvent("p2", "m2")]);

        string? batchLog = logger.Warnings.FirstOrDefault(m => m.Contains("BatchCompleted"));
        Assert.NotNull(batchLog);
        Assert.Contains("total=2", batchLog);
        Assert.Contains("succeeded=0", batchLog);
        Assert.Contains("permanentFailures=1", batchLog);
        Assert.Contains("transientFailures=1", batchLog);
    }

    // STORY-19 AC1: 5-message batch where message 3 throws JsonException — 4 succeed, 1 dead-lettered
    [Fact]
    public async Task ProcessBatchAsync_FiveMessageBatch_ThirdMessageThrowsJsonException_FourSucceedAndOneDeadLettered()
    {
        FakePipeline pipeline = new();
        pipeline.QueueSuccess();
        pipeline.QueueSuccess();
        pipeline.QueueException(new System.Text.Json.JsonException("bad json"));
        pipeline.QueueSuccess();
        pipeline.QueueSuccess();
        FakeDeadLetterService dlq = new();
        FakeRetryService retry = new();
        FakeLogger logger = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, deadLetter: dlq, retry: retry, logger: logger);

        await sut.ProcessBatchAsync([
            ValidEvent("p1", "m1"),
            ValidEvent("p2", "m2"),
            ValidEvent("p3", "m3"),
            ValidEvent("p4", "m4"),
            ValidEvent("p5", "m5")
        ]);

        Assert.Equal(5, pipeline.CallCount);
        Assert.Single(dlq.Calls);
        Assert.Empty(retry.Calls);
        string? batchLog = logger.Warnings.FirstOrDefault(m => m.Contains("BatchCompleted"));
        Assert.NotNull(batchLog);
        Assert.Contains("total=5", batchLog);
        Assert.Contains("succeeded=4", batchLog);
        Assert.Contains("permanentFailures=1", batchLog);
        Assert.Contains("transientFailures=0", batchLog);
    }

    // STORY-19 technical note: error log per failed message carries MessageId, EntityId, Category, ExceptionType
    [Fact]
    public async Task ProcessBatchAsync_FailedMessage_LogsMessageProcessingFailedWithStructuredProperties()
    {
        FakePipeline pipeline = new();
        pipeline.QueueException(new System.Text.Json.JsonException("bad json"));
        FakeLogger logger = new();

        OnboardingKafkaFunction sut = BuildSut(pipeline: pipeline, logger: logger);

        await sut.ProcessBatchAsync([ValidEvent("party-1", "msg-abc")]);

        Assert.Contains(logger.Errors, m =>
            m.Contains("MessageProcessingFailed") &&
            m.Contains("msg-abc") &&
            m.Contains("party-1"));
    }

    private static OnboardingKafkaFunction BuildSut(
        FakePipeline? pipeline = null,
        FakeDeadLetterService? deadLetter = null,
        FakeRetryService? retry = null,
        FakeLogger? logger = null) =>
        new(
            pipeline ?? new FakePipeline(),
            new ErrorClassifier(),
            deadLetter ?? new FakeDeadLetterService(),
            retry ?? new FakeRetryService(),
            logger ?? new FakeLogger()
        );

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakePipeline : IOnboardingPipeline
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

        public Task EnqueueAsync(KafkaMessageContext context, string originalPayload, int attemptCount, bool isImmediate = false)
        {
            Calls.Add((context, originalPayload, attemptCount));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger<OnboardingKafkaFunction>
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];

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
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
