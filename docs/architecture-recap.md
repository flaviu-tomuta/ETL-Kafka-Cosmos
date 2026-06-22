# Azure Function App Architecture — Planning Recap

## Overview

A set of C# Azure Function Apps deployed per Kafka topic, each responsible for consuming state update messages, hydrating the data based on business rules, and persisting enriched documents to Cosmos DB. Audit metrics are captured via structured logging to Application Insights and written as documents to a dedicated Cosmos audit container.

---

## Hosting and trigger

- **Runtime**: .NET 8 isolated worker model
- **Trigger**: Kafka trigger via `Microsoft.Azure.WebJobs.Extensions.Kafka`
- **Hosting plan**: Flex Consumption — pre-warmed instances without the cost of a Premium plan; avoids cold start issues that would affect the Consumption plan at this volume
- **Deployment model**: One Function App per Kafka topic (e.g. `onboarding-func`, `amendment-func`). Each app has a single responsibility aligned to its topic's role
- **Batch processing**: Configure `MaxBatchSize` and `MinBatchSize` on the Kafka trigger to process messages in chunks per invocation, reducing per-invocation overhead at tens of thousands of messages per hour

---

## Kafka topics and app roles

| Function App | Topic role | Write behaviour |
|---|---|---|
| `onboarding-func` | Initial entity creation | Blind upsert — always the source of truth |
| `amendment-func` | State updates to existing entities | Read-before-write with conditional upsert |

Each topic has a distinct role. No app handles multiple topics.

---

## Idempotency

### Onboarding app
Blind upsert — no idempotency check. Re-delivering an onboarding message is harmless; it overwrites the document with identical data. The simplicity benefit outweighs the need for a check.

### Amendment app
Full idempotency check using a dedicated Cosmos container. The amendment app's read-before-write conditional logic means a re-delivered message could silently roll back a subsequent amendment if blindly applied.

**Idempotency container**: `idempotency-records`
- Partition key: `messageId` (same as `id`)
- TTL: Kafka retention period + safety margin (e.g. 7 days)
- Write strategy: `CreateItemAsync` — a `409 Conflict` on concurrent execution means the other instance already processed the message; treat as handled

```csharp
// Check
try
{
    await _container.ReadItemAsync<IdempotencyRecord>(
        messageId, new PartitionKey(messageId));
    return; // duplicate — skip
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // not seen before — proceed
}

// Mark (after successful pipeline completion only)
await _container.CreateItemAsync(record, new PartitionKey(messageId));
```

---

## Cosmos DB — data model

### Database layout

| Container | Purpose | Partition key | TTL |
|---|---|---|---|
| `enriched-records` | Enriched entity documents | `entityId` | None (permanent) |
| `audit-metrics` | Processing audit trail | `processedDate` (YYYY-MM-DD) | Compliance window (e.g. 180 days) |
| `idempotency-records` | Duplicate detection (amendment only) | `messageId` | 7 days |

### Enriched document schema

Both onboarding and amendment apps write to `enriched-records`. The document is shared; each app owns its section.

```json
{
  "id": "entity-123",
  "entityId": "entity-123",
  "schemaVersion": 1,
  "status": "active",
  "lastUpdatedAt": "2026-06-09T11:00:00Z",
  "lastUpdatedBy": "amendment-app",
  "version": 5,

  "onboarding": {
    "completedAt": "2026-06-09T10:00:00Z",
    "data": {}
  },

  "amendments": {
    "lastAmendedAt": "2026-06-09T11:00:00Z",
    "changeCount": 3,
    "data": {}
  },

  "auditSummary": {
    "lastMessageId": "msg-xyz",
    "lastKafkaOffset": 48291,
    "lastHydrationSteps": ["AddressEnrichment", "ComplianceCheck"],
    "wasApiFallback": false,
    "processedAt": "2026-06-09T11:00:00Z"
  }
}
```

### Partition key rationale

`entityId` is used as both `id` and partition key for `enriched-records`. This enables **point reads** (cheapest Cosmos operation, ~1 RU) for all downstream API access. Each document represents the **latest state only** — no history accumulation. Upserts replace the previous document for that entity.

### Optimistic concurrency (amendment app only)

The amendment app reads before writing. Two concurrent executions for the same entity could produce a lost update without ETag gating:

```csharp
ItemResponse<EnrichedEntity> response = await container.ReadItemAsync<EnrichedEntity>(
    entityId, new PartitionKey(entityId));

ItemRequestOptions options = new() { IfMatchEtag = response.ETag };

try
{
    await container.UpsertItemAsync(updatedEntity, new PartitionKey(entityId), options);
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
{
    // ETag conflict — requeue to retry queue
}
```

### Throughput

Use **autoscale throughput** on all containers with a low minimum. Cosmos autoscale only charges for RUs consumed beyond the baseline, keeping idle costs low.

---

## Dead-letter and retry flow (amendment app)

### Service Bus topology

Two queues per function app:

| Queue | Purpose |
|---|---|
| `amendment-retry` | Messages awaiting re-processing with a scheduled delay |
| `amendment-deadletter` | Messages exhausted after 3 attempts, awaiting human approval |

Do not use the Service Bus built-in dead-letter sub-queue — use a plain queue so the approval → requeue path is a straightforward `SendMessageAsync`.

### Retry mechanics

- **Delay**: 30 seconds scheduled enqueue via `ScheduledEnqueueTime` on the Service Bus message — gives the onboarding app time to write its document before an amendment retries
- **Attempt tracking**: Carried as a custom `ApplicationProperty` (`attemptCount`) on the message, not Service Bus's built-in `DeliveryCount`
- **Max attempts**: 3 — after which the message moves to the dead-letter queue

```csharp
var retryMessage = new ServiceBusMessage(originalPayload)
{
    ApplicationProperties =
    {
        ["attemptCount"]      = attemptCount + 1,
        ["entityId"]          = entityId,
        ["originalMessageId"] = originalMessageId
    },
    ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddSeconds(30)
};
```

### Human approval → requeue

On dead-letter, an App Insights alert fires. A lightweight admin tool (Logic App or internal API) lists parked messages, presents them for review, and on approval sends the message back to the retry queue with `attemptCount` reset to `0` for a full fresh set of 3 attempts.

### Retry trigger scenarios

