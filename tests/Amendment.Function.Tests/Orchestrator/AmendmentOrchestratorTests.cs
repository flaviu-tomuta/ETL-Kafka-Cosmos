using System.Net;
using System.Text.Json;
using Amendment.Function.DependencyInjection;
using Amendment.Function.Handlers;
using Amendment.Function.Orchestrator;
using Amendment.Function.Pipeline;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Amendment.Function.Tests.Orchestrator;

public sealed class AmendmentOrchestratorTests
{
    // ── Private test doubles ─────────────────────────────────────────────────

    private sealed class TrackingRetryService : IRetryService
    {
        public bool WasCalled { get; private set; }

        public Task EnqueueAsync(KafkaMessageContext context, string originalPayload, int attemptCount, bool isImmediate = false)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingIdempotencyService : IIdempotencyService
    {
        public bool MarkProcessedCalled { get; private set; }

        public Task<bool> IsDuplicateAsync(string messageId) => Task.FromResult(false);

        public Task MarkProcessedAsync(string messageId)
        {
            MarkProcessedCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingAuditService : IAuditService
    {
        public bool FlushCalled { get; private set; }

        public Task FlushAsync(AuditRecord record)
        {
            FlushCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingOrchestrator : IAmendmentOrchestrator
    {
        public bool WasCalled { get; private set; }

        public Task<AmendmentResult> OrchestrateAsync(AmendmentMessage message, KafkaMessageContext context)
        {
            WasCalled = true;
            return Task.FromResult(new AmendmentResult());
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static KafkaMessageContext MakeContext(string partyId = "party-1") => new()
    {
        MessageId  = "msg-001",
        PartyId    = partyId,
        Topic      = "amendment",
        Partition  = 0,
        Offset     = 42,
        TopicRole  = "amendment",
        ReceivedAt = DateTimeOffset.UtcNow
    };

    private static (
        AmendmentOrchestrator Sut,
        Mock<Container> MockContainer,
        TrackingRetryService RetryService,
        TrackingIdempotencyService IdempotencyService,
        TrackingAuditService AuditService
    ) Build()
    {
        Mock<Container> mockContainer = new();
        TrackingRetryService retryService = new();
        TrackingIdempotencyService idempotencyService = new();
        TrackingAuditService auditService = new();

        List<IOperationHandler> handlers =
        [
            new AddOperationHandler(),
            new RemoveOperationHandler(),
            new UpdateOperationHandler()
        ];

        AmendmentOrchestrator sut = new(
            handlers,
            new EnrichedRecordsContainer(mockContainer.Object),
            idempotencyService,
            auditService,
            retryService);

        return (sut, mockContainer, retryService, idempotencyService, auditService);
    }

    private static void SetupReadSuccess(
        Mock<Container> mock,
        EnrichedCustomer entity,
        string etag = "etag-test")
    {
        Mock<ItemResponse<EnrichedCustomer>> response = new();
        response.SetupGet(r => r.Resource).Returns(entity);
        response.SetupGet(r => r.ETag).Returns(etag);

        mock.Setup(c => c.ReadItemAsync<EnrichedCustomer>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
    }

    private static void SetupUpsertSuccess(Mock<Container> mock)
    {
        mock.Setup(c => c.UpsertItemAsync<EnrichedCustomer>(
                It.IsAny<EnrichedCustomer>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<EnrichedCustomer>>());
    }

    // ── AC1: Pass 1 violation → BusinessRuleViolationException ───────────────

    [Fact]
    public async Task OrchestrateAsync_Pass1Violation_ThrowsBusinessRuleViolationException()
    {
        (AmendmentOrchestrator sut, Mock<Container> mockContainer, _, _, _) = Build();

        Address existing = new() { AddressId = "addr-001" };
        EnrichedCustomer storedEntity = new() { PartyId = "party-1", Addresses = [existing] };
        SetupReadSuccess(mockContainer, storedEntity);

        AmendmentMessage message = new()
        {
            PartyId = "party-1",
            AmendPayload = new()
            {
                OperationsPayload =
                [
                    new Operation
                    {
                        OperationType    = "add",
                        Parameters       = new(),
                        OperationDetails = new Dictionary<string, object> { ["addressId"] = "addr-001" }
                    }
                ]
            }
        };

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => sut.OrchestrateAsync(message, MakeContext()));
    }

    // ── AC1: Pass 1 violation → UpsertItemAsync never called (no apply) ──────

    [Fact]
    public async Task OrchestrateAsync_Pass1Violation_UpsertNeverCalled()
    {
        (AmendmentOrchestrator sut, Mock<Container> mockContainer, _, _, _) = Build();

        Address existing = new() { AddressId = "addr-001" };
        EnrichedCustomer storedEntity = new() { PartyId = "party-1", Addresses = [existing] };
        SetupReadSuccess(mockContainer, storedEntity);

        AmendmentMessage message = new()
        {
            PartyId = "party-1",
            AmendPayload = new()
            {
                OperationsPayload =
                [
                    new Operation
                    {
                        OperationType    = "add",
                        Parameters       = new(),
                        OperationDetails = new Dictionary<string, object> { ["addressId"] = "addr-001" }
                    }
                ]
            }
        };

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => sut.OrchestrateAsync(message, MakeContext()));

        mockContainer.Verify(c => c.UpsertItemAsync<EnrichedCustomer>(
            It.IsAny<EnrichedCustomer>(),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AC2: All ops valid, non-no-op → UpsertItemAsync with ETag ────────────

    [Fact]
    public async Task OrchestrateAsync_AllOpsValid_NonNoOp_CallsUpsertWithETagAndPostUpsertCallbacks()
    {
        (AmendmentOrchestrator sut,
            Mock<Container> mockContainer,
            _,
            TrackingIdempotencyService idempotency,
            TrackingAuditService audit) = Build();

        EnrichedCustomer storedEntity = new() { PartyId = "party-1", Version = 3 };
        SetupReadSuccess(mockContainer, storedEntity, etag: "etag-test");
        SetupUpsertSuccess(mockContainer);

        AmendmentMessage message = new()
        {
            PartyId = "party-1",
            AmendPayload = new()
            {
                OperationsPayload =
                [
                    new Operation
                    {
                        OperationType    = "add",
                        Parameters       = new(),
                        OperationDetails = new Dictionary<string, object>
                        {
                            ["addressId"]   = "addr-new",
                            ["line1"]       = "123 Main St",
                            ["city"]        = "Austin",
                            ["state"]       = "TX",
                            ["postalCode"]  = "78701",
                            ["country"]     = "US"
                        }
                    }
                ]
            }
        };

        AmendmentResult result = await sut.OrchestrateAsync(message, MakeContext());

        Assert.False(result.IsNoOp);
        mockContainer.Verify(c => c.UpsertItemAsync<EnrichedCustomer>(
            It.IsAny<EnrichedCustomer>(),
            It.IsAny<PartitionKey?>(),
            It.Is<ItemRequestOptions?>(o => o != null && o.IfMatchEtag == "etag-test"),
            It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(idempotency.MarkProcessedCalled);
        Assert.True(audit.FlushCalled);
    }

    // ── AC3: All no-ops → IsNoOp = true, no Cosmos write ─────────────────────

    [Fact]
    public async Task OrchestrateAsync_AllNoOps_ReturnsIsNoOp_UpsertNeverCalled()
    {
        (AmendmentOrchestrator sut, Mock<Container> mockContainer, _, _, _) = Build();

        Address existing = new()
        {
            AddressId  = "addr-001",
            Line1      = "123 Main St",
            City       = "Austin",
            State      = "TX",
            PostalCode = "78701",
            Country    = "US"
        };
        EnrichedCustomer storedEntity = new() { PartyId = "party-1", Addresses = [existing] };
        SetupReadSuccess(mockContainer, storedEntity);

        AmendmentMessage message = new()
        {
            PartyId = "party-1",
            AmendPayload = new()
            {
                OperationsPayload =
                [
                    new Operation
                    {
                        OperationType    = "update",
                        Parameters       = new Dictionary<string, object> { ["addressId"] = "addr-001" },
                        OperationDetails = new Dictionary<string, object> { ["line1"] = "123 Main St" }
                    }
                ]
            }
        };

        AmendmentResult result = await sut.OrchestrateAsync(message, MakeContext());

        Assert.True(result.IsNoOp);
        mockContainer.Verify(c => c.UpsertItemAsync<EnrichedCustomer>(
            It.IsAny<EnrichedCustomer>(),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AC4: UpsertItemAsync 412 PreconditionFailed → propagates ─────────────

    [Fact]
    public async Task OrchestrateAsync_Upsert412PreconditionFailed_Propagates()
    {
        (AmendmentOrchestrator sut, Mock<Container> mockContainer, _, _, _) = Build();

        EnrichedCustomer storedEntity = new() { PartyId = "party-1" };
        SetupReadSuccess(mockContainer, storedEntity);

        CosmosException preconditionFailed = new(
            "Precondition failed", HttpStatusCode.PreconditionFailed, 0, "activity-1", 1.0);
        mockContainer.Setup(c => c.UpsertItemAsync<EnrichedCustomer>(
                It.IsAny<EnrichedCustomer>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(preconditionFailed);

        AmendmentMessage message = new()
        {
            PartyId = "party-1",
            AmendPayload = new()
            {
                OperationsPayload =
                [
                    new Operation
                    {
                        OperationType    = "add",
                        Parameters       = new(),
                        OperationDetails = new Dictionary<string, object>
                        {
                            ["addressId"]  = "addr-new",
                            ["line1"]      = "123 Main St",
                            ["city"]       = "Austin",
                            ["state"]      = "TX",
                            ["postalCode"] = "78701",
                            ["country"]    = "US"
                        }
                    }
                ]
            }
        };

        CosmosException thrown = await Assert.ThrowsAsync<CosmosException>(
            () => sut.OrchestrateAsync(message, MakeContext()));

        Assert.Equal(HttpStatusCode.PreconditionFailed, thrown.StatusCode);
    }

    // ── AC5: ReadItemAsync NotFound → retry service called, no exception ──────

    [Fact]
    public async Task OrchestrateAsync_ReadNotFound_CallsRetryService_NoException()
    {
        (AmendmentOrchestrator sut,
            Mock<Container> mockContainer,
            TrackingRetryService retryService,
            _, _) = Build();

        CosmosException notFound = new("Not found", HttpStatusCode.NotFound, 0, "activity-1", 1.0);
        mockContainer.Setup(c => c.ReadItemAsync<EnrichedCustomer>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);

        AmendmentMessage message = new()
        {
            PartyId      = "party-1",
            AmendPayload = new() { OperationsPayload = [] }
        };

        Exception? thrown = await Record.ExceptionAsync(
            () => sut.OrchestrateAsync(message, MakeContext()));

        Assert.Null(thrown);
        Assert.True(retryService.WasCalled);
    }

    // ── AmendmentPipeline: deserializes payload and calls orchestrator ────────

    [Fact]
    public async Task AmendmentPipeline_ProcessAsync_DeserializesAndCallsOrchestrator()
    {
        TrackingOrchestrator orchestrator = new();
        AmendmentPipeline pipeline = new(orchestrator);
        KafkaMessageContext ctx = MakeContext();
        string payload = JsonSerializer.Serialize(new AmendmentMessage
        {
            PartyId      = "party-1",
            AmendPayload = new() { OperationsPayload = [] }
        });

        await pipeline.ProcessAsync(ctx, payload);

        Assert.True(orchestrator.WasCalled);
    }

    // ── DI registration: IAmendmentOrchestrator registered as Scoped ─────────

    [Fact]
    public void AddAmendmentServices_RegistersIAmendmentOrchestrator_AsScoped()
    {
        ServiceCollection services = new();
        services.AddAmendmentServices();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAmendmentOrchestrator));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(AmendmentOrchestrator), descriptor.ImplementationType);
    }
}
