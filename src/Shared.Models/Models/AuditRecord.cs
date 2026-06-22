namespace Shared.Models.Models;

public sealed record KafkaContextInfo
{
    public string         Topic      { get; init; } = string.Empty;
    public int            Partition  { get; init; }
    public long           Offset     { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}

public sealed record VersionInfo
{
    public int  Stored         { get; init; }
    public int  Incoming       { get; init; }
    public int  Gap            { get; init; }
    public bool WasApiFallback { get; init; }
}

public sealed record HydrationStepBreakdown
{
    public string Step       { get; init; } = string.Empty;
    public double DurationMs { get; init; }
    public bool   Applied    { get; init; }
}

public sealed record HydrationInfo
{
    public List<string>                 StepsApplied    { get; init; } = [];
    public List<string>                 StepsSkipped    { get; init; } = [];
    public double                       TotalDurationMs { get; init; }
    public List<HydrationStepBreakdown> StepBreakdown   { get; init; } = [];
}

public sealed record RetryInfo
{
    public int  AttemptNumber { get; init; }
    public bool WasRetry      { get; init; }
    public bool DeadLettered  { get; init; }
}

public sealed record AuditCosmosDocument
{
    public string           Id            { get; init; } = string.Empty;
    public string           MessageId     { get; init; } = string.Empty;
    public string           PartyId       { get; init; } = string.Empty;
    public string           TopicRole     { get; init; } = string.Empty;
    public string           ProcessedDate { get; init; } = string.Empty;
    public DateTimeOffset   ProcessedAt   { get; init; }
    public KafkaContextInfo KafkaContext  { get; init; } = new();
    public VersionInfo      Versions      { get; init; } = new();
    public string           Outcome       { get; init; } = string.Empty;
    public string?          FailureReason { get; init; }
    public HydrationInfo    Hydration     { get; init; } = new();
    public RetryInfo        RetryInfo     { get; init; } = new();
    public int              Ttl           { get; init; } = 15552000;
}

public sealed record AuditRecord
{
    public string           Id            { get; init; } = string.Empty;
    public string           MessageId     { get; init; } = string.Empty;
    public string           PartyId       { get; init; } = string.Empty;
    public string           TopicRole     { get; init; } = string.Empty;
    public string           ProcessedDate { get; init; } = string.Empty;
    public DateTimeOffset   ProcessedAt   { get; init; }
    public KafkaContextInfo KafkaContext  { get; init; } = new();
    public VersionInfo      Versions      { get; init; } = new();
    public string           Outcome       { get; init; } = string.Empty;
    public string?          FailureReason { get; init; }
    public HydrationInfo    Hydration     { get; init; } = new();
    public RetryInfo        RetryInfo     { get; init; } = new();
    public int              Ttl           { get; init; } = 15552000;

    public AuditCosmosDocument ToCosmosDocument() => new()
    {
        Id            = Id,
        MessageId     = MessageId,
        PartyId       = PartyId,
        TopicRole     = TopicRole,
        ProcessedDate = ProcessedDate,
        ProcessedAt   = ProcessedAt,
        KafkaContext  = KafkaContext,
        Versions      = Versions,
        Outcome       = Outcome,
        FailureReason = FailureReason,
        Hydration     = Hydration,
        RetryInfo     = RetryInfo,
        Ttl           = Ttl
    };
}