| Scenario | Action |
|---|---|
| Onboarding document not found | Enqueue to retry queue with 30s delay |
| ETag conflict (concurrent write) | Enqueue to retry queue immediately |
| Hydration failure | Enqueue to retry queue |
| 3 attempts exhausted | Move to dead-letter queue + fire App Insights alert |

---

## Hydration pipeline

### Version gap check

The version number lives in the **message body** (not the header). Deserialization happens first, then the version gap is evaluated:

```csharp
var payload = JsonSerializer.Deserialize<EntityMessage>(message.Value);
int incomingVersion = payload.Version;
int storedVersion = storedEntity?.Version ?? 0;
int gap = incomingVersion - storedVersion;

EntityData data = gap >= 2
    ? await _apiClient.GetEntityAtVersionAsync(payload.EntityId, incomingVersion) // API fallback
    : payload.Data; // trust Kafka payload
```

A gap of 2 or more means at least one message was never delivered to the topic. The missing version is fetched from the external API. Every version gap is logged as a warning — a spike in gaps indicates something systemic upstream.

### HydrationContext

A read-only carrier object that flows through the pipeline. Steps read from it; they never mutate it:

```csharp
public record HydrationContext
{
    public string EntityId       { get; init; }
    public string MessageId      { get; init; }
    public string TopicRole      { get; init; }
    public int IncomingVersion   { get; init; }
    public int StoredVersion     { get; init; }
    public EntityData Data       { get; init; }
    public bool WasApiFallback   { get; init; }
}
```

### IEnrichmentStep interface

Each step declares its own applicability. No central switch statement:

```csharp
public interface IEnrichmentStep
{
    bool AppliesTo(HydrationContext context);
    Task<EnrichmentResult> EnrichAsync(HydrationContext context);
}
```

Steps are registered in DI. Adding a new step is one class and one DI registration — nothing else changes:

```csharp
services.AddScoped<IEnrichmentStep, AddressEnrichmentStep>();
services.AddScoped<IEnrichmentStep, CreditCheckEnrichmentStep>();
services.AddScoped<IEnrichmentStep, ComplianceEnrichmentStep>();
```

### Parallel execution

Steps are independent — `Task.WhenAll` runs them concurrently. Per-message latency is bounded by the slowest single step, not the sum of all steps:

```csharp
var applicableSteps = _steps.Where(s => s.AppliesTo(context));
var results = await Task.WhenAll(
    applicableSteps.Select(s => s.EnrichAsync(context)));
```

### Output assembler

Merges all `EnrichmentResult` objects into the final document and populates `auditSummary` in one place. Called once after all steps complete:

```csharp
public EnrichedEntity Assemble(HydrationContext context, IEnumerable<EnrichmentResult> results)
{
    var applied = results.Where(r => r.Applied).ToList();

    return new EnrichedEntity
    {
        Id            = context.EntityId,
        EntityId      = context.EntityId,
        Version       = context.IncomingVersion,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        Data          = MergeData(context.Data, applied),
        AuditSummary  = new AuditSummary
        {
            MessageId             = context.MessageId,
            HydrationStepsApplied = applied.Select(r => r.StepName).ToList(),
            WasApiFallback        = context.WasApiFallback,
            ProcessedAt           = DateTimeOffset.UtcNow,
            TotalDurationMs       = applied.Sum(r => r.Duration.TotalMilliseconds)
        }
    };
}
```

### API fallback caching

Version gaps should be rare, but if the same entity triggers multiple gaps in a short window, cache the API response briefly to avoid hammering the external service:

```csharp
var cacheKey = $"api-entity-{entityId}-v{version}";
if (!_cache.TryGetValue(cacheKey, out EntityData data))
{
    data = await _apiClient.GetEntityAtVersionAsync(entityId, version);
    _cache.Set(cacheKey, data, TimeSpan.FromSeconds(60));
}
```

---

## Kafka message context

### Identifier roles

| Identifier | Location | Purpose |
|---|---|---|
| `messageId` | Kafka header | Audit and tracing identity — idempotency store key, audit records, all log entries |
| `partyId` | Message body + Kafka partition key | Entity identity — Cosmos `id` and partition key, primary business key throughout pipeline |

`messageId` is consistent across all topics and always lives in the header. `partyId` is the Kafka partition key, meaning all messages for the same entity land on the same partition in order.

### KafkaMessageContext

Populated at the function entry point before anything else runs. Flows through the entire pipeline:

```csharp
public record KafkaMessageContext
{
    public string MessageId          { get; init; }  // header — audit identity
    public string PartyId            { get; init; }  // body — entity identity
    public string Topic              { get; init; }
    public int    Partition          { get; init; }
    public long   Offset             { get; init; }
    public string TopicRole          { get; init; }  // "onboarding" | "amendment"
    public DateTimeOffset ReceivedAt { get; init; }
}
```

### Log scope — automatic context propagation

Register a log scope at the entry point so every downstream log entry carries the full Kafka context in `customDimensions` automatically:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["MessageId"] = ctx.MessageId,
    ["PartyId"]   = ctx.PartyId,
    ["Topic"]     = ctx.Topic,
    ["Partition"] = ctx.Partition,
    ["Offset"]    = ctx.Offset,
    ["TopicRole"] = ctx.TopicRole
}))
{
    await ProcessMessageAsync(message, ctx);
}
```

### Service Bus context propagation

Full Kafka context travels with every retry and dead-letter message:

```csharp
var retryMessage = new ServiceBusMessage(originalPayload)
{
    ApplicationProperties =
    {
        ["messageId"]    = ctx.MessageId,
        ["partyId"]      = ctx.PartyId,
        ["topic"]        = ctx.Topic,
        ["partition"]    = ctx.Partition,
        ["offset"]       = ctx.Offset,
        ["topicRole"]    = ctx.TopicRole,
        ["attemptCount"] = attemptCount + 1
    },
    ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddSeconds(30)
};
```

---

## Audit metrics

### Two sinks, two purposes

| | Application Insights | Cosmos `audit-metrics` |
|---|---|---|
| Primary audience | Engineers | Business stakeholders + compliance |
| Data shape | Structured log entries | JSON documents |
| Query style | KQL dashboards + alerts | Partition scans by date / entity |
| Retention | 90 days (configurable) | Compliance window via TTL |

### Application Insights — ILogger (not TelemetryClient)

`ILogger` structured logs flow to App Insights automatically through the Azure Functions host. Structured properties become queryable fields in `customDimensions` — no custom event ingestion cost:

```csharp
// Successful processing
_logger.LogInformation(
    "MessageProcessed {MessageId} {EntityId} {TopicRole} {StepsApplied} {WasApiFallback} {TotalDurationMs}ms",
    record.MessageId, record.EntityId, record.TopicRole,
    string.Join(",", record.Hydration.StepsApplied),
    record.Versions.WasApiFallback, record.TotalDurationMs);

