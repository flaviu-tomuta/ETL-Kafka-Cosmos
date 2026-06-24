using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.Models;

namespace Shared.Models.Audit;

public sealed class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly Container _auditContainer;

    public AuditService(
        ILogger<AuditService> logger,
        TelemetryClient telemetryClient,
        AuditMetricsContainer auditContainer)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _auditContainer = auditContainer.Value;
    }

    public async Task FlushAsync(AuditRecord record)
    {
        _logger.LogInformation(
            "MessageProcessed {MessageId} {EntityId} {TopicRole} {StepsApplied} {WasApiFallback} {TotalDurationMs}ms",
            record.MessageId,
            record.PartyId,
            record.TopicRole,
            string.Join(",", record.Hydration.StepsApplied),
            record.Versions.WasApiFallback,
            record.Hydration.TotalDurationMs);

        try
        {
            await _auditContainer.CreateItemAsync(
                record.ToCosmosDocument(),
                new PartitionKey(record.ProcessedDate));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(
                "AuditRecordAlreadyExists {MessageId} — duplicate flush suppressed",
                record.MessageId);
        }
    }
}
