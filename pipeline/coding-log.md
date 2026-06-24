# Coding Log

## STORY-24 — Local development environment setup
Status: complete
Files produced:
- local-env/docker-compose.yml
- local-env/Config.json
- src/Onboarding.Function/local.settings.json
- src/Amendment.Function/local.settings.json
- README.md (updated — prerequisites and quick-start commands added)
Tests written: 0
Tests passing: N/A
Notes: >
  This story is purely infrastructure configuration — docker-compose.yml, Service Bus Config.json,
  and local.settings.json files. All acceptance criteria describe operational behaviour (services
  starting on specific ports, func connecting to localhost, logs to console) that cannot be
  unit-tested without a live container runtime. No unit tests are appropriate here; this is noted
  as a justified exception per the blocker-note provision in CLAUDE.md.
  docker-compose.yml: six services — zookeeper (2181), kafka (9092, cp-kafka:7.6.0),
  cosmos (8081, vnext-preview, linux/amd64, mem_limit=2g), sqlserver (1433, 2022-latest),
  servicebus (5672/5300, with Config.json volume mount :Z), azurite (10000–10002).
  Config.json: four queues — onboarding-retry (DeadLetterOnMessageExpiration:true),
  onboarding-deadletter, amendment-retry (DeadLetterOnMessageExpiration:true), amendment-deadletter.
  local.settings.json (both function apps): AzureWebJobsStorage=UseDevelopmentStorage=true,
  FUNCTIONS_WORKER_RUNTIME=dotnet-isolated, KafkaBootstrapServers=localhost:9092,
  CosmosDbConnection with well-known emulator key, APPLICATIONINSIGHTS_CONNECTION_STRING="".
  Dev-init (container auto-create) is implemented in CosmosDbExtensions.AddCosmosDb(), gated by
  IsDevelopment() — satisfies AC2 without any Program.cs change in this story.
  README.md updated with prerequisites (Podman Desktop, podman-compose, azure-functions-core-tools)
  and quick-start commands for both function apps.

## STORY-23 — DLQ admin tool — UI (iteration 2 fix)
Status: complete
Files produced:
- src/DlqAdmin/Program.cs (updated — added app.UseDefaultFiles() before app.UseStaticFiles())
- src/DlqAdmin/wwwroot/index.html
- tests/DlqAdmin.Tests/UI/UiTests.cs
Tests written: 7
Tests passing: 7 (14 DlqAdmin.Tests total; 117 Shared.Models.Tests; 48 Onboarding.Function.Tests; 79 Amendment.Function.Tests — all unchanged)
Fix applied (iteration 2): `togglePayload` condition corrected from `=== 'none'` to `=== 'block'` so the first click on Expand reveals the payload immediately. The inline `style` attribute starts as `''`, not `'none'`, so checking for `'block'` is the correct sentinel for the open state.
Notes: >
  wwwroot/index.html is plain HTML with inline JavaScript; no npm, no framework, no build step.
  Queue selector: two buttons (onboarding-deadletter / amendment-deadletter); clicking re-fetches
  messages via fetch('/api/dlq/{queueName}') and re-renders both the message list and metric cards.
  Message cards display partyId, topic, partition, offset, reason, attempts, deadLetteredAt from
  the API response. Expand button toggles a <pre class="payload"> element with the raw body.
  Requeue: confirm() dialog → fetch POST /api/dlq/{queueName}/requeue/{messageId} → loadMessages().
  Discard: confirm() dialog → fetch DELETE /api/dlq/{queueName}/{messageId} → loadMessages().
  Metric cards: count total messages; count per-reason for EntityNotFound, InvalidStateTransition,
  HydrationFailed by filtering m.reason from the API response.
  Program.cs updated: app.UseDefaultFiles() added before app.UseStaticFiles() so GET "/" serves
  index.html automatically in the browser; existing STORY-22 API tests are unaffected.
  Integration tests use WebApplicationFactory<Program>; wwwroot/index.html is copied to the test
  output directory via project reference (Microsoft.NET.Sdk.Web content inclusion) and served at
  /index.html by UseStaticFiles(). Tests assert HTTP 200 text/html and verify all AC-required
  strings are present in the HTML content.
  All 258 pre-existing tests (117+48+79+14) pass unchanged.

## STORY-22 — DLQ admin tool — API
Status: complete
Files produced:
- src/DlqAdmin/DlqAdmin.csproj
- src/DlqAdmin/Program.cs
- src/DlqAdmin/appsettings.json
- src/DlqAdmin/appsettings.Development.json
- tests/DlqAdmin.Tests/DlqAdmin.Tests.csproj
- tests/DlqAdmin.Tests/Api/DlqApiTests.cs
Tests written: 7
Tests passing: 7 (7 DlqAdmin.Tests; 117 Shared.Models.Tests; 48 Onboarding.Function.Tests; 79 Amendment.Function.Tests — all unchanged)
Notes: >
  DlqAdmin is a Microsoft.NET.Sdk.Web minimal API project targeting net10.0.
  ServiceBusClient registered as a lazy singleton factory so WebApplicationFactory tests can
  replace it before Build() — avoids ArgumentNullException when ServiceBusConnection is absent.
  GET: CreateReceiver(SubQueue=DeadLetter, PeekLock) + PeekMessagesAsync(50) — non-destructive.
  POST (requeue): ReceiveMessagesAsync, find by MessageId, build new ServiceBusMessage with
  attemptCount=0, send to {queueName}.Replace("-deadletter","-retry"), CompleteMessageAsync.
  DELETE (discard): ReceiveMessagesAsync, find by MessageId, CompleteMessageAsync to remove.
  Both POST and DELETE return 404 when MessageId not found in the received batch (AC3).
  ApplicationProperties set via indexer (not object-initializer block) because
  ServiceBusMessage.ApplicationProperties has no setter.
  ServiceBusModelFactory.ServiceBusReceivedMessage in Azure.Messaging.ServiceBus 7.19.0 does not
  expose deadLetterReason as a parameter; DeadLetterReason is null for plain-queue messages —
  test verifies the response key is present, not its exact value.
  app.UseStaticFiles() wired in Program.cs; wwwroot/ not created here (STORY-23 responsibility).
  public partial class Program {} enables WebApplicationFactory<Program> in the test project.
  All 244 pre-existing tests (117+48+79) pass unchanged.


