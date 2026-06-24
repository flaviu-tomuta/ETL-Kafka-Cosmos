using System.Net;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DlqAdmin.Tests.UI;

public sealed class UiTests
{
    private static WebApplicationFactory<Program> BuildFactory()
    {
        Mock<ServiceBusClient>   mockClient   = new();
        Mock<ServiceBusReceiver> mockReceiver = new();

        mockClient
            .Setup(c => c.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
            .Returns(mockReceiver.Object);
        mockReceiver
            .Setup(r => r.PeekMessagesAsync(It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceBusReceivedMessage>());

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.ConfigureServices(services =>
                {
                    services.RemoveAll<ServiceBusClient>();
                    services.AddSingleton(mockClient.Object);
                });
            });
    }

    [Fact]
    public async Task GetIndexHtml_ReturnsOkWithHtmlContentType()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task IndexHtml_ContainsQueueSelectorForBothDeadletterQueues()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("amendment-deadletter", content);
        Assert.Contains("onboarding-deadletter", content);
    }

    [Fact]
    public async Task IndexHtml_ContainsMessageFieldDisplayForAllRequiredFields()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("partyId",       content);
        Assert.Contains("topic",         content);
        Assert.Contains("partition",     content);
        Assert.Contains("offset",        content);
        Assert.Contains("reason",        content);
        Assert.Contains("attempts",      content);
        Assert.Contains("deadLetteredAt", content);
    }

    [Fact]
    public async Task IndexHtml_ContainsExpandButtonForPayloadViewer()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Expand", content);
        // payload viewer element
        Assert.Contains("payload", content);
    }

    [Fact]
    public async Task IndexHtml_ContainsRequeueWithPostAndConfirmDialog()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Requeue",  content);
        Assert.Contains("requeue",  content);
        Assert.Contains("POST",     content);
        Assert.Contains("confirm(", content);
    }

    [Fact]
    public async Task IndexHtml_ContainsDiscardWithDeleteMethod()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Discard", content);
        Assert.Contains("DELETE",  content);
    }

    [Fact]
    public async Task IndexHtml_ContainsMetricCardsForAllThreeFailureReasons()
    {
        using WebApplicationFactory<Program> factory = BuildFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/index.html");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("EntityNotFound",          content);
        Assert.Contains("InvalidStateTransition",  content);
        Assert.Contains("HydrationFailed",         content);
    }
}
