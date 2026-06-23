using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public interface IOnboardingPipeline
{
    Task ProcessAsync(KafkaMessageContext context, string rawPayload);
}