// Version gap
_logger.LogWarning(
    "VersionGapDetected {EntityId} stored={StoredVersion} incoming={IncomingVersion} gap={Gap}",
    context.EntityId, context.StoredVersion, context.IncomingVersion, gap);

// Duplicate skipped
_logger.LogInformation(
    "DuplicateMessageSkipped {MessageId} {EntityId} {TopicRole}",
    context.MessageId, context.EntityId, context.TopicRole);

// Dead-lettered
_logger.LogError(
    "MessageDeadLettered {MessageId} {EntityId} {Reason} attempt={Attempt}",
    messageId, entityId, reason, attemptCount);
```

### TelemetryClient — retained for dependency tracking only

External API calls (version gap fallback) are tracked as dependencies so they appear in the App Insights dependency map and failure analysis:

```csharp
_telemetryClient.TrackDependency(new DependencyTelemetry
{
    Name      = "EntityApi.GetEntityAtVersion",
    Target    = _apiBaseUrl,
    Data      = $"GET /entities/{entityId}/versions/{version}",
    Duration  = timer.Elapsed,
    Success   = success,
    Timestamp = startTime
});
```

### Sampling configuration (`host.json`)

Adaptive sampling keeps ingestion costs down at high message volume. Exceptions and traces are excluded from sampling — always captured in full:

```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20,
        "excludedTypes": "Exception;Trace"
      },
      "enableDependencyTracking": true
    },
    "logLevel": {
      "default": "Information",
      "Function": "Information",
      "Host": "Warning"
    }
  }
}
```

### Cosmos audit document schema

```json
{
  "id": "msg-abc-123",
  "messageId": "msg-abc-123",
  "partyId": "12345",
  "topicRole": "amendment",
  "processedDate": "2026-06-09",
  "processedAt": "2026-06-09T11:00:00Z",

  "kafkaContext": {
    "topic":      "amendments",
    "partition":  3,
    "offset":     48291,
    "receivedAt": "2026-06-09T10:59:59Z"
  },

  "versions": {
    "stored": 4,
    "incoming": 5,
    "gap": 1,
    "wasApiFallback": false
  },

  "outcome": "success",
  "failureReason": null,

  "hydration": {
    "stepsApplied": ["AddressEnrichment", "ComplianceCheck"],
    "stepsSkipped": ["CreditCheck"],
    "totalDurationMs": 142,
    "stepBreakdown": [
      { "step": "AddressEnrichment", "durationMs": 80,  "applied": true  },
      { "step": "ComplianceCheck",   "durationMs": 62,  "applied": true  },
      { "step": "CreditCheck",       "durationMs": 0,   "applied": false }
    ]
  },

  "retryInfo": {
    "attemptNumber": 1,
    "wasRetry": false,
    "deadLettered": false
  },

  "ttl": 15552000
}
```

`ttl` is set per document (in seconds) to allow per-record retention extension for compliance disputes. 15,552,000 = 180 days.

### AuditService — single flush point

```csharp
public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly Container _auditContainer;

    public async Task FlushAsync(AuditRecord record)
    {
        _logger.LogInformation(
            "MessageProcessed {MessageId} {EntityId} {TopicRole} " +
            "{StepsApplied} {WasApiFallback} {TotalDurationMs}ms",
            record.MessageId, record.EntityId, record.TopicRole,
            string.Join(",", record.Hydration.StepsApplied),
            record.Versions.WasApiFallback, record.TotalDurationMs);

        await _auditContainer.CreateItemAsync(
            record.ToCosmosDocument(),
            new PartitionKey(record.ProcessedDate));
    }
}
```

### Recommended App Insights alerts

| Alert | Condition | Audience |
|---|---|---|
| Dead-letter spike | `MessageDeadLettered` count > 5 in 5 min | Engineers |
| Version gap spike | `VersionGapDetected` avg gap > 2 over 10 min | Engineers + business |
| Pipeline latency | `TotalDurationMs` p95 > 2000ms | Engineers |
| Failure rate | `outcome:failure` > 1% of events in 15 min | Engineers |
| API fallback rate | `WasApiFallback:true` > 10% of events in 30 min | Engineers + business |

---

## Data model

### Overview

One Cosmos DB document per customer, stored in the `enriched-records` container with `partyId` as both the document `id` and partition key. The document contains all customer information, addresses, contact details, and bank operations as embedded collections. Each collection item carries a stable ID used by the amendment app for lookup operations.

### C# models

```csharp
public record EnrichedCustomer
{
    public string Id                     { get; init; }  // same as PartyId
    public string PartyId                { get; init; }
    public int    SchemaVersion          { get; init; } = 1;
    public DateTimeOffset LastUpdatedAt  { get; init; }
    public string LastUpdatedBy          { get; init; }  // "onboarding-app" | "amendment-app"
    public int    Version                { get; init; }

    public CustomerInfo       Customer       { get; init; }
    public List<Address>      Addresses      { get; init; } = [];
    public List<PhoneNumber>  PhoneNumbers   { get; init; } = [];
    public List<EmailAddress> EmailAddresses { get; init; } = [];
    public List<BankOperation> BankOperations { get; init; } = [];
    public OnboardingInfo     Onboarding     { get; init; }
    public AuditSummary       AuditSummary   { get; init; }
}

public record CustomerInfo
{
    public string FirstName     { get; init; }
    public string MiddleName    { get; init; }
    public string LastName      { get; init; }
    public string PreferredName { get; init; }
    public TaxInfo TaxInfo      { get; init; }
}

public record TaxInfo
{
    public TaxIdType TaxIdType { get; init; }
    public string    TaxId     { get; init; }  // masked at rest — last 4 digits only
}

public enum TaxIdType { SSN, EIN }

public record Address
{
    public string      AddressId   { get; init; }  // stable ID for amendment lookup
    public AddressType AddressType { get; init; }
    public bool        IsPreferred { get; init; }
    public string      Line1       { get; init; }
    public string      Line2       { get; init; }  // nullable
    public string      City        { get; init; }
    public string      State       { get; init; }
    public string      PostalCode  { get; init; }
    public string      Country     { get; init; }
    public DateTimeOffset AddedAt  { get; init; }
    public string      AddedBy     { get; init; }
}

