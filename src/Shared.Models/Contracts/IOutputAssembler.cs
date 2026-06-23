using Shared.Models.Models;

namespace Shared.Models.Contracts;

public interface IOutputAssembler
{
    EnrichedCustomer Assemble(HydrationContext context, IEnumerable<EnrichmentResult> results);
}
