using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public sealed class OutputAssembler : IOutputAssembler
{
    public EnrichedCustomer Assemble(HydrationContext context, IEnumerable<EnrichmentResult> results)
    {
        List<EnrichmentResult> applied = results.Where(r => r.Applied).ToList();

        return new EnrichedCustomer
        {
            Id            = context.EntityId,
            PartyId       = context.EntityId,
            Version       = context.IncomingVersion,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = context.TopicRole == "onboarding" ? "onboarding-app" : "amendment-app",
            AuditSummary  = new AuditSummary
            {
                LastMessageId      = context.MessageId,
                LastKafkaOffset    = 0,  // HydrationContext does not carry Kafka offset; see coding-log
                LastHydrationSteps = applied.Select(r => r.StepName).ToList(),
                WasApiFallback     = context.WasApiFallback,
                ProcessedAt        = DateTimeOffset.UtcNow
            }
        };
    }
}