public enum AddressType { Primary, Secondary, Mailing, Billing, Previous }

public record PhoneNumber
{
    public string    PhoneId     { get; init; }  // stable ID for amendment lookup
    public PhoneType PhoneType   { get; init; }
    public bool      IsPreferred { get; init; }
    public string    Number      { get; init; }  // E.164 format — e.g. +15125550001
    public string    Extension   { get; init; }  // nullable
    public DateTimeOffset AddedAt { get; init; }
    public string    AddedBy     { get; init; }
}

public enum PhoneType { Mobile, Home, Work, Fax }

public record EmailAddress
{
    public string    EmailId     { get; init; }  // stable ID for amendment lookup
    public EmailType EmailType   { get; init; }
    public bool      IsPreferred { get; init; }
    public string    Address     { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public string    AddedBy     { get; init; }
}

public enum EmailType { Personal, Work, Other }

public record BankOperation
{
    public string OperationId   { get; init; }  // stable ID for amendment lookup
    public string AccountNumber { get; init; }
    public string AccountType   { get; init; }
    public string BankName      { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public string AddedBy       { get; init; }
}

public record OnboardingInfo
{
    public DateTimeOffset CompletedAt        { get; init; }
    public string         ExternalCustomerNo { get; init; }
    public string         EffectiveDate      { get; init; }
}
```

### Full document schema

```json
{
  "id": "12345",
  "partyId": "12345",
  "schemaVersion": 1,
  "lastUpdatedAt": "2026-06-18T11:00:00Z",
  "lastUpdatedBy": "onboarding-app",
  "version": 3,

  "customer": {
    "firstName": "John",
    "middleName": "Michael",
    "lastName": "Smith",
    "preferredName": "Johnny",
    "taxInfo": {
      "taxIdType": "SSN",
      "taxId": "***-**-6789"
    }
  },

  "addresses": [
    {
      "addressId": "addr-001",
      "addressType": "Primary",
      "isPreferred": true,
      "line1": "123 Main Street",
      "line2": "Apt 4B",
      "city": "Austin",
      "state": "TX",
      "postalCode": "78701",
      "country": "US",
      "addedAt": "2026-06-09T10:00:00Z",
      "addedBy": "onboarding-app"
    }
  ],

  "phoneNumbers": [
    {
      "phoneId": "phone-001",
      "phoneType": "Mobile",
      "isPreferred": true,
      "number": "+15125550001",
      "extension": null,
      "addedAt": "2026-06-09T10:00:00Z",
      "addedBy": "onboarding-app"
    }
  ],

  "emailAddresses": [
    {
      "emailId": "email-001",
      "emailType": "Personal",
      "isPreferred": true,
      "address": "john.smith@email.com",
      "addedAt": "2026-06-09T10:00:00Z",
      "addedBy": "onboarding-app"
    }
  ],

  "bankOperations": [
    {
      "operationId": "op-001",
      "accountNumber": "9876543210",
      "accountType": "savings",
      "bankName": "ABC Bank",
      "addedAt": "2026-06-09T10:00:00Z",
      "addedBy": "onboarding-app"
    }
  ],

  "onboarding": {
    "completedAt": "2026-06-09T10:00:00Z",
    "externalCustomerNo": "12345",
    "effectiveDate": "2026-06-09"
  },

  "auditSummary": {
    "lastMessageId": "msg-xyz",
    "lastKafkaOffset": 48291,
    "lastHydrationSteps": ["AddressEnrichment"],
    "wasApiFallback": false,
    "processedAt": "2026-06-18T11:00:00Z"
  }
}
```

### Key design decisions

**Stable IDs on every collection item** — `addressId`, `phoneId`, `emailId`, and `operationId` give the amendment app a clean single-field lookup in `parameters`. No composite key matching needed.

**`isPreferred` flag** — exactly one item per collection should have `isPreferred: true` at any time. The amendment validator enforces this — if an `add` or `update` sets `isPreferred: true`, it must clear the flag on the current preferred item first.

**`addedBy` on every collection item** — tracks which app wrote each item for audit and debugging. Consistent across all four collections.

**Tax IDs are mutually exclusive** — a customer has either an SSN or an EIN, never both. The `TaxIdType` enum discriminates between them. `TaxId` is masked at rest — only the last 4 digits are stored. Masking happens in the onboarding hydration step before the document is assembled.

**Phone numbers in E.164 format** — `+15125550001` rather than `(512) 555-0001`. Consistent format avoids formatting logic in downstream APIs.

**Amendment `parameters` use stable IDs** — all four collection types follow the same pattern:

```json
{ "operationType": "update", "parameters": { "addressId": "addr-001" }, "operationDetails": { "line1": "456 New Street" } }
{ "operationType": "remove", "parameters": { "phoneId": "phone-002" }, "operationDetails": {} }
{ "operationType": "add",    "parameters": {}, "operationDetails": { "emailId": "email-003", "emailType": "Work", "address": "j@co.com" } }
```

---

## Key decisions summary

| Concern | Decision | Rationale |
|---|---|---|
| Hosting plan | Flex Consumption | Pre-warmed instances, cost-efficient at this volume |
| Deployment model | One Function App per topic | Clean ownership, independent scaling and deployment |
| Onboarding write | Blind upsert | Source of truth, re-delivery is harmless |
| Amendment write | Read → ETag upsert | Prevents lost updates from concurrent processing |
| Onboarding idempotency | None | Blind upsert handles it implicitly |
| Amendment idempotency | Cosmos `idempotency-records` + `CreateItemAsync` | Protects conditional logic from re-delivery |
| Partition key (`enriched-records`) | `entityId` = `id` | Point reads, one document per entity, cheapest Cosmos access pattern |
| Partition key (`audit-metrics`) | `processedDate` | Append-friendly, time-windowed queries, cold partition ageing |
| Amendment before onboarding | Dead-letter + retry via Service Bus | Durable, observable, human-controlled |
| Version gap handling | API fallback + `IMemoryCache` (60s) | Fills missing data, avoids external API hammering |
| Hydration step execution | `Task.WhenAll` (parallel) | Latency bounded by slowest step, not sum of all steps |
| Observability | `ILogger` structured logs + `TelemetryClient` for dependencies only | Cost-efficient, fully queryable in KQL |
| Audit retention | Per-document TTL (180 days default) | Flexibility to extend individual records for compliance disputes |
| Batch error behaviour | Continue on transient/permanent, stop on host failure | Maximises throughput, does not sacrifice the batch for one bad message |
| Permanent failures | Dead-letter immediately, no retry | Malformed or rule-violating messages will never succeed — retrying wastes RUs |
| Transient failures | Retry queue (Service Bus) up to 3 attempts | Same retry flow as amendment-before-onboarding race condition |
| In-invocation resilience | Polly standard resilience handler + Cosmos SDK retry | Exhausts fast retries before surfacing to batch handler |
| Entity identity key | `partyId` (body + Kafka partition key) | Cosmos `id` and partition key; all messages for same entity land on same partition in order |
| Document model | One document per customer, all collections embedded | Cheap point reads, no joins, downstream APIs get full picture in one query |
| Collection item identity | Stable IDs (`addressId`, `phoneId`, `emailId`, `operationId`) | Single-field lookup in amendment `parameters` — no composite key matching |
| Tax ID storage | `TaxIdType` enum + masked `TaxId` (last 4 digits) | SSN and EIN are mutually exclusive; full value never written to Cosmos |
| Phone number format | E.164 (`+15125550001`) | Consistent format eliminates formatting logic in downstream APIs |
| Preferred item flag | `isPreferred` bool on each collection item | Uniform pattern across addresses, phones, and emails; validator enforces only one preferred per collection |
| Audit/trace identity key | `messageId` (Kafka header) | Idempotency store key, audit records, log scope — never used as business key |
| Kafka context capture | `KafkaMessageContext` at entry point | Topic, partition, offset, receivedAt flow through pipeline, logs, audit docs, and Service Bus messages |

---

## Error handling strategy

### Error classification

Every exception is classified into one of three categories that drive all handling decisions downstream:

```csharp
public enum ErrorCategory
{
    Transient,   // safe to retry — network, timeout, ETag conflict, throttling
    Permanent,   // never worth retrying — malformed payload, business rule violation
    Unknown      // default — treat as transient conservatively
}

public class ErrorClassifier : IErrorClassifier
{
    public ErrorCategory Classify(Exception ex) => ex switch
    {
        HttpRequestException http
            when IsTransientStatus(http)              => ErrorCategory.Transient,
        CosmosException cosmos
            when cosmos.StatusCode ==
                 HttpStatusCode.TooManyRequests        => ErrorCategory.Transient,
        CosmosException cosmos
            when cosmos.StatusCode ==
                 HttpStatusCode.PreconditionFailed     => ErrorCategory.Transient,
        TaskCanceledException                          => ErrorCategory.Transient,
        TimeoutException                               => ErrorCategory.Transient,
        OperationCanceledException                     => ErrorCategory.Transient,

        JsonException                                  => ErrorCategory.Permanent,
        MessageValidationException                     => ErrorCategory.Permanent,
        BusinessRuleViolationException                 => ErrorCategory.Permanent,
        CosmosException cosmos
            when cosmos.StatusCode ==
                 HttpStatusCode.BadRequest             => ErrorCategory.Permanent,

        _                                              => ErrorCategory.Unknown
    };