## STORY-21 — Audit service — Cosmos flush
Status: complete
Files produced:
- src/Shared.Models/Audit/AuditService.cs (updated — added TelemetryClient and AuditMetricsContainer constructor params; added CreateItemAsync call with 409 Conflict suppression)
- tests/Shared.Models.Tests/Audit/AuditServiceTests.cs (updated — refactored to Build() helper with Moq; added 6 STORY-21 tests)
Tests written: 6
Tests passing: 6 (117 Shared.Models.Tests total; 48 Onboarding.Function.Tests; 79 Amendment.Function.Tests)
Notes: >
  AuditService constructor expanded from (ILogger) to (ILogger, TelemetryClient, AuditMetricsContainer)
  matching the architecture-recap.md signature. TelemetryClient is injected per spec but not actively
  used in FlushAsync (reserved for future dependency tracking, per STORY-20 architecture guidance).
  FlushAsync now: (1) emits LogInformation with exact MessageProcessed template, then (2) calls
  CreateItemAsync(record.ToCosmosDocument(), new PartitionKey(record.ProcessedDate)) on audit-metrics.
  409 Conflict is caught, logged as LogWarning with AuditRecordAlreadyExists template carrying MessageId,
  and NOT rethrown — duplicate flush is not a pipeline failure (AC4).
  AuditCosmosDocument.Ttl defaults to 15552000 (180 days) at the model level (STORY-2) — all documents
  automatically carry the correct TTL value without additional wiring in FlushAsync (AC2).
  Existing STORY-20 tests updated to supply the two new constructor args via a Build() helper that
  creates Mock<Container> (Moq), NoopChannel-backed TelemetryClient, and FakeLogger; all 4 prior
  tests continue to pass with the refactored build pattern.
  FakeLogger upgraded to also capture WarningMessages so AC4 warning log can be asserted.
  DI registration (services.AddScoped<IAuditService, AuditService>() in AddSharedServices()) is
  unchanged — the test verifies ServiceDescriptor without resolving the service, so no TelemetryClient
  DI binding is needed in the shared DI extension.
  All 117 Shared.Models.Tests pass (111 pre-existing + 6 new).
  All 48 Onboarding.Function.Tests pass (unchanged).
  All 79 Amendment.Function.Tests pass (unchanged).

## STORY-20 — Audit service — ILogger structured logging
Status: complete
Files produced:
- src/Shared.Models/Audit/AuditService.cs
- src/Shared.Models/DependencyInjection/ServiceCollectionExtensions.cs (updated — added IAuditService scoped registration)
- src/Amendment.Function/DependencyInjection/AmendmentServiceExtensions.cs (updated — removed NullAuditService stub)
- tests/Shared.Models.Tests/Audit/AuditServiceTests.cs
Tests written: 4
Tests passing: 4 (111 Shared.Models.Tests total; 79 Amendment.Function.Tests; 48 Onboarding.Function.Tests)
Notes: >
  AuditService (sealed) placed in Shared.Models/Audit/ so it is accessible to both
  onboarding and amendment function apps without circular references.
  FlushAsync emits LogInformation with exact template from architecture doc:
  "MessageProcessed {MessageId} {EntityId} {TopicRole} {StepsApplied} {WasApiFallback} {TotalDurationMs}ms"
  where EntityId = record.PartyId and StepsApplied = string.Join(",", record.Hydration.StepsApplied).
  In STORY-21 the constructor will gain AuditMetricsContainer and TelemetryClient parameters
  and the Cosmos flush will be added — the STORY-20 constructor (ILogger only) is intentionally minimal.
  IAuditService registered as Scoped in AddSharedServices() — consistent with IdempotencyService
  pattern; both apps call AddSharedServices() in Program.cs so both resolve AuditService.
  NullAuditService stub removed from AmendmentServiceExtensions — superseded by the real registration.
  AC2 (VersionGapDetected), AC3 (DuplicateMessageSkipped), AC4 (MessageDeadLettered) are satisfied
  by existing implementations in VersionGapDetector, AmendmentKafkaFunction, and DeadLetterService
  respectively — tests for those ACs already existed from STORY-13, STORY-14, and STORY-18.
  All 111 Shared.Models.Tests pass (107 pre-existing + 4 new).
  All 79 Amendment.Function.Tests pass (unchanged).
  All 48 Onboarding.Function.Tests pass (unchanged).

