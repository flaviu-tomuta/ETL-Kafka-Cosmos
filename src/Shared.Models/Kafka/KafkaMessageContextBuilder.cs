using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Shared.Models.Kafka;

public static class KafkaMessageContextBuilder
{
    public static KafkaMessageContext Build(
        IReadOnlyDictionary<string, string> headers,
        string partyId,
        string topic,
        int partition,
        long offset,
        string topicRole)
    {
        if (!headers.TryGetValue("messageId", out string? messageId) || string.IsNullOrEmpty(messageId))
            throw new MessageValidationException(
                "Missing messageId header",
                entityId: "unknown",
                messageId: "unknown");

        return new KafkaMessageContext
        {
            MessageId  = messageId,
            PartyId    = partyId,
            Topic      = topic,
            Partition  = partition,
            Offset     = offset,
            TopicRole  = topicRole,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    public static Dictionary<string, object> BuildLogScope(KafkaMessageContext ctx) =>
        new()
        {
            ["MessageId"] = ctx.MessageId,
            ["PartyId"]   = ctx.PartyId,
            ["Topic"]     = ctx.Topic,
            ["Partition"] = ctx.Partition,
            ["Offset"]    = ctx.Offset,
            ["TopicRole"] = ctx.TopicRole
        };
}
