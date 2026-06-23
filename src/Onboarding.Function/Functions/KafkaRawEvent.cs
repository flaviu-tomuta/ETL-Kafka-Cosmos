namespace Onboarding.Function.Functions;

internal sealed record KafkaRawEvent(
    string RawPayload,
    IReadOnlyDictionary<string, string> Headers,
    string Topic,
    int Partition,
    long Offset
);
