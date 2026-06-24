using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DlqAdmin.Tests.Api;

public sealed class DlqApiTests
{
    private static ServiceBusReceivedMessage MakeMessage(
        string messageId,
        string partyId,
        string topic,
        int partition,
        long offset,
        string topicRole,
        int attemptCount,
        string body,
        DateTimeOffset enqueuedTime)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId,
            properties: new Dictionary<string, object>
            {
                ["partyId"]      = partyId,
                ["topic"]        = topic,
                ["partition"]    = partition,
                ["offset"]       = offset,
                ["topicRole"]    = topicRole,
                ["attemptCount"] = attemptCount
            },
            enqueuedTime: enqueuedTime);
    }

    private static (WebApplicationFactory<Program>, Mock<ServiceBusClient>, Mock<ServiceBusReceiver>, Mock<ServiceBusSender>) BuildFactory()
    {
        Mock<ServiceBusClient>   mockClient   = new();
        Mock<ServiceBusReceiver> mockReceiver = new();
        Mock<ServiceBusSender>   mockSender   = new();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);
        mockClient
            .Setup(c => c.CreateSender(It.IsAny<string>()))
            .Returns(mockSender.Object);

        WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.ConfigureServices(services =>
                {
                    services.RemoveAll<ServiceBusClient>();
                    services.AddSingleton(mockClient.Object);
                });
            });

        return (factory, mockClient, mockReceiver, mockSender);
    }

    [Fact]
    public async Task Get_WithMessages_ReturnsPeekedMessagesAsJson()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            DateTimeOffset enqueuedAt = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ServiceBusReceivedMessage msg = MakeMessage(
                "msg-1", "P1", "onboarding-topic", 0, 100L, "onboarding", 3,
                "{\"partyId\":\"P1\"}", enqueuedAt);

            mockReceiver
                .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.GetAsync("/api/dlq/onboarding-deadletter");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<JsonElement>? result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("msg-1",           result[0].GetProperty("messageId").GetString());
            Assert.Equal("P1",              result[0].GetProperty("partyId").GetString());
            Assert.Equal("onboarding-topic",result[0].GetProperty("topic").GetString());
            Assert.Equal("{\"partyId\":\"P1\"}", result[0].GetProperty("body").GetString());
            Assert.Equal(0,     result[0].GetProperty("partition").GetInt32());
            Assert.Equal(100L,  result[0].GetProperty("offset").GetInt64());
            Assert.Equal(3,     result[0].GetProperty("attempts").GetInt32());
            Assert.NotEqual(default, result[0].GetProperty("deadLetteredAt").GetDateTimeOffset());
            // reason field present (null for plain-queue messages — no Service Bus DLQ annotation)
            Assert.True(result[0].TryGetProperty("reason", out _));
        }
    }

    [Fact]
    public async Task Get_CallsPeekMessagesAsync_NotReceiveMessagesAsync()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            mockReceiver
                .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.GetAsync("/api/dlq/onboarding-deadletter");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            mockReceiver.Verify(
                r => r.PeekMessagesAsync(50, null, It.IsAny<CancellationToken>()),
                Times.Once());
            mockReceiver.Verify(
                r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }
    }

    [Fact]
    public async Task Get_WithEmptyQueue_ReturnsEmptyArray()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            mockReceiver
                .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.GetAsync("/api/dlq/amendment-deadletter");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<JsonElement>? result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task Requeue_WhenMessageFound_SendsToRetryQueueWithAttemptCountZeroAndReturnsOk()
    {
        (WebApplicationFactory<Program> factory, Mock<ServiceBusClient> mockClient, Mock<ServiceBusReceiver> mockReceiver, Mock<ServiceBusSender> mockSender) = BuildFactory();
        await using (factory)
        {
            ServiceBusReceivedMessage msg = MakeMessage(
                "msg-1", "P1", "onboarding-topic", 0, 100L, "onboarding", 3,
                "{\"partyId\":\"P1\"}", DateTimeOffset.UtcNow);

            mockReceiver
                .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });
            mockSender
                .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockReceiver
                .Setup(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.PostAsync(
                "/api/dlq/onboarding-deadletter/requeue/msg-1", content: null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("msg-1", body.GetProperty("requeued").GetString());

            mockClient.Verify(c => c.CreateSender("onboarding-retry"), Times.Once());
            mockSender.Verify(
                s => s.SendMessageAsync(
                    It.Is<ServiceBusMessage>(m =>
                        m.MessageId == "msg-1" &&
                        (int)m.ApplicationProperties["attemptCount"] == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once());
            mockReceiver.Verify(
                r => r.CompleteMessageAsync(
                    It.Is<ServiceBusReceivedMessage>(m => m.MessageId == "msg-1"),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }
    }

    [Fact]
    public async Task Requeue_WhenMessageNotFound_ReturnsNotFound()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            mockReceiver
                .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.PostAsync(
                "/api/dlq/onboarding-deadletter/requeue/missing-id", content: null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Discard_WhenMessageFound_CompletesMessageAndReturnsOk()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            ServiceBusReceivedMessage msg = MakeMessage(
                "msg-1", "P1", "amendment-topic", 1, 200L, "amendment", 2,
                "{\"partyId\":\"P1\"}", DateTimeOffset.UtcNow);

            mockReceiver
                .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });
            mockReceiver
                .Setup(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.DeleteAsync(
                "/api/dlq/amendment-deadletter/msg-1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("msg-1", body.GetProperty("discarded").GetString());
            mockReceiver.Verify(
                r => r.CompleteMessageAsync(
                    It.Is<ServiceBusReceivedMessage>(m => m.MessageId == "msg-1"),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }
    }

    [Fact]
    public async Task Discard_WhenMessageNotFound_ReturnsNotFound()
    {
        (WebApplicationFactory<Program> factory, _, Mock<ServiceBusReceiver> mockReceiver, _) = BuildFactory();
        await using (factory)
        {
            mockReceiver
                .Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            using HttpClient client = factory.CreateClient();
            HttpResponseMessage response = await client.DeleteAsync(
                "/api/dlq/amendment-deadletter/missing-id");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
