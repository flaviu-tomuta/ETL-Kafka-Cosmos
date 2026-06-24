using Amendment.Function.ServiceBus;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models.Models;

namespace Amendment.Function.Tests.ServiceBus;

public sealed class DeadLetterServiceTests
{
    private static KafkaMessageContext MakeContext(string topicRole = "amendment") => new()
    {
        MessageId  = "msg-002",
        PartyId    = "party-2",
        Topic      = "amendment-topic",
        Partition  = 1,
        Offset     = 55L,
        TopicRole  = topicRole,
        ReceivedAt = DateTimeOffset.UtcNow
    };

    private static (
        DeadLetterService Sut,
        Mock<ServiceBusSender> DlqSender,
        FakeLogger Logger
    ) Build(string topicRole = "amendment")
    {
        Mock<ServiceBusSender> dlqSender = new();
        dlqSender.Setup(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<ServiceBusClient> client = new();
        client.Setup(c => c.CreateSender($"{topicRole}-deadletter")).Returns(dlqSender.Object);

        FakeLogger logger = new();
        DeadLetterService sut = new(client.Object, logger);

        return (sut, dlqSender, logger);
    }

    // AC2: SendAsync sends to {topicRole}-deadletter queue
    [Fact]
    public async Task SendAsync_SendsToDeadLetterQueue()
    {
        (DeadLetterService sut, Mock<ServiceBusSender> dlqSender, _) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.SendAsync(ctx, "payload", "SomeReason", attempt: 1);

        dlqSender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC2: MessageDeadLettered Error log is emitted
    [Fact]
    public async Task SendAsync_LogsMessageDeadLettered()
    {
        (DeadLetterService sut, _, FakeLogger logger) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.SendAsync(ctx, "payload", "SomeReason", attempt: 1);

        Assert.Contains(logger.Errors, m => m.Contains("MessageDeadLettered"));
    }

    // AC2: log contains MessageId, EntityId, Reason, and attempt
    [Fact]
    public async Task SendAsync_LogContainsRequiredFields()
    {
        (DeadLetterService sut, _, FakeLogger logger) = Build();
        KafkaMessageContext ctx = MakeContext();

        await sut.SendAsync(ctx, "payload", "RecordNotFound", attempt: 2);

        string? log = logger.Errors.FirstOrDefault(m => m.Contains("MessageDeadLettered"));
        Assert.NotNull(log);
        Assert.Contains(ctx.MessageId, log);
        Assert.Contains(ctx.PartyId,   log);
        Assert.Contains("RecordNotFound", log);
    }

    // AC3: all required ApplicationProperties present
    [Fact]
    public async Task SendAsync_SetsAllRequiredApplicationProperties()
    {
        (DeadLetterService sut, Mock<ServiceBusSender> dlqSender, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        dlqSender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await sut.SendAsync(ctx, "payload", "reason", attempt: 1);

        Assert.NotNull(captured);
        Assert.Equal(ctx.MessageId, captured.ApplicationProperties["messageId"]);
        Assert.Equal(ctx.PartyId,   captured.ApplicationProperties["partyId"]);
        Assert.Equal(ctx.Topic,     captured.ApplicationProperties["topic"]);
        Assert.Equal(ctx.Partition, captured.ApplicationProperties["partition"]);
        Assert.Equal(ctx.Offset,    captured.ApplicationProperties["offset"]);
        Assert.Equal(ctx.TopicRole, captured.ApplicationProperties["topicRole"]);
        Assert.True(captured.ApplicationProperties.ContainsKey("attemptCount"));
    }

    // AC4: attempt value passed through to properties (permanent failure with attempt=1)
    [Fact]
    public async Task SendAsync_StoresAttemptInProperties()
    {
        (DeadLetterService sut, Mock<ServiceBusSender> dlqSender, _) = Build();
        KafkaMessageContext ctx = MakeContext();
        ServiceBusMessage? captured = null;
        dlqSender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await sut.SendAsync(ctx, "payload", "reason", attempt: 1);

        Assert.NotNull(captured);
        Assert.Equal(1, captured.ApplicationProperties["attemptCount"]);
    }

    // Queue name derived from TopicRole (uses onboarding- prefix when topicRole = "onboarding")
    [Fact]
    public async Task SendAsync_QueueNameDerivedFromTopicRole()
    {
        (DeadLetterService sut, Mock<ServiceBusSender> dlqSender, _) = Build(topicRole: "onboarding");
        KafkaMessageContext ctx = MakeContext(topicRole: "onboarding");

        await sut.SendAsync(ctx, "payload", "reason", attempt: 1);

        dlqSender.Verify(s => s.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Fake logger ───────────────────────────────────────────────────────────

    private sealed class FakeLogger : ILogger<DeadLetterService>
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
