using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline.Steps;

public sealed class ComplianceEnrichmentStep : IEnrichmentStep
{
    public string StepName => "ComplianceCheck";

    public bool AppliesTo(HydrationContext context) => true;

    public Task<EnrichmentResult> EnrichAsync(HydrationContext context) =>
        Task.FromResult(new EnrichmentResult { StepName = StepName, Applied = true });
}