## STORY-19 — Batch error handling
Status: complete
Files produced:
- tests/Onboarding.Function.Tests/Functions/OnboardingKafkaFunctionTests.cs (updated — fixed FakeRetryService compile error; added 2 STORY-19 tests)
- tests/Amendment.Function.Tests/Functions/AmendmentKafkaFunctionTests.cs (updated — added 2 STORY-19 tests)
Tests written: 4
Tests passing: 4 (48 Onboarding.Function.Tests total; 79 Amendment.Function.Tests total)
Notes: >
  No new production code required — the batch error handling loop in both OnboardingKafkaFunction
  and AmendmentKafkaFunction was already fully implemented in STORY-8 and STORY-14/18.
  STORY-19 deliverables:
  (1) Fixed FakeRetryService in OnboardingKafkaFunctionTests.cs — missing bool isImmediate = false
  parameter (added to IRetryService in STORY-18) caused a compile error that blocked all 46
  Onboarding tests. Fixed by adding the optional parameter to the fake's EnqueueAsync signature.
  (2) Added ProcessBatchAsync_FiveMessageBatch_ThirdMessageThrowsJsonException_FourSucceedAndOneDeadLettered
  to both OnboardingKafkaFunctionTests and AmendmentKafkaFunctionTests — tests the exact 5-message
  scenario from AC1: message 3 throws JsonException; messages 1, 2, 4, 5 succeed; BatchCompleted
  log shows total=5, succeeded=4, permanentFailures=1, transientFailures=0.
  (3) Added ProcessBatchAsync_FailedMessage_LogsMessageProcessingFailedWithStructuredProperties
  to both test files — verifies the per-message error log entry carries MessageId, EntityId per
  the STORY-19 technical notes.
  All 107 Shared.Models.Tests pass. 48 Onboarding.Function.Tests pass (46 pre-existing + 2 new).
  79 Amendment.Function.Tests pass (77 pre-existing + 2 new).


## STORY-18 — Dead-letter and retry flow — Service Bus integration
Status: complete
Files produced:
- src/Amendment.Function/ServiceBus/RetryService.cs
- src/Amendment.Function/ServiceBus/DeadLetterService.cs
- src/Shared.Models/Contracts/IRetryService.cs (updated — added bool isImmediate = false optional parameter)
- src/Amendment.Function/DependencyInjection/AmendmentServiceExtensions.cs (updated — replaced NullRetryService/NullDeadLetterService stubs with real RetryService and DeadLetterService registrations)
- src/Amendment.Function/Functions/AmendmentKafkaFunction.cs (updated — added CosmosException PreconditionFailed detection to pass isImmediate: true to retry service)
- tests/Amendment.Function.Tests/ServiceBus/RetryServiceTests.cs
- tests/Amendment.Function.Tests/ServiceBus/DeadLetterServiceTests.cs
- tests/Amendment.Function.Tests/Functions/AmendmentKafkaFunctionTests.cs (updated — FakeRetryService tracks IsImmediate; new ETag conflict test)
- tests/Amendment.Function.Tests/Orchestrator/AmendmentOrchestratorTests.cs (updated — TrackingRetryService matches new interface signature)
Tests written: 14
Tests passing: 77 (Amendment.Function.Tests total; 14 new + 63 carried forward)
Notes: >
  RetryService (sealed): injects ServiceBusClient (singleton) and ILogger<RetryService>.
  EnqueueAsync checks nextAttemptCount (attemptCount + 1) against MaxAttempts = 3.
  Below max: creates sender for {topicRole}-retry, builds ServiceBusMessage with all 7
  ApplicationProperties (messageId, partyId, topic, partition, offset, topicRole, attemptCount),
  sets ScheduledEnqueueTime = UtcNow + 30s normally or UtcNow when isImmediate = true.
  At or above max: routes to {topicRole}-deadletter queue and emits LogError
  "MessageDeadLettered {MessageId} {EntityId} MaxRetriesExhausted attempt={finalAttemptCount}".
  DeadLetterService (sealed): injects ServiceBusClient (singleton) and ILogger<DeadLetterService>.
  SendAsync always sends to {topicRole}-deadletter with all 7 ApplicationProperties and emits
  LogError "MessageDeadLettered {MessageId} {EntityId} {Reason} attempt={Attempt}".
  IRetryService interface updated with bool isImmediate = false optional parameter (backward-
  compatible: all existing callers at call sites compile unchanged; implementing classes updated).
  AmendmentKafkaFunction.ProcessBatchAsync updated to detect CosmosException(PreconditionFailed)
  and pass isImmediate: true so ETag conflicts enqueue with ScheduledEnqueueTime = UtcNow (no
  30-second delay) per AC5. Using Azure.Cosmos and System.Net imports added to function.
  NullRetryService and NullDeadLetterService stubs removed from AmendmentServiceExtensions —
  real implementations registered as Scoped. NullAuditService stub retained for STORY-21.
  All 107 Shared.Models.Tests pass. All 46 Onboarding.Function.Tests pass.
  All 77 Amendment.Function.Tests pass (14 new + 63 carried forward).


