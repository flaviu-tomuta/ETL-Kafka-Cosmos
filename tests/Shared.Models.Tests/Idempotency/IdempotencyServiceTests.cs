using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.DependencyInjection;
using Shared.Models.Idempotency;
using Shared.Models.Models;

namespace Shared.Models.Tests.Idempotency;

public sealed class IdempotencyServiceTests
{
    private static (IdempotencyService Sut, Mock<Container> MockContainer, Mock<ILogger<IdempotencyService>> MockLogger) Build()
    {
        Mock<Container> mockContainer = new();
        Mock<ILogger<IdempotencyService>> mockLogger = new();
        IdempotencyContainer container = new(mockContainer.Object);
        IdempotencyService sut = new(container, mockLogger.Object);
        return (sut, mockContainer, mockLogger);
    }

    // ─── AC1: IsDuplicateAsync — message exists → true ───────────────────────

    [Fact]
    public async Task IsDuplicateAsync_WhenMessageExists_ReturnsTrue()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, _) = Build();
        mockContainer
            .Setup(c => c.ReadItemAsync<IdempotencyRecord>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<IdempotencyRecord>>());

        bool result = await sut.IsDuplicateAsync("msg-123");

        Assert.True(result);
    }

    // ─── AC1: IsDuplicateAsync — message exists → DuplicateMessageSkipped log ─

    [Fact]
    public async Task IsDuplicateAsync_WhenMessageExists_LogsDuplicateMessageSkipped()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, Mock<ILogger<IdempotencyService>> mockLogger) = Build();
        mockContainer
            .Setup(c => c.ReadItemAsync<IdempotencyRecord>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<IdempotencyRecord>>());

        await sut.IsDuplicateAsync("msg-123");

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DuplicateMessageSkipped")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─── AC2: IsDuplicateAsync — message not found → false ───────────────────

    [Fact]
    public async Task IsDuplicateAsync_WhenMessageNotFound_ReturnsFalse()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, _) = Build();
        CosmosException notFound = new("Not found", HttpStatusCode.NotFound, 0, "activity-1", 1.0);
        mockContainer
            .Setup(c => c.ReadItemAsync<IdempotencyRecord>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);

        bool result = await sut.IsDuplicateAsync("msg-123");

        Assert.False(result);
    }

    // ─── AC3: MarkProcessedAsync — writes IdempotencyRecord with correct fields

    [Fact]
    public async Task MarkProcessedAsync_WhenSuccess_CallsCreateItemAsync_WithIdEqualToMessageId()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, _) = Build();
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<IdempotencyRecord>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<IdempotencyRecord>>());

        await sut.MarkProcessedAsync("msg-123");

        mockContainer.Verify(c => c.CreateItemAsync(
            It.Is<IdempotencyRecord>(r =>
                r.Id == "msg-123" &&
                r.MessageId == "msg-123" &&
                r.Ttl == 604800),
            new PartitionKey("msg-123"),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC4: MarkProcessedAsync — 409 Conflict → success, no rethrow ────────

    [Fact]
    public async Task MarkProcessedAsync_When409Conflict_DoesNotRethrow()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, _) = Build();
        CosmosException conflict = new("Conflict", HttpStatusCode.Conflict, 0, "activity-1", 1.0);
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<IdempotencyRecord>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(conflict);

        Exception? thrown = await Record.ExceptionAsync(() => sut.MarkProcessedAsync("msg-123"));

        Assert.Null(thrown);
    }

    // ─── Non-409 CosmosException rethrows ────────────────────────────────────

    [Fact]
    public async Task MarkProcessedAsync_WhenNon409CosmosException_Rethrows()
    {
        (IdempotencyService sut, Mock<Container> mockContainer, _) = Build();
        CosmosException throttled = new("Too many requests", HttpStatusCode.TooManyRequests, 0, "activity-1", 1.0);
        mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<IdempotencyRecord>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(throttled);

        CosmosException thrown = await Assert.ThrowsAsync<CosmosException>(
            () => sut.MarkProcessedAsync("msg-123"));

        Assert.Equal(HttpStatusCode.TooManyRequests, thrown.StatusCode);
    }

    // ─── DI registration — IIdempotencyService registered as Scoped ──────────

    [Fact]
    public void AddSharedServices_RegistersIIdempotencyService_AsScoped()
    {
        ServiceCollection services = new();
        services.AddSharedServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdempotencyService));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(IdempotencyService), descriptor.ImplementationType);
    }
}
