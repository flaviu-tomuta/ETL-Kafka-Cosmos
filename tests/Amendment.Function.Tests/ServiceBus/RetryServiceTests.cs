using Amendment.Function.ServiceBus;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models.Models;

namespace Amendment.Function.Tests.ServiceBus;

public sealed class RetryServiceTests
{
    private static KafkaMessageContext MakeContext(string topicRole = "amendment") => new()
    {
        MessageId  = "msg-001",
        PartyId    = "party-1",
        Topic      = "amendment-topic",
        Partition  = 2,
        Offset     = 99L,
        TopicRole  = topicRole,
        ReceivedAt = DateTimeOffset.UtcNow
    };

    private static (
        RetryService Sut,
        Mock<ServiceBusSender> RetrySender,
        Mock<ServiceBusSender> DlqSender,
        FakeLogger Logger
    ) Build(string topicRole = "amendment")
    {
        Mock<ServiceBusSender> retrySender = new();
        retrySender.Setup(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<ServiceBusSender> dlqSender = new();
        dlqSender.Setup(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<ServiceBusClient> client = new();
        client.Setup(c => c.CreateSender($"{topicRole}-retry")).Returns(retrySender.Object);
        client.Setup(c => c.CreateSender($"{topicRole}-deadletter")).Returns(dlqSender.Object);

        FakeLogger logger = new();
        RetryService sut = new(client.Object, logger);

        return (sut, retrySender, dlqSender, logger);
    }

    // AC1: attemptCount < max → sends to {topicRole}-retry queue
    [Fact]
    public async Task EnqueueAsync_AttemptBelowMax_SendsToRetryQueue()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, Mock<ServiceBusSender> dlqSender, _) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.EnqueueAsync(ctx, "payload", attemptCount: 1);

        retrySender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        dlqSender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC1: ScheduledEnqueueTime is ~30 seconds ahead when not immediate
    [Fact]
    public async Task EnqueueAsync_AttemptBelowMax_Sets30SecondDelay()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, _, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        retrySender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        await sut.EnqueueAsync(ctx, "payload", attemptCount: 1);

        Assert.NotNull(captured);
        Assert.True(captured.ScheduledEnqueueTime >= before.AddSeconds(25));
        Assert.True(captured.ScheduledEnqueueTime <= before.AddSeconds(35));
    }

    // AC5: ETag conflict — isImmediate=true → ScheduledEnqueueTime ≈ UtcNow (no delay)
    [Fact]
    public async Task EnqueueAsync_IsImmediate_SetsScheduledEnqueueTimeToNow()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, _, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        retrySender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        await sut.EnqueueAsync(ctx, "payload", attemptCount: 1, isImmediate: true);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.NotNull(captured);
        Assert.True(captured.ScheduledEnqueueTime >= before.AddSeconds(-1));
        Assert.True(captured.ScheduledEnqueueTime <= after.AddSeconds(5));
    }

    // AC1: attemptCount in ApplicationProperties is incremented by 1
    [Fact]
    public async Task EnqueueAsync_AttemptBelowMax_IncrementsAttemptCountInProperties()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, _, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        retrySender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await sut.EnqueueAsync(ctx, "payload", attemptCount: 1);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.ApplicationProperties["attemptCount"]);
    }

    // AC3: all required ApplicationProperties present
    [Fact]
    public async Task EnqueueAsync_SetsAllRequiredApplicationProperties()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, _, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        retrySender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await sut.EnqueueAsync(ctx, "payload", attemptCount: 1);

        Assert.NotNull(captured);
        Assert.Equal(ctx.MessageId,  captured.ApplicationProperties["messageId"]);
        Assert.Equal(ctx.PartyId,    captured.ApplicationProperties["partyId"]);
        Assert.Equal(ctx.Topic,      captured.ApplicationProperties["topic"]);
        Assert.Equal(ctx.Partition,  captured.ApplicationProperties["partition"]);
        Assert.Equal(ctx.Offset,     captured.ApplicationProperties["offset"]);
        Assert.Equal(ctx.TopicRole,  captured.ApplicationProperties["topicRole"]);
        Assert.True(captured.ApplicationProperties.ContainsKey("attemptCount"));
    }

    // Max retries exhausted: attemptCount = 3 → routes to dead-letter queue
    [Fact]
    public async Task EnqueueAsync_AttemptExceedsMax_SendsToDeadLetterQueue()
    {
        (RetryService sut, Mock<ServiceBusSender> retrySender, Mock<ServiceBusSender> dlqSender, _) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.EnqueueAsync(ctx, "payload", attemptCount: 3);

        dlqSender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        retrySender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Max retries exhausted: MessageDeadLettered Error log emitted
    [Fact]
    public async Task EnqueueAsync_AttemptExceedsMax_LogsMessageDeadLettered()
    {
        (RetryService sut, _, _, FakeLogger logger) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.EnqueueAsync(ctx, "payload", attemptCount: 3);

        Assert.Contains(logger.Errors, m => m.Contains("MessageDeadLettered"));
    }

    // ── Fake logger ───────────────────────────────────────────────────────────

    private sealed class FakeLogger : ILogger<RetryService>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                Errors.Add(formatter(state, exception));
        }
    }
}