## STORY-17 — Amendment conditional logic — orchestrator and ETag-gated upsert
Status: complete
Files produced:
- src/Amendment.Function/Orchestrator/AmendmentOrchestrator.cs
- src/Amendment.Function/Pipeline/AmendmentPipeline.cs
- src/Amendment.Function/DependencyInjection/AmendmentServiceExtensions.cs (updated — IAmendmentOrchestrator and AmendmentPipeline registered; NullAmendmentPipeline stub removed)
- tests/Amendment.Function.Tests/Orchestrator/AmendmentOrchestratorTests.cs
- tests/Amendment.Function.Tests/Amendment.Function.Tests.csproj (updated — added Moq 4.20.72)
Tests written: 8
Tests passing: 63 (Amendment.Function.Tests total; 8 new + 55 carried forward)
Notes: >
  AmendmentOrchestrator (sealed, public) placed in Amendment.Function/Orchestrator/.
  Three-pass design: Pass 1 validates all ops; violations throw BusinessRuleViolationException
  with first violation Reason as RuleName; noOps accumulated for skip in Pass 2.
  Pass 2 applies non-no-op ops sequentially; updatedEntity patched with Version+1,
  LastUpdatedAt, LastUpdatedBy="amendment-app", and AuditSummary from KafkaMessageContext.
  Pass 3 ETag-gated UpsertItemAsync using response.ETag from ReadItemAsync.
  NotFound during Read caught explicitly: IRetryService.EnqueueAsync called and IsNoOp=true
  returned without rethrowing. ETag 412 not caught — propagates to batch handler (Transient).
  After upsert: IIdempotencyService.MarkProcessedAsync then IAuditService.FlushAsync.
  AmendmentKafkaFunction (STORY-14) also calls MarkProcessedAsync after ProcessAsync — the
  second call hits 409 Conflict which is swallowed in IdempotencyService (harmless by design).
  AmendmentPipeline (internal sealed) replaces NullAmendmentPipeline stub: deserializes
  rawPayload and delegates to IAmendmentOrchestrator.OrchestrateAsync.
  Moq 4.20.72 added to Amendment.Function.Tests for mocking abstract Container class.
  All 107 Shared.Models.Tests pass. All 46 Onboarding.Function.Tests pass.
  All 63 Amendment.Function.Tests pass (8 new + 55 carried forward).


## STORY-16 — Amendment conditional logic — validator
Status: complete
Files produced:
- src/Amendment.Function/Handlers/AddOperationHandler.cs (updated — real Validate logic replacing stub)
- src/Amendment.Function/Handlers/RemoveOperationHandler.cs (updated — real Validate logic replacing stub)
- src/Amendment.Function/Handlers/UpdateOperationHandler.cs (updated — real Validate logic replacing stub; added IsNoOp helpers)
- src/Amendment.Function/Handlers/OperationHandlerResolver.cs (new — static Resolve method throws BusinessRuleViolationException for unknown OperationType)
- tests/Amendment.Function.Tests/Handlers/ValidatorTests.cs (new — 21 tests covering all ACs)
- tests/Amendment.Function.Tests/Handlers/OperationHandlerTests.cs (updated — two stale stub tests replaced with valid-scenario tests)
Tests written: 21
Tests passing: 55 (Amendment.Function.Tests total; 21 new + 34 carried forward)
Notes: >
  AddOperationHandler.Validate: checks stable ID from operationDetails against the stored
  collection for DuplicateRecord; checks isPreferred conflict against the same collection.
  RemoveOperationHandler.Validate: checks stable ID from parameters against the stored
  collection for RecordNotFound — returns RecordNotFound immediately if item absent.
  UpdateOperationHandler.Validate: checks RecordNotFound first; then IsPreferredConflict for
  addresses/phones/emails when update sets isPreferred=true on a non-preferred item and another
  item in the same collection is already preferred; finally checks per-collection no-op helpers
  (IsAddressNoOp, IsPhoneNoOp, IsEmailNoOp, IsBankOperationNoOp) — if ALL specified
  operationDetails fields match the existing record, returns IsNoOp=true.
  OperationHandlerResolver (static class): Resolve(handlers, operationType, entityId, messageId)
  — throws BusinessRuleViolationException(RuleName="UnknownOperationType") when no handler
  matches; used by AmendmentOrchestrator in STORY-17.
  Two previously-stub Validate tests in OperationHandlerTests.cs updated to use entities that
  contain the referenced items, matching the now-real validation semantics.
  All 107 Shared.Models.Tests pass. All 46 Onboarding.Function.Tests pass. All 55
  Amendment.Function.Tests pass.

## STORY-15 — Amendment conditional logic — operation handlers
Status: complete
Files produced:
- src/Amendment.Function/Handlers/AddOperationHandler.cs
- src/Amendment.Function/Handlers/RemoveOperationHandler.cs
- src/Amendment.Function/Handlers/UpdateOperationHandler.cs
- src/Amendment.Function/DependencyInjection/AmendmentServiceExtensions.cs (updated — added IOperationHandler registrations for all three handlers)
- tests/Amendment.Function.Tests/Handlers/OperationHandlerTests.cs
Tests written: 23
Tests passing: 23
Notes: >
  AddOperationHandler (sealed): OperationType = "add" — determines target collection from
  which stable ID key is present in operationDetails (addressId → Addresses, phoneId →
  PhoneNumbers, emailId → EmailAddresses, operationId → BankOperations). New items get
  AddedAt = DateTimeOffset.UtcNow and AddedBy = "amendment-app". Existing collection items
  preserved via record `with` spread syntax.
  RemoveOperationHandler (sealed): OperationType = "remove" — stable ID looked up in
  parameters; removes matching item from collection, leaves all others unchanged.
  UpdateOperationHandler (sealed): OperationType = "update" — stable ID in parameters
  identifies target; operationDetails specifies only the fields to patch. Uses nullable-return
  helper TryGet/TryGetBool/TryGetEnum so missing keys fall back to the existing field value,
  ensuring non-specified fields are unchanged.
  Validate stubs on all three handlers return ValidationResult { IsRejected = false, IsNoOp =
  false } — full validation rules implemented in STORY-16.
  All three registered as IOperationHandler (Scoped) in AddAmendmentServices(). DI test
  resolves IEnumerable<IOperationHandler> and asserts "add", "remove", "update" types present.
  AC4 (no handler ambiguity) verified by asserting distinct OperationType count equals handler count.
  All 107 Shared.Models.Tests pass. All 46 Onboarding.Function.Tests pass.
  36 Amendment.Function.Tests pass (13 pre-existing + 23 new).

