using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Onboarding.Function.Pipeline;

public sealed class HydrationPipeline : IHydrationPipeline
{
    private readonly IEnumerable<IEnrichmentStep> _steps;

    public HydrationPipeline(IEnumerable<IEnrichmentStep> steps)
    {
        _steps = steps;
    }

    public async Task<IEnumerable<EnrichmentResult>> ExecuteAsync(HydrationContext context)
    {
        IEnumerable<IEnrichmentStep> applicable = _steps.Where(s => s.AppliesTo(context));
        IEnumerable<IEnrichmentStep> skipped = _steps.Where(s => !s.AppliesTo(context));

        EnrichmentResult[] appliedResults = await Task.WhenAll(
            applicable.Select(s => s.EnrichAsync(context)));

        IEnumerable<EnrichmentResult> skippedResults = skipped.Select(s => new EnrichmentResult
        {
            StepName = s.StepName,
            Applied = false,
            Duration = TimeSpan.Zero
        });

        return appliedResults.Concat(skippedResults);
    }
}
