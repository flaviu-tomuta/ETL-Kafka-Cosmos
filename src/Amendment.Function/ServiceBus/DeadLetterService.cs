using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.ServiceBus;

public sealed class DeadLetterService : IDeadLetterService
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<DeadLetterService> _logger;

    public DeadLetterService(ServiceBusClient client, ILogger<DeadLetterService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task SendAsync(
        KafkaMessageContext context,
        string originalPayload,
        string reason,
        int attempt)
    {
        string queueName = $"{context.TopicRole}-deadletter";
        await using ServiceBusSender sender = _client.CreateSender(queueName);

        ServiceBusMessage message = new(originalPayload);
        message.ApplicationProperties["messageId"]    = context.MessageId;
        message.ApplicationProperties["partyId"]      = context.PartyId;
        message.ApplicationProperties["topic"]        = context.Topic;
        message.ApplicationProperties["partition"]    = context.Partition;
        message.ApplicationProperties["offset"]       = context.Offset;
        message.ApplicationProperties["topicRole"]    = context.TopicRole;
        message.ApplicationProperties["attemptCount"] = attempt;

        await sender.SendMessageAsync(message);

        _logger.LogError(
            "MessageDeadLettered {MessageId} {EntityId} {Reason} attempt={Attempt}",
            context.MessageId,
            context.PartyId,
            reason,
            attempt);
    }
}
