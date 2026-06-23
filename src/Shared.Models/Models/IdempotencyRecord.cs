namespace Shared.Models.Models;

public sealed record IdempotencyRecord
{
    public string         Id          { get; init; } = string.Empty;
    public string         MessageId   { get; init; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; init; }
    public int            Ttl         { get; init; } = 604800;
}