    private static bool IsTransientStatus(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.TooManyRequests;
}
```

### Batch behaviour by error type

Each message in a batch is processed independently. A failure on one message does not stop the rest of the batch — unless the exception is a host-level failure (`OutOfMemoryException` etc.) which is rethrown to let the Functions runtime handle the restart.

```csharp
foreach (var message in messages)
{
    try
    {
        await _pipeline.ProcessAsync(message);
    }
    catch (Exception ex)
    {
        var category = _errorClassifier.Classify(ex);

        _logger.LogError(ex,
            "MessageProcessingFailed {MessageId} {EntityId} {Category} {ExceptionType}",
            messageId, entityId, category, ex.GetType().Name);

        switch (category)
        {
            case ErrorCategory.Permanent:
                await _deadLetterService.SendAsync(message, ex.Message, attempt: 1);
                break;

            case ErrorCategory.Transient:
            case ErrorCategory.Unknown:
                await _retryService.EnqueueAsync(message, attemptCount: 1);
                break;
        }
        // Continue to next message in batch
    }
}
```

### Error handling matrix

| Error type | Batch behaviour | Retry | Destination | Log level |
|---|---|---|---|---|
| `JsonException` / deserialization | Continue | No | Dead-letter immediately | Error |
| `MessageValidationException` | Continue | No | Dead-letter immediately | Error |
| `BusinessRuleViolationException` | Continue | No | Dead-letter immediately | Error |
| `EnrichmentStepException` (transient) | Continue | Yes | Retry → DLQ after 3 | Warning |
| `CosmosException` 429 | Continue | Yes (SDK auto-retry first) | Retry queue if SDK exhausted | Warning |
| `CosmosException` ETag conflict | Continue | Yes | Retry → DLQ after 3 | Warning |
| `TaskCanceledException` / timeout | Continue | Yes | Retry → DLQ after 3 | Warning |
| `OutOfMemoryException` / host failure | **Stop batch** | Runtime restart | N/A | Critical |

### Custom exception hierarchy

All domain exceptions carry `EntityId` and `MessageId` for structured, queryable log context:

```csharp
public abstract class FunctionAppException : Exception
{
    public string EntityId  { get; }
    public string MessageId { get; }

