using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public interface IOnboardingCosmosWriter
{
    Task WriteAsync(EnrichedCustomer entity);
}
