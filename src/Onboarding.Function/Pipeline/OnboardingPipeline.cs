using System.Text.Json;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public sealed class OnboardingPipeline : IOnboardingPipeline
{
    private readonly IHydrationPipeline    _hydrationPipeline;
    private readonly IOutputAssembler      _outputAssembler;
    private readonly IOnboardingCosmosWriter _cosmosWriter;
    private readonly IAuditService         _auditService;

    public OnboardingPipeline(
        IHydrationPipeline      hydrationPipeline,
        IOutputAssembler        outputAssembler,
        IOnboardingCosmosWriter cosmosWriter,
        IAuditService           auditService)
    {
        _hydrationPipeline = hydrationPipeline;
        _outputAssembler   = outputAssembler;
        _cosmosWriter      = cosmosWriter;
        _auditService      = auditService;
    }

    public async Task ProcessAsync(KafkaMessageContext context, string rawPayload)
    {
        int incomingVersion = ParseVersion(rawPayload);

        HydrationContext hydrationContext = new()
        {
            EntityId        = context.PartyId,
            MessageId       = context.MessageId,
            TopicRole       = context.TopicRole,
            IncomingVersion = incomingVersion,
            StoredVersion   = 0,
            WasApiFallback  = false
        };

        List<EnrichmentResult> resultList =
            (await _hydrationPipeline.ExecuteAsync(hydrationContext)).ToList();

        EnrichedCustomer entity = _outputAssembler.Assemble(hydrationContext, resultList);

        await _cosmosWriter.WriteAsync(entity);

        List<EnrichmentResult> applied = resultList.Where(r => r.Applied).ToList();
        List<EnrichmentResult> skipped = resultList.Where(r => !r.Applied).ToList();

        AuditRecord auditRecord = new()
        {
            Id            = context.MessageId,
            MessageId     = context.MessageId,
            PartyId       = context.PartyId,
            TopicRole     = context.TopicRole,
            ProcessedDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            ProcessedAt   = DateTimeOffset.UtcNow,
            KafkaContext  = new KafkaContextInfo
            {
                Topic      = context.Topic,
                Partition  = context.Partition,
                Offset     = context.Offset,
                ReceivedAt = context.ReceivedAt
            },
            Versions = new VersionInfo
            {
                Stored         = hydrationContext.StoredVersion,
                Incoming       = hydrationContext.IncomingVersion,
                Gap            = hydrationContext.IncomingVersion - hydrationContext.StoredVersion,
                WasApiFallback = hydrationContext.WasApiFallback
            },
            Outcome   = "success",
            Hydration = new HydrationInfo
            {
                StepsApplied    = applied.Select(r => r.StepName).ToList(),
                StepsSkipped    = skipped.Select(r => r.StepName).ToList(),
                TotalDurationMs = applied.Sum(r => r.Duration.TotalMilliseconds),
                StepBreakdown   = resultList.Select(r => new HydrationStepBreakdown
                {
                    Step       = r.StepName,
                    DurationMs = r.Duration.TotalMilliseconds,
                    Applied    = r.Applied
                }).ToList()
            },
            RetryInfo = new RetryInfo
            {
                AttemptNumber = 1,
                WasRetry      = false,
                DeadLettered  = false
            }
        };

        await _auditService.FlushAsync(auditRecord);
    }

    private static int ParseVersion(string rawPayload)
    {
        using JsonDocument doc = JsonDocument.Parse(rawPayload);
        if (doc.RootElement.TryGetProperty("version", out JsonElement versionElement)
            && versionElement.TryGetInt32(out int version))
        {
            return version;
        }

        return 0;
    }
}
