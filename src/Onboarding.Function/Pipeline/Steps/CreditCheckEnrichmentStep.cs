using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline.Steps;

public sealed class CreditCheckEnrichmentStep : IEnrichmentStep
{
    public string StepName => "CreditCheck";

    public bool AppliesTo(HydrationContext context) => true;

    public Task<EnrichmentResult> EnrichAsync(HydrationContext context) =>
        Task.FromResult(new EnrichmentResult { StepName = StepName, Applied = true });
}
