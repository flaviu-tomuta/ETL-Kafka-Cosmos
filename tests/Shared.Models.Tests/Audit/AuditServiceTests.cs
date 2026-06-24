using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models.Audit;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.DependencyInjection;
using Shared.Models.Models;

namespace Shared.Models.Tests.Audit;

public sealed class AuditServiceTests
{
    private static (
        AuditService Sut,
        Mock<Container> MockContainer,
        FakeLogger Logger
    ) Build()
    {
        Mock<Container> mockContainer = new();
        FakeLogger logger = new();
        TelemetryConfiguration config = new() { TelemetryChannel = new NoopChannel() };
        TelemetryClient telemetryClient = new(config);
        AuditMetricsContainer auditContainer = new(mockContainer.Object);
        AuditService sut = new(logger, telemetryClient, auditContainer);
        return (sut, mockContainer, logger);
    }

    private static void SetupCreateItemSuccess(Mock<Container> mockContainer) =>
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<AuditCosmosDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<AuditCosmosDocument>>());

    // ── AC1 (STORY-20): FlushAsync emits MessageProcessed Information log ──────

    [Fact]
    public async Task FlushAsync_EmitsMessageProcessed_AtInformationLevel()
    {
        (AuditService sut, Mock<Container> mockContainer, FakeLogger logger) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = BuildRecord("msg-1", "party-1", "onboarding");

        await sut.FlushAsync(record);

        Assert.Single(logger.InfoMessages);
        Assert.Contains("MessageProcessed", logger.InfoMessages[0]);
    }

    [Fact]
    public async Task FlushAsync_LogContainsMessageId_PartyId_TopicRole()
    {
        (AuditService sut, Mock<Container> mockContainer, FakeLogger logger) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = BuildRecord("msg-abc", "party-xyz", "amendment");

        await sut.FlushAsync(record);

        string msg = logger.InfoMessages[0];
        Assert.Contains("msg-abc",   msg);
        Assert.Contains("party-xyz", msg);
        Assert.Contains("amendment", msg);
    }

    [Fact]
    public async Task FlushAsync_LogContainsStepsApplied_WasApiFallback_TotalDurationMs()
    {
        (AuditService sut, Mock<Container> mockContainer, FakeLogger logger) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = new()
        {
            MessageId     = "msg-1",
            PartyId       = "party-1",
            TopicRole     = "onboarding",
            ProcessedDate = "2026-06-24",
            Hydration = new HydrationInfo
            {
                StepsApplied    = ["AddressEnrichment", "ComplianceCheck"],
                TotalDurationMs = 142
            },
            Versions = new VersionInfo { WasApiFallback = true }
        };

        await sut.FlushAsync(record);

        string msg = logger.InfoMessages[0];
        Assert.Contains("AddressEnrichment", msg);
        Assert.Contains("142",               msg);
    }

    // ── AC1 (STORY-21): FlushAsync calls CreateItemAsync with mapped document ──

    [Fact]
    public async Task FlushAsync_CallsCreateItemAsync_WithMappedDocument()
    {
        (AuditService sut, Mock<Container> mockContainer, _) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = BuildRecord("msg-42", "party-99", "onboarding");

        await sut.FlushAsync(record);

        mockContainer.Verify(c => c.CreateItemAsync(
            It.Is<AuditCosmosDocument>(d =>
                d.MessageId == "msg-42" &&
                d.PartyId   == "party-99" &&
                d.Id        == "msg-42"),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC2: id = messageId, partition key = processedDate, ttl = 15552000 ────

    [Fact]
    public async Task FlushAsync_CallsCreateItemAsync_WithProcessedDate_AsPartitionKey()
    {
        (AuditService sut, Mock<Container> mockContainer, _) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = BuildRecord("msg-1", "party-1", "onboarding") with
        {
            ProcessedDate = "2026-06-24"
        };

        await sut.FlushAsync(record);

        mockContainer.Verify(c => c.CreateItemAsync(
            It.IsAny<AuditCosmosDocument>(),
            new PartitionKey("2026-06-24"),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_DocumentHasTtl_15552000()
    {
        (AuditService sut, Mock<Container> mockContainer, _) = Build();
        SetupCreateItemSuccess(mockContainer);
        AuditRecord record = BuildRecord("msg-1", "party-1", "onboarding");

        await sut.FlushAsync(record);

        mockContainer.Verify(c => c.CreateItemAsync(
            It.Is<AuditCosmosDocument>(d => d.Ttl == 15552000),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC3: ToCosmosDocument maps all hydration fields ───────────────────────

    [Fact]
    public void ToCosmosDocument_IncludesAllHydrationFields()
    {
        AuditRecord record = new()
        {
            MessageId     = "msg-1",
            PartyId       = "party-1",
            TopicRole     = "onboarding",
            ProcessedDate = "2026-06-24",
            Hydration = new HydrationInfo
            {
                StepsApplied    = ["AddressEnrichment", "ComplianceCheck"],
                StepsSkipped    = ["CreditCheck"],
                TotalDurationMs = 142,
                StepBreakdown   =
                [
                    new HydrationStepBreakdown { Step = "AddressEnrichment", DurationMs = 80,  Applied = true  },
                    new HydrationStepBreakdown { Step = "ComplianceCheck",   DurationMs = 62,  Applied = true  },
                    new HydrationStepBreakdown { Step = "CreditCheck",       DurationMs = 0,   Applied = false }
                ]
            }
        };

        AuditCosmosDocument doc = record.ToCosmosDocument();

        Assert.Equal(record.Hydration.StepsApplied,    doc.Hydration.StepsApplied);
        Assert.Equal(record.Hydration.StepsSkipped,    doc.Hydration.StepsSkipped);
        Assert.Equal(record.Hydration.TotalDurationMs, doc.Hydration.TotalDurationMs);
        Assert.Equal(3,                                 doc.Hydration.StepBreakdown.Count);
        Assert.Equal("AddressEnrichment",               doc.Hydration.StepBreakdown[0].Step);
        Assert.True(doc.Hydration.StepBreakdown[0].Applied);
        Assert.False(doc.Hydration.StepBreakdown[2].Applied);
    }

    // ── AC4: 409 Conflict → Warning log, no rethrow ───────────────────────────

    [Fact]
    public async Task FlushAsync_When409Conflict_DoesNotRethrow()
    {
        (AuditService sut, Mock<Container> mockContainer, _) = Build();
        CosmosException conflict = new("Conflict", HttpStatusCode.Conflict, 0, "act-1", 1.0);
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<AuditCosmosDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(conflict);

        Exception? thrown = await Record.ExceptionAsync(
            () => sut.FlushAsync(BuildRecord("msg-1", "party-1", "onboarding")));

        Assert.Null(thrown);
    }

    [Fact]
    public async Task FlushAsync_When409Conflict_LogsWarning()
    {
        (AuditService sut, Mock<Container> mockContainer, FakeLogger logger) = Build();
        CosmosException conflict = new("Conflict", HttpStatusCode.Conflict, 0, "act-1", 1.0);
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<AuditCosmosDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(conflict);

        await sut.FlushAsync(BuildRecord("msg-99", "party-1", "onboarding"));

        Assert.Single(logger.WarningMessages);
        Assert.Contains("msg-99", logger.WarningMessages[0]);
    }

    // ── DI registration ────────────────────────────────────────────────────────

    [Fact]
    public void AddSharedServices_RegistersIAuditService_AsScoped()
    {
        ServiceCollection services = new();
        services.AddSharedServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAuditService));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped,    descriptor!.Lifetime);
        Assert.Equal(typeof(AuditService), descriptor.ImplementationType);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuditRecord BuildRecord(string messageId, string partyId, string topicRole) =>
        new()
        {
            Id            = messageId,
            MessageId     = messageId,
            PartyId       = partyId,
            TopicRole     = topicRole,
            ProcessedDate = "2026-06-24"
        };

    private sealed class NoopChannel : ITelemetryChannel
    {
        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; } = string.Empty;
        public void Send(ITelemetry item) { }
        public void Flush() { }
        public void Dispose() { }
    }

    private sealed class FakeLogger : ILogger<AuditService>
    {
        public List<string> InfoMessages    { get; } = [];
        public List<string> WarningMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            if (logLevel == LogLevel.Information)
                InfoMessages.Add(message);
            else if (logLevel == LogLevel.Warning)
                WarningMessages.Add(message);
        }
    }
}
