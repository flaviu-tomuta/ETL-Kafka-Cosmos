using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Shared.Models.Audit;

public sealed class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    public Task FlushAsync(AuditRecord record)
    {
        _logger.LogInformation(
            "MessageProcessed {MessageId} {EntityId} {TopicRole} {StepsApplied} {WasApiFallback} {TotalDurationMs}ms",
            record.MessageId,
            record.PartyId,
            record.TopicRole,
            string.Join(",", record.Hydration.StepsApplied),
            record.Versions.WasApiFallback,
            record.Hydration.TotalDurationMs);

        return Task.CompletedTask;
    }
}
