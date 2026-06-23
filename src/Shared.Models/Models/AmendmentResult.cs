namespace Shared.Models.Models;

public sealed record AmendmentResult
{
    public bool              IsNoOp { get; init; }
    public EnrichedCustomer? Entity { get; init; }
}