    protected FunctionAppException(
        string message, string entityId, string messageId, Exception inner = null)
        : base(message, inner)
    {
        EntityId  = entityId;
        MessageId = messageId;
    }
}

public class MessageValidationException : FunctionAppException { ... }
public class BusinessRuleViolationException : FunctionAppException
{
    public string RuleName { get; }
}
public class EnrichmentStepException : FunctionAppException
{
    public string StepName { get; }
}
```

### Resilience on outbound calls

Polly (`Microsoft.Extensions.Http.Resilience`) handles transient HTTP retries **within the invocation** before exceptions propagate to the batch handler. By the time an exception reaches the handler, in-invocation retries are already exhausted:

```csharp
services.AddHttpClient<IEntityApiClient, EntityApiClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.Timeout.Timeout = TimeSpan.FromSeconds(5);
    });
```

Cosmos SDK throttling retries are configured at client registration:

```csharp
new CosmosClientBuilder(connectionString)
    .WithThrottlingRetryOptions(
        maxRetryWaitTimeOnThrottledRequests: TimeSpan.FromSeconds(10),
        maxRetryAttemptsOnThrottledRequests: 5)
    .Build();
```

### Batch summary logging

One summary log entry per batch keeps log volume manageable at high throughput:

```csharp
_logger.LogWarning(
    "BatchCompleted total={Total} succeeded={Succeeded} " +
    "transientFailures={Transient} permanentFailures={Permanent}",
    total, succeeded, transient, permanent);
```

### Guiding principle

No component below the function entry point catches and swallows exceptions silently. Individual steps throw typed exceptions; the top-level batch handler is the single place that decides what to do with them. Polly and the Cosmos SDK are the only exceptions — they handle their own transient retries transparently before surfacing to the handler.

---

## Amendment conditional logic

### Payload structure

Amendment messages follow an operation-based patch model. Each message carries a `partyId`, an `eventType` of `"amend"`, and an `operationsPayload` array containing one or more discrete operations:

```json
{
    "partyId": "12345",
    "eventType": "amend",
    "amendPayload": {
        "externalCustomerNo": "12345",
        "effectiveDate": "2024-07-01",
        "operationsPayload": [
            {
                "operationType": "add",
                "parameters": {},
                "operationDetails": {
                    "accountNumber": "9876543210",
                    "accountType": "savings",
                    "bankName": "ABC Bank"
                }
            },
            {
                "operationType": "remove",
                "parameters": {
                    "accountNumber": "9876543210",
                    "accountType": "savings"
                },
                "operationDetails": {}
            },
            {
                "operationType": "update",
                "parameters": {
                    "accountNumber": "9876543210",
                    "accountType": "savings"
                },
                "operationDetails": {
                    "bankName": "DEF Bank"
                }
            }
        ]
    }
}
```

`parameters` identifies the existing record to act on (used by `remove` and `update`). `operationDetails` contains the data to write.

### C# models

```csharp
public record AmendmentMessage
{
    public string PartyId        { get; init; }
    public string EventType      { get; init; }
    public AmendPayload AmendPayload { get; init; }
}

public record AmendPayload
{
    public string ExternalCustomerNo { get; init; }
    public string EffectiveDate      { get; init; }
    public List<Operation> OperationsPayload { get; init; }
}

public record Operation
{
    public string OperationType                    { get; init; }
    public Dictionary<string, object> Parameters   { get; init; }
    public Dictionary<string, object> OperationDetails { get; init; }
}
```

### Operation handler interface

Each operation type (`add`, `remove`, `update`) has its own handler registered in DI:

```csharp
public interface IOperationHandler
{
    string OperationType { get; }
    ValidationResult Validate(Operation operation, EnrichedEntity storedEntity);
    EnrichedEntity Apply(Operation operation, EnrichedEntity storedEntity);
}

services.AddScoped<IOperationHandler, AddOperationHandler>();
services.AddScoped<IOperationHandler, RemoveOperationHandler>();
services.AddScoped<IOperationHandler, UpdateOperationHandler>();
```

### Validation rules per operation type

| Operation | Rule | Failure reason |
|---|---|---|
| `add` | Target must NOT already exist (matched on `operationDetails` identity fields) | `DuplicateRecord` |
| `remove` | Target must exist (matched via `parameters`) | `RecordNotFound` |
| `update` | Target must exist (matched via `parameters`) | `RecordNotFound` |
| `update` | `operationDetails` must differ from current record | No-op — skip silently |
| any | Unknown `operationType` | `UnknownOperationType` |

### Two-pass orchestration — validate all, then apply all

All operations are validated before any are applied. A single violation rejects the entire message — no partial writes:

```csharp
// Pass 1 — validate all, collect all violations
for (int i = 0; i < operations.Count; i++)
{
    var result = handler.Validate(op, storedEntity);
    if (result.IsRejected) violations.Add((i, result));
    if (result.IsNoOp)     noOps.Add(i);
}

// Any violation → BusinessRuleViolationException → dead-letter (permanent failure)
if (violations.Any()) throw new BusinessRuleViolationException(...);

// All no-ops → log and ack, no Cosmos write
if (noOps.Count == operations.Count) return AmendmentResult.NoOp;

// Pass 2 — apply all valid non-no-op operations in sequence
foreach (var op in operations.Where((_, i) => !noOps.Contains(i)))
    updatedEntity = handler.Apply(op, updatedEntity);

// Pass 3 — ETag-gated upsert
await _cosmosWriter.UpsertWithETagAsync(updatedEntity, storedEntity.ETag);
```

### Decision flow

```
Incoming amendment message
    │
    ├─ Deserialize → AmendmentMessage
    │
    ├─ For each operation (validate all first):
    │       ├─ add:    target must NOT exist           → violation if duplicate
    │       ├─ remove: target must exist (by params)   → violation if not found
    │       └─ update: target must exist (by params)   → violation if not found
    │                  operationDetails must differ    → no-op if identical
    │
    ├─ Any violations?    → BusinessRuleViolationException → dead-letter
    │
    ├─ All no-ops?        → log + ack, no write
    │
    ├─ Apply all valid operations in sequence
    │
    └─ ETag upsert
            ├─ Success        → audit flush, ack
            └─ ETag conflict  → retry queue
```

---

## Local development setup

### Tooling

Podman Desktop is used as the local container runtime. It supports Docker Compose via `podman-compose`. Function Apps run directly on the host via `func start` — Podman exposes all service ports to `localhost` so `local.settings.json` connection strings resolve correctly without any extra networking config.

**Prerequisites:**
- Podman Desktop — https://podman.io/desktop
- podman-compose — `pip3 install podman-compose`
- Azure Functions Core Tools — `npm install -g azure-functions-core-tools@4`

### Services

| Service | Image | Purpose |
|---|---|---|
| Zookeeper | `confluentinc/cp-zookeeper:7.6.0` | Required by Kafka |
| Kafka | `confluentinc/cp-kafka:7.6.0` | Message broker for all topics |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-latest` | Required by Service Bus emulator |
| Cosmos DB emulator | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` | Local Cosmos SQL API — vnext-preview required for ARM64 (Apple Silicon) |
| Service Bus emulator | `mcr.microsoft.com/azure-messaging/servicebus-emulator` | Retry and dead-letter queues |
| Azurite | `mcr.microsoft.com/azure-storage/azurite` | Required by Azure Functions runtime |

### Project structure

```
local-env/
├── docker-compose.yml
└── Config.json          # Service Bus queue configuration — must be named Config.json
```

### docker-compose.yml

```yaml
version: '3.8'

services:

