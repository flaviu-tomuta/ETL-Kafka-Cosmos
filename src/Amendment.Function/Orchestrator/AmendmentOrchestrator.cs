using System.Net;
using System.Text.Json;
using Amendment.Function.Handlers;
using Microsoft.Azure.Cosmos;
using Shared.Models.Contracts;
using Shared.Models.CosmosDb;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Amendment.Function.Orchestrator;

public sealed class AmendmentOrchestrator : IAmendmentOrchestrator
{
    private readonly IEnumerable<IOperationHandler> _handlers;
    private readonly EnrichedRecordsContainer _container;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAuditService _auditService;
    private readonly IRetryService _retryService;

    public AmendmentOrchestrator(
        IEnumerable<IOperationHandler> handlers,
        EnrichedRecordsContainer container,
        IIdempotencyService idempotencyService,
        IAuditService auditService,
        IRetryService retryService)
    {
        _handlers = handlers;
        _container = container;
        _idempotencyService = idempotencyService;
        _auditService = auditService;
        _retryService = retryService;
    }

    public async Task<AmendmentResult> OrchestrateAsync(
        AmendmentMessage message,
        KafkaMessageContext context)
    {
        string partyId = message.PartyId;

        ItemResponse<EnrichedCustomer> response;
        try
        {
            response = await _container.Value.ReadItemAsync<EnrichedCustomer>(
                partyId, new PartitionKey(partyId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            string payload = JsonSerializer.Serialize(message);
            await _retryService.EnqueueAsync(context, payload, attemptCount: 1);
            return new AmendmentResult { IsNoOp = true };
        }

        EnrichedCustomer storedEntity = response.Resource;
        List<Operation> operations = message.AmendPayload.OperationsPayload;

        // Pass 1 — validate all, collect violations and no-ops
        List<(int index, ValidationResult result)> violations = [];
        List<int> noOps = [];

        for (int i = 0; i < operations.Count; i++)
        {
            Operation op = operations[i];
            IOperationHandler handler = OperationHandlerResolver.Resolve(
                _handlers, op.OperationType, partyId, context.MessageId);
            ValidationResult result = handler.Validate(op, storedEntity);
            if (result.IsRejected)
                violations.Add((i, result));
            else if (result.IsNoOp)
                noOps.Add(i);
        }

        if (violations.Any())
            throw new BusinessRuleViolationException(
                $"Amendment validation failed: {string.Join(", ", violations.Select(v => v.result.Reason))}",
                partyId,
                context.MessageId,
                violations[0].result.Reason ?? "ValidationFailed");

        if (noOps.Count == operations.Count)
            return new AmendmentResult { IsNoOp = true };

        // Pass 2 — apply non-no-op operations sequentially
        EnrichedCustomer updatedEntity = storedEntity;
        for (int i = 0; i < operations.Count; i++)
        {
            if (noOps.Contains(i)) continue;
            Operation op = operations[i];
            IOperationHandler handler = OperationHandlerResolver.Resolve(
                _handlers, op.OperationType, partyId, context.MessageId);
            updatedEntity = handler.Apply(op, updatedEntity);
        }

        updatedEntity = updatedEntity with
        {
            Version       = storedEntity.Version + 1,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "amendment-app",
            AuditSummary  = new AuditSummary
            {
                LastMessageId      = context.MessageId,
                LastKafkaOffset    = context.Offset,
                LastHydrationSteps = [],
                WasApiFallback     = false,
                ProcessedAt        = DateTimeOffset.UtcNow
            }
        };

        // Pass 3 — ETag-gated upsert
        await _container.Value.UpsertItemAsync(
            updatedEntity,
            new PartitionKey(partyId),
            new ItemRequestOptions { IfMatchEtag = response.ETag });

        await _idempotencyService.MarkProcessedAsync(context.MessageId);
        await _auditService.FlushAsync(BuildAuditRecord(context, storedEntity, updatedEntity));

        return new AmendmentResult { IsNoOp = false, Entity = updatedEntity };
    }

    private static AuditRecord BuildAuditRecord(
        KafkaMessageContext context,
        EnrichedCustomer storedEntity,
        EnrichedCustomer updatedEntity) => new()
    {
        Id            = context.MessageId,
        MessageId     = context.MessageId,
        PartyId       = context.PartyId,
        TopicRole     = context.TopicRole,
        ProcessedDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
        ProcessedAt   = DateTimeOffset.UtcNow,
        KafkaContext  = new KafkaContextInfo
        {
            Topic      = context.Topic,
            Partition  = context.Partition,
            Offset     = context.Offset,
            ReceivedAt = context.ReceivedAt
        },
        Versions = new VersionInfo
        {
            Stored   = storedEntity.Version,
            Incoming = updatedEntity.Version,
            Gap      = updatedEntity.Version - storedEntity.Version
        },
        Outcome   = "success",
        Hydration = new HydrationInfo(),
        RetryInfo = new RetryInfo { AttemptNumber = 1, WasRetry = false }
    };
}
