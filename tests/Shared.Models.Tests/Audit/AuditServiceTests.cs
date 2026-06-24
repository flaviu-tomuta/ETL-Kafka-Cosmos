using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Models.Audit;
using Shared.Models.Contracts;
using Shared.Models.DependencyInjection;
using Shared.Models.Models;

namespace Shared.Models.Tests.Audit;

public sealed class AuditServiceTests
{
    // ── AC1: FlushAsync emits MessageProcessed Information log ─────────────────

    [Fact]
    public async Task FlushAsync_EmitsMessageProcessed_AtInformationLevel()
    {
        FakeLogger logger = new();
        AuditService sut = new(logger);
        AuditRecord record = BuildRecord("msg-1", "party-1", "onboarding");

        await sut.FlushAsync(record);

        Assert.Single(logger.InfoMessages);
        Assert.Contains("MessageProcessed", logger.InfoMessages[0]);
    }

    [Fact]
    public async Task FlushAsync_LogContainsMessageId_PartyId_TopicRole()
    {
        FakeLogger logger = new();
        AuditService sut = new(logger);
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
        FakeLogger logger = new();
        AuditService sut = new(logger);
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
            MessageId     = messageId,
            PartyId       = partyId,
            TopicRole     = topicRole,
            ProcessedDate = "2026-06-24"
        };

    private sealed class FakeLogger : ILogger<AuditService>
    {
        public List<string> InfoMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
                InfoMessages.Add(formatter(state, exception));
        }
    }
}