## STORY-14 — Amendment function app — Kafka trigger and hosting
Status: complete
Files produced:
- src/Amendment.Function/Amendment.Function.csproj
- src/Amendment.Function/Program.cs
- src/Amendment.Function/host.json
- src/Amendment.Function/local.settings.json
- src/Amendment.Function/Properties/AssemblyInfo.cs
- src/Amendment.Function/Functions/KafkaRawEvent.cs
- src/Amendment.Function/Functions/AmendmentKafkaFunction.cs
- src/Amendment.Function/Pipeline/IAmendmentPipeline.cs
- src/Amendment.Function/Pipeline/NullAmendmentPipeline.cs
- src/Amendment.Function/DependencyInjection/AmendmentServiceExtensions.cs
- tests/Amendment.Function.Tests/Amendment.Function.Tests.csproj
- tests/Amendment.Function.Tests/Functions/AmendmentKafkaFunctionTests.cs
Tests written: 13
Tests passing: 13
Notes: >
  AmendmentKafkaFunction (sealed) entry point sequence: (1) build KafkaMessageContext with
  TopicRole="amendment", (2) IsDuplicateAsync — if true log DuplicateMessageSkipped and
  skip (counted as succeeded), (3) call IAmendmentPipeline.ProcessAsync, (4) on success
  call IIdempotencyService.MarkProcessedAsync. OutOfMemoryException rethrows; all others
  classified by IErrorClassifier and routed to IDeadLetterService (Permanent) or
  IRetryService (Transient/Unknown).
  KafkaTrigger attribute has ConsumerGroup = "amendment-func" verified via dynamic reflection
  (attribute name string comparison rather than compile-time type) — avoids a test project
  compile dependency on the Kafka extension assembly; same approach that onboarding tests use
  by not testing the attribute at all.
  IAmendmentPipeline defined as a new interface (mirrors IOnboardingPipeline pattern) with
  NullAmendmentPipeline stub registered in AddAmendmentServices — concrete implementation
  backed by IAmendmentOrchestrator will be wired in STORY-17.
  AddAmendmentServices registers: IAmendmentPipeline → NullAmendmentPipeline (stub),
  IRetryService → NullRetryService (stub, STORY-18), IDeadLetterService → NullDeadLetterService
  (stub, STORY-18), IAuditService → NullAuditService (stub, STORY-21), ServiceBusClient
  (singleton factory from IConfiguration["ServiceBusConnection"]).
  IIdempotencyService, IVersionGapDetector, IEntityApiClient are already registered in
  AddSharedServices() and are NOT re-registered in AddAmendmentServices.
  host.json identical to onboarding-func: adaptive sampling, maxTelemetryItemsPerSecond=20,
  excludedTypes="Exception;Trace", enableDependencyTracking=true.
  All 107 Shared.Models.Tests pass. All 46 Onboarding.Function.Tests pass. 13 new tests pass.

## STORY-13 — Version gap detection and API fallback with caching
Status: complete
Files produced:
- src/Shared.Models/VersionGap/VersionGapDetector.cs
- src/Shared.Models/VersionGap/EntityApiClient.cs
- src/Shared.Models/DependencyInjection/ServiceCollectionExtensions.cs (updated — added IVersionGapDetector scoped, IEntityApiClient typed HttpClient with Polly, AddMemoryCache)
- src/Shared.Models/Shared.Models.csproj (updated — added Microsoft.ApplicationInsights 2.22.0, Microsoft.Extensions.Caching.Memory 9.0.0, Microsoft.Extensions.Http 9.0.0, Microsoft.Extensions.Http.Resilience 9.0.0)
- tests/Shared.Models.Tests/VersionGap/VersionGapDetectorTests.cs
- tests/Shared.Models.Tests/VersionGap/EntityApiClientTests.cs
- tests/Shared.Models.Tests/Shared.Models.Tests.csproj (updated — added Microsoft.ApplicationInsights 2.22.0, Microsoft.Extensions.Caching.Memory 9.0.0)
Tests written: 12
Tests passing: 12
Notes: >
  VersionGapDetector (sealed) placed in Shared.Models/VersionGap/ following the same
  pattern as IdempotencyService — accessible to Amendment.Function (STORY-14) without
  circular references.
  Gap formula: gap = incomingVersion - storedVersion; gap >= 2 triggers API fallback;
  gap < 2 returns messagePayloadData as-is.
  Cache key: "api-entity-{entityId}-v{incomingVersion}" with 60s absolute expiry via
  IMemoryCache. Second call with same key returns cached EntityData without HTTP call.
  TelemetryClient.TrackDependency called in a finally block so success=false is recorded
  even when GetEntityAtVersionAsync throws. DependencyTelemetry Name = "EntityApi.GetEntityAtVersion".
  VersionGapDetector.ResolveDataAsync returns EntityData only — the caller (STORY-14's
  AmendmentOrchestrator or pipeline) is responsible for setting WasApiFallback=true on
  HydrationContext based on whether gap >= 2 was detected.
  Story spec says options.Timeout.Timeout but the actual Microsoft.Extensions.Http.Resilience
  9.0.0 API uses options.AttemptTimeout.Timeout — used the correct API name.
  TelemetryClient tested via a custom CapturingChannel (ITelemetryChannel) that stores
  items synchronously; no extra package needed beyond Microsoft.ApplicationInsights.
  All 107 Shared.Models.Tests pass (95 pre-existing + 12 new).
  All 46 Onboarding.Function.Tests continue to pass.

