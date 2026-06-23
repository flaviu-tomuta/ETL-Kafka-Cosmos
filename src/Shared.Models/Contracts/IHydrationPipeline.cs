using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IHydrationPipeline
{
    Task<IEnumerable<EnrichmentResult>> ExecuteAsync(HydrationContext context);
}
