using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Amendment.Function.ServiceBus;

public sealed class RetryService : IRetryService
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<RetryService> _logger;
    private const int MaxAttempts = 3;

    public RetryService(ServiceBusClient client, ILogger<RetryService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        KafkaMessageContext context,
        string originalPayload,
        int attemptCount,
        bool isImmediate = false)
    {
        int nextAttemptCount = attemptCount + 1;

        if (nextAttemptCount > MaxAttempts)
        {
            await RouteToDeadLetterAsync(context, originalPayload, nextAttemptCount);
            return;
        }

        string queueName = $"{context.TopicRole}-retry";
        await using ServiceBusSender sender = _client.CreateSender(queueName);

        ServiceBusMessage message = BuildMessage(context, originalPayload, nextAttemptCount);
        message.ScheduledEnqueueTime = isImmediate
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.UtcNow.AddSeconds(30);

        await sender.SendMessageAsync(message);
    }

    private async Task RouteToDeadLetterAsync(
        KafkaMessageContext context,
        string originalPayload,
        int finalAttemptCount)
    {
        string queueName = $"{context.TopicRole}-deadletter";
        await using ServiceBusSender sender = _client.CreateSender(queueName);

        ServiceBusMessage message = BuildMessage(context, originalPayload, finalAttemptCount);
        await sender.SendMessageAsync(message);

        _logger.LogError(
            "MessageDeadLettered {MessageId} {EntityId} {Reason} attempt={Attempt}",
            context.MessageId,
            context.PartyId,
            "MaxRetriesExhausted",
            finalAttemptCount);
    }

    private static ServiceBusMessage BuildMessage(
        KafkaMessageContext context,
        string originalPayload,
        int attemptCount)
    {
        ServiceBusMessage message = new(originalPayload);
        message.ApplicationProperties["messageId"]    = context.MessageId;
        message.ApplicationProperties["partyId"]      = context.PartyId;
        message.ApplicationProperties["topic"]        = context.Topic;
        message.ApplicationProperties["partition"]    = context.Partition;
        message.ApplicationProperties["offset"]       = context.Offset;
        message.ApplicationProperties["topicRole"]    = context.TopicRole;
        message.ApplicationProperties["attemptCount"] = attemptCount;
        return message;
    }
}
