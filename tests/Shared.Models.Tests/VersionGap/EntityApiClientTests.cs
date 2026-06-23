using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using Shared.Models.Models;
using Shared.Models.VersionGap;

namespace Shared.Models.Tests.VersionGap;

public sealed class EntityApiClientTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private static EntityApiClient Build(HttpResponseMessage response, string baseUrl = "http://test-api")
    {
        HttpClient httpClient = new(new FakeHttpMessageHandler(response))
        {
            BaseAddress = new Uri(baseUrl)
        };
        Mock<IConfiguration> mockConfig = new();
        mockConfig.Setup(c => c["ApiBaseUrl"]).Returns(baseUrl);
        return new EntityApiClient(httpClient, mockConfig.Object);
    }

    // ─── Successful response → returns deserialized EntityData ────────────────

    [Fact]
    public async Task GetEntityAtVersionAsync_WhenHttpReturns200_ReturnsEntityData()
    {
        EntityData expected = new() { Properties = new() { ["field1"] = "test" } };
        string json = JsonSerializer.Serialize(expected);
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        EntityApiClient sut = Build(response);
        EntityData result = await sut.GetEntityAtVersionAsync("entity-1", 5);

        Assert.NotNull(result);
    }

    // ─── Non-2xx response → throws HttpRequestException ──────────────────────

    [Fact]
    public async Task GetEntityAtVersionAsync_WhenHttpReturns404_ThrowsHttpRequestException()
    {
        HttpResponseMessage response = new(HttpStatusCode.NotFound);
        EntityApiClient sut = Build(response);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.GetEntityAtVersionAsync("entity-1", 5));
    }

    [Fact]
    public async Task GetEntityAtVersionAsync_WhenHttpReturns500_ThrowsHttpRequestException()
    {
        HttpResponseMessage response = new(HttpStatusCode.InternalServerError);
        EntityApiClient sut = Build(response);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.GetEntityAtVersionAsync("entity-1", 5));
    }
}
