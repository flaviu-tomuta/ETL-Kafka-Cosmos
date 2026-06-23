namespace Shared.Models.Models;

public sealed record ValidationResult
{
    public bool    IsRejected { get; init; }
    public bool    IsNoOp     { get; init; }
    public string? Reason     { get; init; }
}