  zookeeper:
    image: confluentinc/cp-zookeeper:7.6.0
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"

  kafka:
    image: confluentinc/cp-kafka:7.6.0
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"

  cosmos:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview
    platform: linux/amd64   # required on Apple Silicon — runs via Rosetta
    ports:
      - "8081:8081"
    environment:
      PROTOCOL: https
    mem_limit: 2g

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "ServiceBus123!"
    ports:
      - "1433:1433"

  servicebus:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    depends_on:
      - sqlserver
    ports:
      - "5672:5672"
      - "5300:5300"
    environment:
      ACCEPT_EULA: "Y"
      SQL_SERVER: sqlserver
      MSSQL_SA_PASSWORD: "ServiceBus123!"
    volumes:
      - ./Config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:Z

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - "10000:10000"
      - "10001:10001"
      - "10002:10002"
    command: azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0
```

### Config.json (Service Bus queue definitions)

Must be named exactly `Config.json` — the Service Bus emulator looks for this filename specifically:

```json
{
  "UserConfig": {
    "Namespaces": [
      {
        "Name": "local-namespace",
        "Queues": [
          { "Name": "onboarding-retry",      "Properties": { "DeadLetterOnMessageExpiration": true } },
          { "Name": "onboarding-deadletter", "Properties": {} },
          { "Name": "amendment-retry",       "Properties": { "DeadLetterOnMessageExpiration": true } },
          { "Name": "amendment-deadletter",  "Properties": {} }
        ]
      }
    ]
  }
}
```

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "KafkaBootstrapServers": "localhost:9092",
    "KafkaTopic": "onboarding",
    "CosmosDbConnection": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMcZcaGosWgFZ4R==",
    "CosmosDbName": "your-db",
    "ServiceBusConnection": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<emulator-key>;UseDevelopmentEmulator=true;",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
  }
}
```

The Cosmos emulator uses a well-known fixed key — safe to commit in local settings. Leaving `APPLICATIONINSIGHTS_CONNECTION_STRING` empty redirects structured logs to the console.

### Cosmos emulator — TLS and database initialisation

The vnext-preview emulator uses a self-signed certificate and does not include the browser explorer UI. Browse data using the **Azure Databases** VS Code extension with the connection string above.

Disable TLS verification and auto-create all required databases and containers on startup in `Program.cs`, gated by environment so it never runs in Azure:

```csharp
if (builder.Environment.IsDevelopment())
{
    var devClient = new CosmosClientBuilder(connectionString)
        .WithHttpClientFactory(() => new HttpClient(
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }))
        .Build();

    var db = await devClient.CreateDatabaseIfNotExistsAsync("your-db");

    await db.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties("enriched-records",    "/partyId"));
    await db.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties("audit-metrics",       "/processedDate"));
    await db.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties("idempotency-records", "/messageId"));

    services.AddSingleton(devClient);
}
else
{
    services.AddSingleton(_ => new CosmosClientBuilder(connectionString).Build());
}
```

`CreateDatabaseIfNotExistsAsync` and `CreateContainerIfNotExistsAsync` are idempotent — safe to run on every startup.

### Daily workflow

```bash
# Start all services
cd local-env
podman-compose up -d

# Start Function Apps (separate terminal per app)
cd onboarding-func && func start
cd amendment-func  && func start

# Stop all services at end of day
podman-compose down

# Wipe all data and start fresh
podman-compose down -v
```

---

## CI/CD pipeline

### Platform
GitHub Actions — one repository per Function App, two workflows per repo.

### Repository structure

```
onboarding-func/
├── src/
│   └── Onboarding.Function/
│       ├── Onboarding.Function.csproj
│       ├── Functions/
│       ├── Pipeline/
│       └── host.json
├── tests/
│   └── Onboarding.Function.Tests/
│       └── Onboarding.Function.Tests.csproj
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── pr-checks.yml
└── local-env/
    ├── docker-compose.yml
    └── Config.json
```

### Workflows

| Workflow | Trigger | Responsibility |
|---|---|---|
| `ci.yml` | Push to `main` + manual trigger | Restore, build, test, publish versioned artifact |
| `pr-checks.yml` | Pull requests to `main` | Fast feedback — build and test only, no artifact |

### ci.yml

```yaml
name: CI

on:
  push:
    branches:
      - main
  workflow_dispatch:

env:
  DOTNET_VERSION: 8.x
  BUILD_CONFIGURATION: Release

jobs:
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration ${{ env.BUILD_CONFIGURATION }} --no-restore

      - name: Test
        run: dotnet test tests/**/*.Tests.csproj
          --configuration ${{ env.BUILD_CONFIGURATION }}
          --no-build
          --collect "XPlat Code Coverage"
          --results-directory ./coverage

      - name: Upload coverage report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./coverage

      - name: Publish Function App
        run: dotnet publish src/**/*.csproj
          --configuration ${{ env.BUILD_CONFIGURATION }}
          --output ./publish

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: function-app-${{ github.sha }}
          path: ./publish
          retention-days: 7
```

Artifact is named with the commit SHA — `function-app-abc1234` — so every build is traceable to its exact commit. 7-day retention keeps storage within GitHub's free tier.

### pr-checks.yml

```yaml
name: PR Checks

on:
  pull_request:
    branches:
      - main

env:
  DOTNET_VERSION: 8.x
  BUILD_CONFIGURATION: Release

jobs:
  pr-check:
    name: Build and Test
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration ${{ env.BUILD_CONFIGURATION }} --no-restore

      - name: Test
        run: dotnet test tests/**/*.Tests.csproj
          --configuration ${{ env.BUILD_CONFIGURATION }}
          --no-build
```

### Branch protection (GitHub Settings → Branches → main)

- Require a pull request before merging — no direct pushes to `main`
- Require status checks to pass — select `PR Checks / Build and Test`
- Require branches to be up to date before merging

### Amendment app

Identical workflow files — copy both YAML files into the amendment repo unchanged. Glob patterns (`src/**/*.csproj`, `tests/**/*.Tests.csproj`) resolve correctly in both repos.

### When Azure access is available

Add a deploy job to `ci.yml` — currently stubbed as a comment so nothing needs redesigning when access is granted:

```yaml
  # Uncomment when Azure access is available
  # deploy-dev:
  #   name: Deploy to Dev
  #   needs: build-and-test
  #   runs-on: ubuntu-latest
  #   environment: dev
  #   steps:
  #     - uses: actions/download-artifact@v4
  #       with:
  #         name: function-app-${{ github.sha }}
  #         path: ./publish
  #     - uses: Azure/functions-action@v1
  #       with:
  #         app-name: onboarding-func-dev
  #         package: ./publish
  #         publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

---

## DLQ admin tool

### Overview

A standalone C# minimal API with a plain HTML/JS frontend. Runs locally alongside the Function Apps via `dotnet run`. No framework, no separate deployment — designed to be demoed to stakeholders as part of the POC.

### Project structure

```
dlq-admin/
├── DlqAdmin.csproj
├── Program.cs
└── wwwroot/
    └── index.html     # UI — plain HTML/JS, no framework needed
```

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/dlq/{queueName}` | List up to 50 messages in the dead-letter queue (peek, non-destructive) |
| `POST` | `/api/dlq/{queueName}/requeue/{messageId}` | Approve — move to retry queue with `attemptCount` reset to 0 |
| `DELETE` | `/api/dlq/{queueName}/{messageId}` | Discard — permanently remove from dead-letter queue |

Queue names: `amendment-deadletter`, `onboarding-deadletter`.

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new ServiceBusClient(
    builder.Configuration["ServiceBusConnection"]));