## STORY-12 — Amendment idempotency check
Status: complete
Files produced:
- src/Shared.Models/Models/IdempotencyRecord.cs
- src/Shared.Models/Idempotency/IdempotencyService.cs
- src/Shared.Models/DependencyInjection/ServiceCollectionExtensions.cs (updated — added IIdempotencyService scoped registration)
- tests/Shared.Models.Tests/Idempotency/IdempotencyServiceTests.cs
- tests/Shared.Models.Tests/Shared.Models.Tests.csproj (updated — added Moq 4.20.72 for Container mocking)
Tests written: 7
Tests passing: 7
Notes: >
  IdempotencyService (sealed) placed in Shared.Models/Idempotency/ because it depends only
  on IdempotencyContainer (already in Shared.Models) and ILogger<T>. This makes it accessible
  to the Amendment.Function project created in STORY-14 without circular references.
  IsDuplicateAsync uses ReadItemAsync<IdempotencyRecord>(messageId, new PartitionKey(messageId));
  a successful read means duplicate (returns true + logs DuplicateMessageSkipped {MessageId});
  CosmosException(NotFound) means not a duplicate (returns false, no log).
  MarkProcessedAsync builds IdempotencyRecord with Id = MessageId = messageId and Ttl = 604800
  (7 days in seconds), calls CreateItemAsync with partition key messageId, and catches
  CosmosException(Conflict) as success — the expected race condition when two concurrent
  executions both try to mark the same message. Non-Conflict CosmosExceptions propagate.
  Moq 4.20.72 added to Shared.Models.Tests for mocking the abstract Container class.
  DI test verifies ServiceDescriptor (Scoped lifetime, IdempotencyService implementation type)
  rather than resolving via GetRequiredService — IdempotencyService requires IdempotencyContainer
  which is registered by AddCosmosDb() (not AddSharedServices()).
  All 95 Shared.Models.Tests pass (88 pre-existing + 7 new).
  All 46 Onboarding.Function.Tests continue to pass.

## STORY-11 — Onboarding pipeline — blind upsert to enriched-records
Status: complete
Files produced:
- src/Onboarding.Function/Pipeline/IOnboardingCosmosWriter.cs
- src/Onboarding.Function/Pipeline/OnboardingCosmosWriter.cs
- src/Onboarding.Function/Pipeline/OnboardingPipeline.cs
- src/Onboarding.Function/DependencyInjection/OnboardingServiceExtensions.cs (updated — added IOnboardingCosmosWriter and IOnboardingPipeline registrations)
- tests/Onboarding.Function.Tests/Pipeline/OnboardingCosmosWriterTests.cs
- tests/Onboarding.Function.Tests/Pipeline/OnboardingPipelineTests.cs
- tests/Onboarding.Function.Tests/Onboarding.Function.Tests.csproj (updated — added Moq 4.20.72 for Container mocking)
Tests written: 16
Tests passing: 16
Notes: >
  OnboardingCosmosWriter (sealed) injects EnrichedRecordsContainer, calls
  UpsertItemAsync(entity, new PartitionKey(entity.PartyId)) with no ItemRequestOptions
  — blind upsert by design, no ETag check.
  OnboardingPipeline (sealed) orchestrates: build HydrationContext (EntityId=PartyId,
  StoredVersion=0, IncomingVersion from JSON payload "version" field) → IHydrationPipeline
  .ExecuteAsync → IOutputAssembler.Assemble → IOnboardingCosmosWriter.WriteAsync →
  IAuditService.FlushAsync with AuditRecord (Outcome="success").
  AuditRecord is built inline with all required fields: KafkaContextInfo, VersionInfo,
  HydrationInfo (applied/skipped steps), RetryInfo (AttemptNumber=1, WasRetry=false).
  Moq 4.20.72 added to Onboarding.Function.Tests for mocking the abstract Container class.
  All other pipeline fakes remain hand-rolled (interfaces only need no-framework stubs).
  DI tests use ServiceDescriptor checks rather than GetRequiredService resolution since
  OnboardingCosmosWriter depends on EnrichedRecordsContainer which is registered by
  AddCosmosDb() (not AddOnboardingServices()).
  All 46 Onboarding.Function.Tests pass (30 from STORY-8/9/10 + 16 new).
  All 88 Shared.Models.Tests continue to pass.

## STORY-10 — Hydration pipeline — output assembler
Status: complete
Files produced:
- src/Onboarding.Function/Pipeline/OutputAssembler.cs
- src/Onboarding.Function/DependencyInjection/OnboardingServiceExtensions.cs (updated — added IOutputAssembler registration)
- tests/Onboarding.Function.Tests/Pipeline/OutputAssemblerTests.cs
Tests written: 11
Tests passing: 11
Notes: >
  OutputAssembler is sealed, implements IOutputAssembler, and is registered as
  services.AddScoped<IOutputAssembler, OutputAssembler>() in OnboardingServiceExtensions.
  Assemble() filters results to applied-only for LastHydrationSteps, sets LastUpdatedBy
  to "onboarding-app"/"amendment-app" based on TopicRole, and populates all AuditSummary
  fields from HydrationContext and applied EnrichmentResults.
  Gap noted: The story technical notes specify LastKafkaOffset = context.Offset but
  HydrationContext (defined in STORY-2) does not carry a Kafka offset field. LastKafkaOffset
  is hardcoded to 0 in this implementation; to propagate the real offset, KafkaOffset
  should be added to HydrationContext in a follow-up.
  MergeData is intentionally minimal — enrichment steps are stubs at this stage; concrete
  collection population (Addresses, PhoneNumbers, etc.) will be wired in STORY-11+ when
  step implementations are fleshed out. The method signature satisfies the interface contract.
  All 30 Onboarding.Function.Tests pass (19 from STORY-8/9 + 11 new).
  All 88 Shared.Models.Tests continue to pass.

