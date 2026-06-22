namespace Shared.Models.Models;

public sealed record KafkaMessageContext
{
    public string         MessageId  { get; init; } = string.Empty;
    public string         PartyId    { get; init; } = string.Empty;
    public string         Topic      { get; init; } = string.Empty;
    public int            Partition  { get; init; }
    public long           Offset     { get; init; }
    public string         TopicRole  { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; }
}

public sealed record HydrationContext
{
    public string     EntityId        { get; init; } = string.Empty;
    public string     MessageId       { get; init; } = string.Empty;
    public string     TopicRole       { get; init; } = string.Empty;
    public int        IncomingVersion { get; init; }
    public int        StoredVersion   { get; init; }
    public EntityData Data            { get; init; } = new();
    public bool       WasApiFallback  { get; init; }
}

public sealed record EntityData
{
    public Dictionary<string, object> Properties { get; init; } = new();
}

public sealed record EnrichmentResult
{
    public string      StepName { get; init; } = string.Empty;
    public bool        Applied  { get; init; }
    public TimeSpan    Duration { get; init; } = TimeSpan.Zero;
    public EntityData? Data     { get; init; }
}