var app = builder.Build();
app.UseStaticFiles();

app.MapGet("/api/dlq/{queueName}", async (
    string queueName, ServiceBusClient client) =>
{
    var receiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions
        {
            SubQueue    = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

    var messages = await receiver.PeekMessagesAsync(maxMessages: 50);

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
    var dlqReceiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

    var messages = await dlqReceiver.ReceiveMessagesAsync(maxMessages: 50);
    var target   = messages.FirstOrDefault(m => m.MessageId == messageId);

    if (target is null) return Results.NotFound();

    var retrySender = client.CreateSender(
        queueName.Replace("-deadletter", "-retry"));

    var requeued = new ServiceBusMessage(target.Body)
    {
        MessageId = target.MessageId,
        ApplicationProperties =
        {
            ["partyId"]      = target.ApplicationProperties["partyId"],
            ["topic"]        = target.ApplicationProperties["topic"],
            ["partition"]    = target.ApplicationProperties["partition"],
            ["offset"]       = target.ApplicationProperties["offset"],
            ["topicRole"]    = target.ApplicationProperties["topicRole"],
            ["attemptCount"] = 0   // reset — full fresh set of 3 retries
        }
    };

    await retrySender.SendMessageAsync(requeued);
    await dlqReceiver.CompleteMessageAsync(target);

    return Results.Ok(new { requeued = messageId });
});

app.MapDelete("/api/dlq/{queueName}/{messageId}", async (
    string queueName, string messageId, ServiceBusClient client) =>
{
    var dlqReceiver = client.CreateReceiver(queueName,
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

    var messages = await dlqReceiver.ReceiveMessagesAsync(maxMessages: 50);
    var target   = messages.FirstOrDefault(m => m.MessageId == messageId);

    if (target is null) return Results.NotFound();

    await dlqReceiver.DeadLetterMessageAsync(target);
    return Results.Ok(new { discarded = messageId });
});

app.Run();
```

### UI features

- Queue selector tabs — switch between `amendment-deadletter` and `onboarding-deadletter`
- Metric cards — total count, breakdown by failure reason (EntityNotFound, InvalidStateTransition, HydrationFailed)
- Per-message cards showing partyId, topic, partition, offset, reason, attempts, and dead-lettered timestamp
- Expandable payload viewer — shows the raw Kafka message body
- Requeue action — confirmation dialog before sending to retry queue with `attemptCount` reset to 0
- Discard action — confirmation dialog before permanent deletion

### Running locally

```bash
cd dlq-admin
dotnet run
# Open http://localhost:5000
```

The `ServiceBusConnection` in `appsettings.Development.json` points to the local Service Bus emulator — same connection string used by the Function Apps.

---

## Open items

These were identified during planning but not fully resolved — worth aligning on before implementation begins:


- **Schema versioning**: `schemaVersion` is on the enriched document but no migration strategy was defined for when the document shape changes.
- **Compliance retention window**: 180 days used as a placeholder — confirm exact regulatory requirement before setting TTL values.

### Out of scope for POC
- **External API authentication**: Not required for the POC — hardcoded or unauthenticated calls acceptable at this stage.
- **Configuration and secrets management**: Key Vault integration and secrets rotation not required for the POC.

### Resolved
- ~~**Error handling strategy**~~: Fully designed — error classification, batch behaviour, custom exception hierarchy, Polly resilience, and batch summary logging.
- ~~**Amendment conditional logic**~~: Fully designed — operation-based patch model (`add`, `remove`, `update`), per-operation validation rules, two-pass orchestration (validate all then apply all), no-op detection, and ETag-gated upsert.
- ~~**`messageId` location**~~: Confirmed in Kafka header across all topics. `partyId` confirmed as entity identity key (Cosmos `id` + partition key) and Kafka partition key. Full `KafkaMessageContext` (topic, partition, offset, receivedAt) captured at entry point and propagated via log scope and Service Bus message properties.
- ~~**Local development setup**~~: Fully defined — Podman Desktop with `podman-compose`, Confluent Kafka, Cosmos DB emulator, Service Bus emulator, and Azurite. TLS workaround documented for Cosmos emulator. Queue pre-configuration via `servicebus-config.json`.
- ~~**CI/CD pipeline**~~: Fully defined — GitHub Actions with two workflows per repo (`ci.yml` for builds and versioned artifacts on `main`, `pr-checks.yml` for PR gates). Branch protection on `main`. Azure deploy job stubbed as comment ready to uncomment when access is available.
- ~~**Admin tool for DLQ approval**~~: Fully designed and built — standalone C# minimal API with plain HTML/JS UI. Three endpoints (list, requeue, discard). Runs locally via `dotnet run`. Includes metric cards, payload viewer, confirmation dialogs, and queue selector for amendment and onboarding dead-letter queues.