## STORY-8 — Onboarding function app — Kafka trigger and hosting
Status: complete
Files produced:
- src/Onboarding.Function/Onboarding.Function.csproj
- src/Onboarding.Function/Program.cs
- src/Onboarding.Function/host.json
- src/Onboarding.Function/local.settings.json
- src/Onboarding.Function/Properties/AssemblyInfo.cs
- src/Onboarding.Function/Pipeline/IOnboardingPipeline.cs
- src/Onboarding.Function/Functions/KafkaRawEvent.cs
- src/Onboarding.Function/Functions/OnboardingKafkaFunction.cs
- src/Onboarding.Function/DependencyInjection/OnboardingServiceExtensions.cs
- tests/Onboarding.Function.Tests/Onboarding.Function.Tests.csproj
- tests/Onboarding.Function.Tests/Functions/OnboardingKafkaFunctionTests.cs
Tests written: 8
Tests passing: 8
Notes: >
  Azure Functions isolated worker project targeting net10.0 (consistent with Shared.Models).
  Packages used: Microsoft.Azure.Functions.Worker 2.52.0, Worker.Sdk 2.0.7,
  Worker.Extensions.Kafka 4.3.0, Worker.ApplicationInsights 2.51.0.
  The isolated-worker Kafka extension uses KafkaRecord (not KafkaEventData) with Value as
  byte[], and KafkaHeader with Key (string) + Value (byte[]) decoded via UTF-8.
  Batch loop extracted to internal ProcessBatchAsync(IEnumerable<KafkaRawEvent>) for
  testability; InternalsVisibleTo("Onboarding.Function.Tests") added via AssemblyInfo.cs.
  IOnboardingPipeline defined here (accepts KafkaMessageContext + rawPayload string)
  so implementations in STORY-11 can proceed without interface changes.
  AddOnboardingServices() is a stub — concrete registrations added in STORY-9, 10, 11.
  OutOfMemoryException rethrow tested; error routing (Permanent→DLQ, Transient→retry,
  Unknown→retry) and BatchCompleted log with accurate counters all tested.
  All 88 Shared.Models.Tests still pass after this story.

## STORY-3 — Custom exception hierarchy
Status: complete
Files produced:
- src/Shared.Models/Exceptions/FunctionAppException.cs
- src/Shared.Models/Exceptions/MessageValidationException.cs
- src/Shared.Models/Exceptions/BusinessRuleViolationException.cs
- src/Shared.Models/Exceptions/EnrichmentStepException.cs
- tests/Shared.Models.Tests/Exceptions/FunctionAppExceptionTests.cs
Tests written: 14
Tests passing: 14
Notes: >
  Exception hierarchy was implemented alongside STORY-2 (pipeline context models)
  since it has no dependencies and was needed immediately. All four classes are in
  the existing Shared.Models class library under the Exceptions/ folder.
  FunctionAppException is abstract; all three concrete types are sealed.
  BusinessRuleViolationException constructor adds `ruleName` before `inner`;
  EnrichmentStepException adds `stepName` before `inner` — both consistent with
  the base-class pattern. No DI registration required for exception types.
  All 49 Shared.Models.Tests pass (includes 14 exception-specific tests and 35
  from STORY-1 and STORY-2).

## STORY-1 — Shared enriched customer data models
Status: complete
Files produced:
- src/Shared.Models/Shared.Models.csproj
- src/Shared.Models/Models/Enums.cs
- src/Shared.Models/Models/EnrichedCustomer.cs
- tests/Shared.Models.Tests/Shared.Models.Tests.csproj
- tests/Shared.Models.Tests/Models/EnrichedCustomerTests.cs
Tests written: 17
Tests passing: 17
Notes: >
  All record types are sealed with init-only properties. Collection defaults use
  C# 12 collection expression syntax (`= []`). Runtime target is net10.0 (the
  installed SDK version) rather than net8.0 — code is fully compatible. No DI
  registration required for pure model/enum types; this project is a class
  library consumed by function app projects in later stories.

## STORY-5 -- Shared service interfaces, DI extension, and ValidationResult
Status: complete
Files produced:
- src/Shared.Models/Models/ValidationResult.cs
- src/Shared.Models/Models/AmendmentResult.cs
- src/Shared.Models/Contracts/IEnrichmentStep.cs
- src/Shared.Models/Contracts/IOperationHandler.cs
- src/Shared.Models/Contracts/IAuditService.cs
- src/Shared.Models/Contracts/IRetryService.cs
- src/Shared.Models/Contracts/IDeadLetterService.cs
- src/Shared.Models/Contracts/IHydrationPipeline.cs
- src/Shared.Models/Contracts/IOutputAssembler.cs
- src/Shared.Models/Contracts/IIdempotencyService.cs
- src/Shared.Models/Contracts/IAmendmentOrchestrator.cs
- src/Shared.Models/Contracts/IVersionGapDetector.cs
- src/Shared.Models/Contracts/IEntityApiClient.cs
- src/Shared.Models/DependencyInjection/ServiceCollectionExtensions.cs
- tests/Shared.Models.Tests/ServiceContracts/ServiceContractsTests.cs
Tests written: 8
Tests passing: 8
Notes: >
  All 11 service interfaces placed in src/Shared.Models/Contracts/ namespace.
  ValidationResult and AmendmentResult sealed records placed in Shared.Models.Models.
  ServiceCollectionExtensions.AddSharedServices() registers IErrorClassifier as singleton.
  Microsoft.Extensions.DependencyInjection.Abstractions 9.0.0 added to Shared.Models.csproj.
  Microsoft.Extensions.DependencyInjection 9.0.0 added to test project for DI integration tests.
  All 72 Shared.Models.Tests pass after this story (8 new + 64 pre-existing).

