using System.Text.Json;
using Shared.Models.Contracts;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Amendment.Function.Pipeline;

internal sealed class AmendmentPipeline : IAmendmentPipeline
{
    private readonly IAmendmentOrchestrator _orchestrator;

    public AmendmentPipeline(IAmendmentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ProcessAsync(KafkaMessageContext context, string rawPayload)
    {
        AmendmentMessage message = JsonSerializer.Deserialize<AmendmentMessage>(rawPayload)
            ?? throw new MessageValidationException(
                "Failed to deserialize amendment message",
                context.PartyId,
                context.MessageId);

        await _orchestrator.OrchestrateAsync(message, context);
    }
}
