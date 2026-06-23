using Shared.Models.Models;

namespace Amendment.Function.Pipeline;

public interface IAmendmentPipeline
{
    Task ProcessAsync(KafkaMessageContext context, string rawPayload);
}
