using Azure.Messaging.ServiceBus;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ServiceBusClient>(
    _ => new ServiceBusClient(builder.Configuration["ServiceBusConnection"]!));

WebApplication app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/dlq/{queueName}", async (string queueName, ServiceBusClient client) =>
{
    ServiceBusReceiver receiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions
        {
            SubQueue    = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

    IReadOnlyList<ServiceBusReceivedMessage> messages =
        await receiver.PeekMessagesAsync(maxMessages: 50);

    return messages.Select(m => new
    {
        MessageId      = m.MessageId,
        PartyId        = m.ApplicationProperties.GetValueOrDefault("partyId"),
        Topic          = m.ApplicationProperties.GetValueOrDefault("topic"),
        Partition      = m.ApplicationProperties.GetValueOrDefault("partition"),
        Offset         = m.ApplicationProperties.GetValueOrDefault("offset"),
        Reason         = m.DeadLetterReason,
        Attempts       = m.ApplicationProperties.GetValueOrDefault("attemptCount"),
        DeadLetteredAt = m.EnqueuedTime,
        Body           = m.Body.ToString()
    });
});

app.MapPost("/api/dlq/{queueName}/requeue/{messageId}", async (
    string queueName, string messageId, ServiceBusClient client) =>
{
    ServiceBusReceiver dlqReceiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

    IReadOnlyList<ServiceBusReceivedMessage> messages =
        await dlqReceiver.ReceiveMessagesAsync(maxMessages: 50);
    ServiceBusReceivedMessage? target =
        messages.FirstOrDefault(m => m.MessageId == messageId);

    if (target is null) return Results.NotFound();

    ServiceBusSender retrySender = client.CreateSender(
        queueName.Replace("-deadletter", "-retry"));

    ServiceBusMessage requeued = new ServiceBusMessage(target.Body)
    {
        MessageId = target.MessageId
    };
    requeued.ApplicationProperties["partyId"]      = target.ApplicationProperties["partyId"];
    requeued.ApplicationProperties["topic"]        = target.ApplicationProperties["topic"];
    requeued.ApplicationProperties["partition"]    = target.ApplicationProperties["partition"];
    requeued.ApplicationProperties["offset"]       = target.ApplicationProperties["offset"];
    requeued.ApplicationProperties["topicRole"]    = target.ApplicationProperties["topicRole"];
    requeued.ApplicationProperties["attemptCount"] = 0;

    await retrySender.SendMessageAsync(requeued);
    await dlqReceiver.CompleteMessageAsync(target);

    return Results.Ok(new { requeued = messageId });
});

app.MapDelete("/api/dlq/{queueName}/{messageId}", async (
    string queueName, string messageId, ServiceBusClient client) =>
{
    ServiceBusReceiver dlqReceiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

    IReadOnlyList<ServiceBusReceivedMessage> messages =
        await dlqReceiver.ReceiveMessagesAsync(maxMessages: 50);
    ServiceBusReceivedMessage? target =
        messages.FirstOrDefault(m => m.MessageId == messageId);

    if (target is null) return Results.NotFound();

    await dlqReceiver.CompleteMessageAsync(target);
    return Results.Ok(new { discarded = messageId });
});

app.Run();

public partial class Program { }
