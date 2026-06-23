using Shared.Models.Models;

namespace Amendment.Function.Pipeline;

// Stub replaced when STORY-17 implements AmendmentOrchestrator-backed pipeline
internal sealed class NullAmendmentPipeline : IAmendmentPipeline
{
    public Task ProcessAsync(KafkaMessageContext context, string rawPayload) => Task.CompletedTask;
}
