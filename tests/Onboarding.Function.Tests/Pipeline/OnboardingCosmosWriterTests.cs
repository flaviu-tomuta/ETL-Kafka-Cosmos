using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using Onboarding.Function.Pipeline;
using Shared.Models.CosmosDb;
using Shared.Models.Models;

namespace Onboarding.Function.Tests.Pipeline;

public sealed class OnboardingCosmosWriterTests
{
    private static EnrichedCustomer BuildEntity(string partyId = "party-123") =>
        new() { Id = partyId, PartyId = partyId };

    // ─── AC1: WriteAsync calls UpsertItemAsync with correct entity and partition key ───

    [Fact]
    public async Task WriteAsync_CallsUpsertItemAsync_WithEntityAndMatchingPartitionKey()
    {
        Mock<Container> mockContainer = new();
        mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<EnrichedCustomer>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<EnrichedCustomer>>());

        EnrichedRecordsContainer container = new(mockContainer.Object);
        OnboardingCosmosWriter sut = new(container);
        EnrichedCustomer entity = BuildEntity("party-abc");

        await sut.WriteAsync(entity);

        mockContainer.Verify(c => c.UpsertItemAsync(
            entity,
            new PartitionKey("party-abc"),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC2: No IfMatchEtag — blind upsert, not ETag-gated ──────────────────

    [Fact]
    public async Task WriteAsync_DoesNotSetIfMatchEtag_IsBlindUpsert()
    {
        Mock<Container> mockContainer = new();
        mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<EnrichedCustomer>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<EnrichedCustomer>>());

        EnrichedRecordsContainer container = new(mockContainer.Object);
        OnboardingCosmosWriter sut = new(container);

        await sut.WriteAsync(BuildEntity());

        mockContainer.Verify(c => c.UpsertItemAsync(
            It.IsAny<EnrichedCustomer>(),
            It.IsAny<PartitionKey?>(),
            It.Is<ItemRequestOptions?>(opts => opts == null || opts.IfMatchEtag == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC3: CosmosException propagates — not swallowed ─────────────────────

    [Fact]
    public async Task WriteAsync_WhenUpsertThrowsCosmosException_PropagatesException()
    {
        CosmosException cosmosException = new(
            "Too many requests",
            HttpStatusCode.TooManyRequests,
            0,
            "activity-1",
            1.0);

        Mock<Container> mockContainer = new();
        mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<EnrichedCustomer>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(cosmosException);

        EnrichedRecordsContainer container = new(mockContainer.Object);
        OnboardingCosmosWriter sut = new(container);

        CosmosException thrown = await Assert.ThrowsAsync<CosmosException>(
            () => sut.WriteAsync(BuildEntity()));

        Assert.Equal(HttpStatusCode.TooManyRequests, thrown.StatusCode);
    }
}
