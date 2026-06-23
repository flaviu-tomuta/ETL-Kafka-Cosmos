using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IAmendmentOrchestrator
{
    Task<AmendmentResult> OrchestrateAsync(AmendmentMessage message, KafkaMessageContext context);
}