## STORY-4 — Error classification
Status: complete
Files produced:
- src/Shared.Models/Models/Enums.cs (ErrorCategory enum appended)
- src/Shared.Models/ErrorClassification/IErrorClassifier.cs
- src/Shared.Models/ErrorClassification/ErrorClassifier.cs
- tests/Shared.Models.Tests/ErrorClassification/ErrorClassifierTests.cs
Tests written: 14
Tests passing: 14
Notes: >
  ErrorCategory enum (Transient, Permanent, Unknown) appended to existing Enums.cs.
  IErrorClassifier interface and ErrorClassifier sealed class placed in a new
  ErrorClassification/ folder within Shared.Models.
  ErrorClassifier uses a single C# switch expression with pattern matching; no
  embedded if/else chains.
  Microsoft.Azure.Cosmos 3.46.0 and Newtonsoft.Json 13.0.3 added to Shared.Models.csproj
  — Newtonsoft.Json is required by the Cosmos SDK targets check.
  DI registration (services.AddSingleton<IErrorClassifier, ErrorClassifier>()) is deferred
  to STORY-5 which creates ServiceCollectionExtensions.AddSharedServices() — this is
  explicit in the STORY-5 story text and dependency graph.
  All 63 Shared.Models.Tests pass after this story (14 new + 49 pre-existing).

## STORY-6 — Cosmos DB client and container DI registration
Status: complete
Files produced:
- src/Shared.Models/CosmosDb/CosmosDbExtensions.cs
- src/Shared.Models/CosmosDb/EnrichedRecordsContainer.cs
- src/Shared.Models/CosmosDb/AuditMetricsContainer.cs
- src/Shared.Models/CosmosDb/IdempotencyContainer.cs
- tests/Shared.Models.Tests/CosmosDb/CosmosDbExtensionsTests.cs
Tests written: 7
Tests passing: 7
Notes: >
  CosmosDbExtensions.AddCosmosDb() reads CosmosDbConnection and CosmosDbName from
  IConfiguration; branches on environment.IsDevelopment() for TLS bypass path.
  Non-dev path: CosmosClientBuilder with WithThrottlingRetryOptions(10s, 5 retries)
  registered as singleton factory.
  Dev path: client built with HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
  then InitializeDevContainersAsync blocks synchronously (.GetAwaiter().GetResult()) to
  create all three containers via CreateContainerIfNotExistsAsync (idempotent).
  Container wrapper types (EnrichedRecordsContainer, AuditMetricsContainer,
  IdempotencyContainer) are sealed records wrapping Container; each registered as singleton
  factory resolving CosmosClient from DI.
  Partition keys: enriched-records=/partyId, audit-metrics=/processedDate,
  idempotency-records=/messageId — match architecture exactly.
  Microsoft.Extensions.Hosting.Abstractions 9.0.0 added to Shared.Models.csproj.
  Microsoft.Extensions.Configuration 9.0.0 and Microsoft.Extensions.Hosting.Abstractions
  9.0.0 added to test project.
  The well-known Cosmos emulator key in the architecture doc is NOT a valid base64 string
  (84 non-padding chars, 84 % 4 = 0, so the trailing == is invalid padding). Unit tests use
  Convert.ToBase64String(new byte[64]) to produce a structurally-valid key.
  AC1/AC2/AC5 (dev-path TLS bypass, container creation, idempotency) require a running
  Cosmos emulator and are verified by integration testing — they are not covered in unit tests
  because CosmosClientBuilder.Build() eagerly validates the connection on the dev path.
  All 79 Shared.Models.Tests pass after this story (7 new + 72 pre-existing).

## STORY-9 — Hydration pipeline — IEnrichmentStep registration and parallel execution
Status: complete
Files produced:
- src/Shared.Models/Contracts/IEnrichmentStep.cs (updated — added string StepName { get; })
- src/Onboarding.Function/Pipeline/HydrationPipeline.cs
- src/Onboarding.Function/Pipeline/Steps/AddressEnrichmentStep.cs
- src/Onboarding.Function/Pipeline/Steps/CreditCheckEnrichmentStep.cs
- src/Onboarding.Function/Pipeline/Steps/ComplianceEnrichmentStep.cs
- src/Onboarding.Function/DependencyInjection/OnboardingServiceExtensions.cs (updated — DI registrations)
- tests/Onboarding.Function.Tests/Pipeline/HydrationPipelineTests.cs
- tests/Onboarding.Function.Tests/Onboarding.Function.Tests.csproj (updated — added Microsoft.Extensions.DependencyInjection 10.0.0)
- tests/Shared.Models.Tests/ServiceContracts/ServiceContractsTests.cs (updated — added StepName to TestEnrichmentStepA/B fakes)
Tests written: 11
Tests passing: 11
Notes: >
  IEnrichmentStep was updated to add `string StepName { get; }` — required by HydrationPipeline
  to construct skipped EnrichmentResults. This is a breaking change to the interface but no
  concrete implementations existed before this story. The two test fakes in ServiceContractsTests
  were updated to implement StepName.
  HydrationPipeline calls AppliesTo twice per step (once to partition applicable/skipped) — this
  matches the exact pattern in the architecture document and is acceptable since AppliesTo is a
  pure, cheap boolean check.
  All three concrete steps are stubs returning Applied=true for all contexts; concrete
  applicability rules are deferred as specified in the story.
  DI registration test requires Microsoft.Extensions.DependencyInjection 10.0.0 (matched to the
  transitive version pulled in by Microsoft.Azure.Functions.Worker).
  All 88 Shared.Models.Tests pass; all 19 Onboarding.Function.Tests pass (8 from STORY-8 + 11 new).
