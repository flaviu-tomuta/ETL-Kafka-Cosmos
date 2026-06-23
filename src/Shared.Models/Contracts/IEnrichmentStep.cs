using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IEnrichmentStep
{
    bool AppliesTo(HydrationContext context);
    Task<EnrichmentResult> EnrichAsync(HydrationContext context);
}
