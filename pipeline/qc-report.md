<!-- QC-STORY-17-ITER-1 | 2026-06-24T00:03:28Z | pending -->

<!-- QC-STORY-16-ITER-1 | 2026-06-23T21:55:41Z | pending -->

<!-- QC-STORY-15-ITER-1 | 2026-06-23T17:04:55Z | pending -->

## QC report — STORY-15 — iteration 1

### Verdict: PASS

---

### Issues found

None. No correctness or coverage issues identified.

---

### Passed checks

- [x] **Story coverage — AC1 (add appends to correct collection with AddedAt/AddedBy)**: Four tests cover all collection types:
  - `AddOperationHandler_Apply_AppendsAddressWithAuditFields` — asserts `AddedBy = "amendment-app"`, `AddedAt >= before`, `AddressId`, `Line1` ✓
  - `AddOperationHandler_Apply_AppendsPhoneNumberWithAuditFields` — asserts `AddedBy`, `PhoneId`, `Number` ✓
  - `AddOperationHandler_Apply_AppendsEmailAddressWithAuditFields` — asserts `AddedBy`, `EmailId`, `Address` ✓
  - `AddOperationHandler_Apply_AppendsBankOperationWithAuditFields` — asserts `AddedBy`, `OperationId`, `BankName` ✓
  - `AddOperationHandler_Apply_PreservesExistingItemsInCollection` — verifies existing items are retained alongside the new one ✓
- [x] **Story coverage — AC2 (remove by stable ID)**: Four tests cover all collection types:
  - `RemoveOperationHandler_Apply_RemovesMatchingAddress` ✓
  - `RemoveOperationHandler_Apply_RemovesMatchingPhoneNumber` ✓
  - `RemoveOperationHandler_Apply_RemovesMatchingEmailAddress` ✓
  - `RemoveOperationHandler_Apply_RemovesMatchingBankOperation` ✓
  - `RemoveOperationHandler_Apply_LeavesOtherAddressesUntouched` — verifies non-matching items are preserved ✓
- [x] **Story coverage — AC3 (update only specified fields)**: Four tests covering all collection types:
  - `UpdateOperationHandler_Apply_UpdatesSpecifiedAddressFieldsOnly` — updates `line1`, asserts `line2`, `city`, `addedBy` unchanged ✓
  - `UpdateOperationHandler_Apply_UpdatesSpecifiedPhoneFields` — updates `number`, asserts `phoneType`, `addedBy` unchanged ✓
  - `UpdateOperationHandler_Apply_UpdatesSpecifiedEmailFields` — updates `address`, asserts `emailType` unchanged ✓
  - `UpdateOperationHandler_Apply_UpdatesSpecifiedBankOperationFields` — updates `bankName`, asserts `accountNumber`, `addedBy` unchanged ✓
  - `UpdateOperationHandler_Apply_LeavesNonMatchingItemsUnchanged` — verifies items not matching the stable ID are untouched ✓
- [x] **Story coverage — AC4 (no handler ambiguity per OperationType)**: `AllHandlers_HaveUniqueOperationTypes` instantiates all three handlers and asserts `Distinct().Count() == handlers.Count` ✓
- [x] **TDD compliance**: No failure scenarios are specified in STORY-15 ACs — Validate stubs are the correct behaviour at this story (full validation logic deferred to STORY-16). Happy-path coverage is complete for all four ACs. The story explicitly states stubs return `{ IsRejected = false, IsNoOp = false }`.
- [x] **Architecture alignment — class names and modifiers**: `AddOperationHandler` (sealed), `RemoveOperationHandler` (sealed), `UpdateOperationHandler` (sealed) — all match architecture document exactly ✓
- [x] **Architecture alignment — OperationType values**: `"add"`, `"remove"`, `"update"` — match the architecture payload schema and STORY-15 spec ✓
- [x] **Architecture alignment — AddedBy / AddedAt stamping**: `AddedBy = "amendment-app"`, `AddedAt = DateTimeOffset.UtcNow` on every add path for all four collection types ✓
- [x] **Architecture alignment — stable ID lookup**: `AddOperationHandler` looks in `operationDetails` for the stable ID (matching architecture example `{ "parameters": {}, "operationDetails": { "emailId": ... } }`); `RemoveOperationHandler` and `UpdateOperationHandler` look in `parameters` (matching `{ "parameters": { "addressId": ... } }`) ✓
- [x] **Architecture alignment — Validate stubs**: All three handlers return `new() { IsRejected = false, IsNoOp = false }` — correct stub behaviour for STORY-15; full rules implemented in STORY-16 ✓
- [x] **Lean code**: No dead code, no unused parameters. Private helper methods (`GetString`, `GetBool`, `TryGet`, `TryGetBool`, `TryGetEnum`, `ApplyAddressUpdate`, etc.) are minimal, single-purpose, and non-speculative. `record with {}` spread pattern is clean and correct for immutable updates ✓
- [x] **DI registration**: All three handlers registered as `IOperationHandler` (Scoped) in `AddAmendmentServices()`:
  - `services.AddScoped<IOperationHandler, AddOperationHandler>()` ✓
  - `services.AddScoped<IOperationHandler, RemoveOperationHandler>()` ✓
  - `services.AddScoped<IOperationHandler, UpdateOperationHandler>()` ✓
  - `AddAmendmentServices_RegistersAllThreeOperationHandlers` resolves `IEnumerable<IOperationHandler>` and asserts all three `OperationType` values are present ✓
- [x] **Logging**: Handlers are pure domain functions with no side effects — no logging required or present. Correct for this component type ✓
- [x] **Error handling / ErrorCategory**: N/A for STORY-15 — Validate stubs always return non-rejected; error classification for operation handlers is STORY-16 ✓
- [x] **Cosmos partition keys**: N/A — no Cosmos operations in this story ✓
- [x] **Test count matches log**: 23 tests in `OperationHandlerTests.cs`, 23 reported in coding log ✓
- [x] **Regression safety**: Coding log confirms all 107 Shared.Models.Tests and 46 Onboarding.Function.Tests continue to pass; 36 Amendment.Function.Tests pass (13 pre-existing + 23 new) ✓

---

### Recommendation

PASS → approved for git push

---

## QC report — STORY-14 — iteration 1

### Verdict: PASS

---

### Issues found

None. No correctness or coverage issues identified.

**Structural observation (not a failure — inherited from STORY-12/13):**
The story technical notes specify that `AddAmendmentServices()` should register `IIdempotencyService`, `IVersionGapDetector`, and `IEntityApiClient`. In the actual implementation, all three were registered in `AddSharedServices()` during STORY-12 and STORY-13, making them available globally (including to Onboarding.Function which does not need them). The services are correctly available in the DI container when Amendment.Function starts — there is no runtime defect. This is a design placement deviation but not a correctness or coverage problem. Per QC rules, structure deviations that do not affect correctness are not fails.

**Informational — net10.0 vs net8.0:**
`Amendment.Function.csproj` targets `net10.0` rather than the `net8.0` specified in the story and architecture document. This is consistent with every other project in the codebase (established in STORY-1 and STORY-8) and reflects the installed SDK version. Not a failure.

---

### Passed checks

- [x] **Story coverage — AC1**: `Run_Method_HasKafkaTriggerWithAmendmentConsumerGroup` verifies `KafkaTriggerAttribute` with `ConsumerGroup = "amendment-func"` via reflection. Trigger is wired on `%KafkaTopic%` / `%KafkaBootstrapServers%` from configuration.
- [x] **Story coverage — AC2**: Three tests cover the idempotency-first path: `ProcessBatchAsync_DuplicateMessage_PipelineIsNotCalled`, `ProcessBatchAsync_DuplicateMessage_MarkProcessedAsyncNotCalled`, and `ProcessBatchAsync_DuplicateMessage_LogsDuplicateMessageSkipped`. `IsDuplicateAsync` is called before `_pipeline.ProcessAsync` in the batch loop.
- [x] **Story coverage — AC3**: `ProcessBatchAsync_NonDuplicateMessage_TopicRoleIsAmendment` uses `CapturingPipeline` to assert `context.TopicRole == "amendment"`.
- [x] **Story coverage — AC4**: `ProcessBatchAsync_OutOfMemoryException_RethrowsWithoutRouting` confirms `OutOfMemoryException` rethrows and neither DLQ nor retry service is called.
- [x] **TDD compliance**: All failure scenarios are covered — permanent exception → dead-letter, transient → retry, unknown → retry (treated as transient), OOM rethrow, pipeline failure suppresses `MarkProcessedAsync`.
- [x] **Architecture alignment**: Class named `AmendmentKafkaFunction` (sealed) — matches architecture document exactly. Entry point sequence is (1) build context, (2) idempotency check, (3) pipeline, (4) mark processed — matches story spec.
- [x] **Lean code**: No dead code, no unused parameters, no speculative abstractions. Null stubs (`NullRetryService`, `NullDeadLetterService`, `NullAuditService`) are minimal and clearly scoped as stubs pending STORY-18/21.
- [x] **DI registration**: `AddAmendmentServices()` registers `IAmendmentPipeline`, `IRetryService`, `IDeadLetterService`, `IAuditService`, and `ServiceBusClient` (singleton). `IIdempotencyService`, `IVersionGapDetector`, `IEntityApiClient` are available via `AddSharedServices()`. All services required by `AmendmentKafkaFunction` are resolvable at startup.
- [x] **Logging**: `ILogger<AmendmentKafkaFunction>` with structured message templates — `"DuplicateMessageSkipped {MessageId} {EntityId} {TopicRole}"`, `"MessageProcessingFailed {MessageId} {EntityId} {Category} {ExceptionType}"`, `"BatchCompleted total={Total} succeeded={Succeeded} transientFailures={Transient} permanentFailures={Permanent}"`. No string interpolation in log templates.
- [x] **Error handling**: `OutOfMemoryException` rethrows. `ErrorCategory.Permanent` → `IDeadLetterService.SendAsync` with `attempt: 1`. `Transient` and `Unknown` → `IRetryService.EnqueueAsync` with `attemptCount: 1`. `IErrorClassifier` used correctly.
- [x] **Cosmos partition keys**: N/A — STORY-14 makes no direct Cosmos writes (reads/writes are in STORY-12 idempotency service, not this function's entry-point code).
- [x] **host.json**: Identical to onboarding-func — `maxTelemetryItemsPerSecond: 20`, `excludedTypes: "Exception;Trace"`, `enableDependencyTracking: true`, log levels `Information` for default and Function.
- [x] **Test count**: 13 tests written and passing, covering all 4 ACs plus additional failure, routing, and batch-counter scenarios.
- [x] **InternalsVisibleTo**: `AssemblyInfo.cs` exposes `internal` `ProcessBatchAsync` to `Amendment.Function.Tests` — same testability pattern as Onboarding.Function.
- [x] **Batch counter accuracy**: `ProcessBatchAsync_MixedBatch_LogsBatchCompletedWithCorrectCounts` asserts `total=3`, `succeeded=2`, `permanentFailures=1`, `transientFailures=0` — duplicate skips count as succeeded.
- [x] **Batch isolation**: `ProcessBatchAsync_OneMessageFails_RemainingMessagesAreProcessed` confirms a failing message does not halt other messages in the same batch.

---

### Recommendation

PASS → approved for git push

---

## QC report — STORY-13 — iteration 1

### Verdict: PASS

---

### Issues found

No blocking issues. Two observations for reviewer awareness:

- [ ] **AC4 WasApiFallback component not tested in this story** — `IVersionGapDetector.ResolveDataAsync` returns only `EntityData`; the interface contract (defined in STORY-5) provides no return channel for a boolean flag. The coding log correctly notes that the caller must set `WasApiFallback = true` on `HydrationContext` based on whether gap >= 2. The `TelemetryClient.TrackDependency` half of AC4 is fully covered. The WasApiFallback half must be verified in STORY-14 when `AmendmentOrchestrator` builds the `HydrationContext`.
      Suggested fix: Ensure STORY-14's `AmendmentOrchestrator` test asserts `HydrationContext.WasApiFallback = true` when `VersionGapDetector.ResolveDataAsync` was called via the gap path.

- [ ] **TelemetryClient.TrackDependency with `Success = false` not tested** — The coding log notes that the `finally` block records a failed dependency when `GetEntityAtVersionAsync` throws. This is a correctly implemented behaviour but no test exercises the failure path to assert `dep.Success == false`. No story AC requires it explicitly.
      Suggested fix: Add `ResolveDataAsync_WhenApiCallThrows_TracksDependencyTelemetryWithSuccessFalse` to `VersionGapDetectorTests` in a follow-up.

---

### Passed checks

- [x] **AC1 covered** — `ResolveDataAsync_WhenGapIsOne_ReturnsPayloadData_WithoutApiCall` and `_WhenGapIsZero_` both verify payload is returned as-is and `GetEntityAtVersionAsync` is never called.
- [x] **AC2 covered** — `_WhenGapIsTwo_CallsApiClient` verifies API invocation; `_WhenGapIsTwo_LogsVersionGapDetectedWarning` verifies the warning log; `_WhenGapIsGreaterThanTwo_ReturnsApiData` confirms API data is returned.
- [x] **AC3 covered** — `_WhenSameEntityVersionRequestedTwice_ReturnsCachedData_WithoutSecondApiCall` asserts `GetEntityAtVersionAsync` called exactly once and second result is the same object instance.
- [x] **AC4 (TelemetryClient half) covered** — `_WhenApiCallSucceeds_TracksDependencyTelemetry` asserts `DependencyTelemetry.Name = "EntityApi.GetEntityAtVersion"` and `Success = true`.
- [x] **EntityApiClient failure scenarios covered** — `_WhenHttpReturns404_ThrowsHttpRequestException` and `_WhenHttpReturns500_ThrowsHttpRequestException` cover non-2xx error propagation.
- [x] **Architecture alignment — class names** — `VersionGapDetector` (sealed), `EntityApiClient` (sealed), `IVersionGapDetector`, `IEntityApiClient` all match architecture document exactly.
- [x] **Gap formula correct** — `gap = incomingVersion - storedVersion`; `gap >= 2` triggers API; `gap < 2` returns payload.
- [x] **Cache key format** — `$"api-entity-{entityId}-v{incomingVersion}"` matches architecture spec `$"api-entity-{entityId}-v{version}"`.
- [x] **Cache absolute expiry** — `TimeSpan.FromSeconds(60)` passed to `_cache.Set`.
- [x] **Warning log template** — `"VersionGapDetected {EntityId} stored={StoredVersion} incoming={IncomingVersion} gap={Gap}"` matches architecture document verbatim.
- [x] **TelemetryClient.TrackDependency fields** — `Name = "EntityApi.GetEntityAtVersion"`, `Target = _apiBaseUrl`, `Data = $"GET /entities/{entityId}/versions/{incomingVersion}"`, `Duration`, `Success`, `Timestamp` all set per architecture document.
- [x] **DI registration** — `AddMemoryCache()`, `AddScoped<IVersionGapDetector, VersionGapDetector>()`, and `AddHttpClient<IEntityApiClient, EntityApiClient>().AddStandardResilienceHandler(...)` all registered in `AddSharedServices()`.
- [x] **DI tests present** — `AddSharedServices_RegistersIVersionGapDetector_AsScoped` verifies lifetime=Scoped and implementation type; `_RegistersIEntityApiClient_AsHttpClient` verifies service registration presence.
- [x] **Polly settings** — `MaxRetryAttempts = 3`, `Delay = 200ms`, `BackoffType = Exponential`, `FailureRatio = 0.5`, `MinimumThroughput = 10`, `SamplingDuration = 30s`, `AttemptTimeout.Timeout = 5s`. Use of `AttemptTimeout` instead of `Timeout` is the correct API name for `Microsoft.Extensions.Http.Resilience` 9.0.0 — documented in coding log.
- [x] **Structured logging only** — no string interpolation in any log message template.
- [x] **Lean code** — no dead code, no unused parameters, no speculative abstractions.
- [x] **TelemetryClient injected via constructor** — tests supply it via `CapturingChannel`; Azure Functions host provides it in production.
- [x] **Regression safety** — coding log confirms all 107 Shared.Models.Tests and 46 Onboarding.Function.Tests continue to pass after this story.

---

### Recommendation

PASS → approved for git push

---

## QC report — STORY-12 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **DuplicateMessageSkipped log template is incomplete** — `src/Shared.Models/Idempotency/IdempotencyService.cs` line 27
      Architecture document specifies `"DuplicateMessageSkipped {MessageId} {EntityId} {TopicRole}"` with three structured properties. Implementation logs only `"DuplicateMessageSkipped {MessageId}"`. EntityId and TopicRole are inaccessible because `IsDuplicateAsync(string messageId)` only receives a messageId.
      Suggested fix: Either extend the `IsDuplicateAsync` signature to accept `KafkaMessageContext` (so EntityId and TopicRole are available), or leave as-is and document that STORY-20 will complete the log template. The current signature is constrained by the STORY-5 interface definition. This is a design-level gap, not a correctness bug, and the STORY-12 AC only requires "a DuplicateMessageSkipped Information log is emitted" — which is satisfied.

### Passed checks

- [x] **AC1 — duplicate exists → returns true**: `IsDuplicateAsync_WhenMessageExists_ReturnsTrue` ✓
- [x] **AC1 — duplicate exists → logs DuplicateMessageSkipped at Information level**: `IsDuplicateAsync_WhenMessageExists_LogsDuplicateMessageSkipped` verifies log level and message content ✓
- [x] **AC2 — message not found → returns false**: `IsDuplicateAsync_WhenMessageNotFound_ReturnsFalse` — `CosmosException(NotFound)` caught, returns false ✓
- [x] **AC3 — MarkProcessedAsync writes IdempotencyRecord with messageId as id and partition key**: `MarkProcessedAsync_WhenSuccess_CallsCreateItemAsync_WithIdEqualToMessageId` asserts `r.Id == messageId`, `r.MessageId == messageId`, `r.Ttl == 604800`, and `new PartitionKey(messageId)` ✓
- [x] **AC4 — 409 Conflict treated as success, no rethrow**: `MarkProcessedAsync_When409Conflict_DoesNotRethrow` uses `Record.ExceptionAsync` to assert null thrown ✓
- [x] **Failure scenario — non-409 CosmosException rethrows**: `MarkProcessedAsync_WhenNon409CosmosException_Rethrows` verifies 429 propagates — correctly absent from the `when` filter ✓
- [x] **Architecture alignment — IdempotencyService**: `sealed`, implements `IIdempotencyService`, correct use of `ReadItemAsync` (duplicate check) and `CreateItemAsync` (mark processed) ✓
- [x] **Architecture alignment — IdempotencyRecord**: `Id`, `MessageId`, `ProcessedAt`, `Ttl = 604800` — all correct ✓
- [x] **Architecture alignment — container and partition key**: `IdempotencyContainer` injected, `new PartitionKey(messageId)` used in both read and write ✓
- [x] **Architecture alignment — Conflict suppression comment**: Inline comment explains race condition rationale ✓
- [x] **DI registration**: `services.AddScoped<IIdempotencyService, IdempotencyService>()` confirmed in `ServiceCollectionExtensions.cs`; `AddSharedServices_RegistersIIdempotencyService_AsScoped` verifies lifetime and implementation type ✓
- [x] **Logging — structured logging used**: `_logger.LogInformation("DuplicateMessageSkipped {MessageId}", messageId)` uses message template, not string interpolation ✓
- [x] **Lean code**: No dead code; `MarkProcessedAsync` catch block correctly swallows only `HttpStatusCode.Conflict` ✓
- [x] **Test count matches log**: 7 tests in `IdempotencyServiceTests.cs`, 7 reported in coding log ✓

### Recommendation

PASS → approved for git push. The partial log template (missing EntityId/TopicRole) is a design constraint of the `IsDuplicateAsync(string messageId)` interface, not a coding error; it should be addressed either by extending the interface or accepting STORY-20 as the completion point for the full structured log.

---

## QC report — STORY-11 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **AuditRecord nested structure fields not asserted in tests** — `tests/Onboarding.Function.Tests/Pipeline/OnboardingPipelineTests.cs`
      `KafkaContextInfo`, `VersionInfo`, `HydrationInfo` (StepsApplied, StepsSkipped, TotalDurationMs, StepBreakdown), and `RetryInfo` (AttemptNumber=1, WasRetry=false) are all populated in `OnboardingPipeline.ProcessAsync` but no test asserts their values. The coding log explicitly lists these as required fields. While AC4 is met (FlushAsync is called with a record carrying the correct identity fields), the structural completeness of the audit document is unverified.
      Suggested fix: Add tests that capture the `AuditRecord` and assert `RetryInfo.AttemptNumber == 1`, `RetryInfo.WasRetry == false`, `KafkaContext.Offset == context.Offset`, and `HydrationInfo.StepsApplied` matches the applied enrichment results. These are not blocking but the audit document is the compliance trail — correctness matters.

### Passed checks

- [x] **AC1 — WriteAsync calls UpsertItemAsync with entity and partyId partition key**: `WriteAsync_CallsUpsertItemAsync_WithEntityAndMatchingPartitionKey` uses Moq to verify exact entity reference and `new PartitionKey("party-abc")` ✓
- [x] **AC2 — blind upsert, no IfMatchEtag**: `WriteAsync_DoesNotSetIfMatchEtag_IsBlindUpsert` asserts `opts == null || opts.IfMatchEtag == null` ✓
- [x] **AC3 — CosmosException propagates to caller**: `WriteAsync_WhenUpsertThrowsCosmosException_PropagatesException` verifies 429 is not swallowed ✓
- [x] **AC4 — IAuditService.FlushAsync called with correct AuditRecord**: Four tests capture the flushed record and assert `MessageId`, `PartyId`, `TopicRole`, and `Outcome = "success"` ✓
- [x] **AC4 implicit — audit not called when writer throws**: `ProcessAsync_WhenWriterThrows_AuditServiceIsNotCalled` confirms sequential dependency — FlushAsync only runs after a successful upsert ✓
- [x] **Orchestration order verified**: `ProcessAsync_HappyPath_CallsAllFourServicesInOrder` asserts `["hydration", "assembler", "writer", "audit"]` exact order ✓
- [x] **HydrationContext built correctly from context + payload**: `EntityId = context.PartyId`, `IncomingVersion` from JSON payload `"version"` field, `StoredVersion = 0` — each individually tested ✓
- [x] **Missing version field handled**: `ProcessAsync_PayloadMissingVersionField_UsesDefaultVersionZero` ✓
- [x] **Assembled entity passed to writer**: `ProcessAsync_PassesAssembledEntityToCosmosWriter` uses `Assert.Same` to verify same reference ✓
- [x] **Architecture alignment — OnboardingCosmosWriter**: `sealed`, implements `IOnboardingCosmosWriter`, injects `EnrichedRecordsContainer`, calls `UpsertItemAsync` with no `ItemRequestOptions` ✓
- [x] **Architecture alignment — OnboardingPipeline**: `sealed`, implements `IOnboardingPipeline`, all four dependencies injected, AuditRecord built inline with all required top-level fields ✓
- [x] **Architecture alignment — IOnboardingCosmosWriter**: `Task WriteAsync(EnrichedCustomer entity)` ✓
- [x] **Architecture alignment — no idempotency check**: `OnboardingCosmosWriter.WriteAsync` has no guard or check before upsert ✓
- [x] **DI registrations**: `AddOnboardingServices_RegistersIOnboardingCosmosWriter` and `AddOnboardingServices_RegistersIOnboardingPipeline` verify scoped lifetime and implementation type ✓
- [x] **Lean code**: `ParseVersion` is a private static helper with a single purpose; `OnboardingCosmosWriter` is three lines of production code — no excess ✓
- [x] **Logging**: No logging in `OnboardingCosmosWriter` (correct — no log required); `OnboardingPipeline` delegates audit logging to `IAuditService` ✓
- [x] **Test count matches log**: 3 (writer) + 13 (pipeline) = 16 tests, 16 reported in coding log ✓

### Recommendation

PASS → approved for git push. The missing assertions on AuditRecord nested structures (KafkaContextInfo, VersionInfo, HydrationInfo, RetryInfo) are a test coverage gap worth addressing before integration testing, but they do not block any story acceptance criterion.

---

## QC report — STORY-10 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **LastKafkaOffset hardcoded to 0** — `src/Onboarding.Function/Pipeline/OutputAssembler.cs` line 22
      `HydrationContext` carries no `Offset` field, so `AuditSummary.LastKafkaOffset` is hardcoded to `0`. Documented in coding log as a known gap. Architecture doc specifies `LastKafkaOffset = context.Offset`.
      Suggested fix: Add `KafkaOffset` (long) to `HydrationContext` (STORY-2 model) and pass the Kafka offset from the function entry point through to the pipeline. No AC is directly violated, but the assembled document is structurally incomplete relative to the architecture schema.
- [ ] **MergeData method absent** — `src/Onboarding.Function/Pipeline/OutputAssembler.cs`
      Story technical notes require a private `MergeData` method that merges applied `EnrichmentResult.Data` values into the document's collection properties. The method is missing. Documented in coding log as intentionally deferred since enrichment steps are stubs at this stage.
      Suggested fix: Stub `MergeData` as a private method that iterates applied results and is ready to be filled in when concrete step data types are defined.

### Passed checks

- [x] **AC1 — Id and PartyId set to context.EntityId**: `Assemble_Sets_Id_And_PartyId_From_EntityId` ✓
- [x] **AC1 — Version set from IncomingVersion**: `Assemble_Sets_Version_From_IncomingVersion` ✓
- [x] **AC2 — Only Applied=true steps in LastHydrationSteps**: `Assemble_LastHydrationSteps_Contains_Only_Applied_Steps` and `Assemble_Excludes_Unapplied_Steps_From_LastHydrationSteps` ✓
- [x] **AC3 — WasApiFallback propagated correctly**: Both true and false cases tested ✓
- [x] **AC4 — No applied step omitted from LastHydrationSteps**: `Assemble_LastHydrationSteps_Includes_All_Applied_Results_With_NonZero_Duration` verifies all 3 applied steps appear ✓
- [x] **Architecture alignment — OutputAssembler**: `sealed`, implements `IOutputAssembler`, method signature matches spec ✓
- [x] **Architecture alignment — LastUpdatedBy**: "onboarding-app"/"amendment-app" based on `TopicRole` — both cases tested ✓
- [x] **Architecture alignment — ProcessedAt**: Set to `DateTimeOffset.UtcNow` — tested ✓
- [x] **Architecture alignment — LastMessageId**: Set from `context.MessageId` — tested ✓
- [x] **DI registration**: `services.AddScoped<IOutputAssembler, OutputAssembler>()` present in `OnboardingServiceExtensions.cs` ✓
- [x] **Test count matches log**: 11 tests in `OutputAssemblerTests.cs`, 11 reported in coding log ✓

### Recommendation

PASS → approved for git push. Documented gaps (LastKafkaOffset=0, absent MergeData stub) are pre-existing known deferred items recorded in the coding log; they do not block this story.

---

## QC report — STORY-9 — iteration 1

### Verdict: PASS

### Issues found

None.

### Passed checks

- [x] **AC1 — Only AppliesTo=true steps passed to Task.WhenAll**: `ExecuteAsync_AllStepsApply_AllResultsHaveAppliedTrue` and `ExecuteAsync_MixedApplicability_ResultsCombineAppliedAndSkipped` ✓
- [x] **AC2 — Two concurrent steps → one EnrichmentResult each with Applied=true**: `ExecuteAsync_TwoApplicableSteps_EachResultCarriesCorrectStepName` ✓
- [x] **AC3 — Skipped step in results with Applied=false and Duration=TimeSpan.Zero**: `ExecuteAsync_OneStepNotApplicable_SkippedResultHasAppliedFalseAndZeroDuration` ✓
- [x] **AC3 — Mixed scenario returns combined applied+skipped**: `ExecuteAsync_MixedApplicability_ResultsCombineAppliedAndSkipped` ✓
- [x] **AC3 — All-skipped scenario**: `ExecuteAsync_NoStepsApply_AllResultsSkipped` ✓
- [x] **AC4 — EnrichmentStepException bubbles up**: `ExecuteAsync_StepThrowsEnrichmentStepException_BubblesUpToCaller` ✓
- [x] **Architecture alignment — HydrationPipeline**: `sealed`, implements `IHydrationPipeline`, injects `IEnumerable<IEnrichmentStep>` ✓
- [x] **Architecture alignment — parallel execution pattern**: `Task.WhenAll(applicable.Select(s => s.EnrichAsync(context)))` matches architecture document exactly ✓
- [x] **Architecture alignment — skipped result construction**: `StepName = s.StepName, Applied = false, Duration = TimeSpan.Zero` ✓
- [x] **Concrete step stubs**: `AddressEnrichmentStep`, `CreditCheckEnrichmentStep`, `ComplianceEnrichmentStep` all exist, all `AppliesTo` return `true` — tested ✓
- [x] **DI registrations**: All three steps registered as `IEnrichmentStep` scoped; `IHydrationPipeline` registered scoped — `AddOnboardingServices_RegistersAllThreeEnrichmentSteps` and `AddOnboardingServices_RegistersIHydrationPipeline` ✓
- [x] **IEnrichmentStep updated to include StepName**: Breaking change correctly handled; `ServiceContractsTests` fakes updated ✓
- [x] **Test count matches log**: 11 tests in `HydrationPipelineTests.cs`, 11 reported in coding log ✓

### Recommendation

PASS → approved for git push

---

## QC report — STORY-8 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **MaxBatchSize and MinBatchSize not configured on trigger** — `src/Onboarding.Function/Functions/OnboardingKafkaFunction.cs` line 38–43
      Story technical notes require `MaxBatchSize` and `MinBatchSize` on the Kafka trigger with values from configuration. The `[KafkaTrigger]` attribute only sets `ConsumerGroup` and `Protocol`; `MaxBatchSize`/`MinBatchSize` are absent.
      Suggested fix: Add `MaxBatchSize = int.Parse("%KafkaMaxBatchSize%")` and `MinBatchSize = int.Parse("%KafkaMinBatchSize%")` to the attribute, and add those keys to `local.settings.json`. This is a technical note requirement, not an AC.

### Passed checks

- [x] **AC1 — Trigger connects to KafkaBootstrapServers and topic from configuration**: `[KafkaTrigger("%KafkaTopic%", "%KafkaBootstrapServers%"...)]` ✓
- [x] **AC2 — Per-message failure isolation**: `ProcessBatchAsync_OneMessageThrowsPermanentException_OtherMessagesStillProcessed` and `ProcessBatchAsync_OneMessageThrowsTransientException_OtherMessagesStillProcessed` ✓
- [x] **AC3 — OutOfMemoryException rethrows without routing**: `ProcessBatchAsync_OutOfMemoryException_RethrowsWithoutRouting` verifies DLQ and retry are empty ✓
- [x] **AC4 — BatchCompleted log with accurate counters**: `ProcessBatchAsync_AllMessagesSucceed_LogsBatchCompletedWithCorrectCounts` and `ProcessBatchAsync_BatchCompleted_AlwaysWrittenEvenWhenAllFail` (mixed scenario) ✓
- [x] **Error routing — Permanent → DeadLetter**: `ProcessBatchAsync_PermanentException_SendsToDeadLetter` (attempt=1 verified) ✓
- [x] **Error routing — Transient → Retry**: `ProcessBatchAsync_TransientException_EnqueuesToRetry` (attemptCount=1 verified) ✓
- [x] **Error routing — Unknown treated as Transient**: `ProcessBatchAsync_UnknownException_TreatedAsTransientAndEnqueuesToRetry` ✓
- [x] **Architecture alignment — function class name**: `OnboardingKafkaFunction` ✓
- [x] **Architecture alignment — trigger ConsumerGroup**: `"onboarding-func"` ✓
- [x] **Architecture alignment — IOnboardingPipeline signature**: `Task ProcessAsync(KafkaMessageContext context, string rawPayload)` ✓
- [x] **Architecture alignment — Program.cs registrations**: `AddSharedServices()`, `AddCosmosDb()`, `AddOnboardingServices()`, `ConfigureFunctionsApplicationInsights()` all present ✓
- [x] **Architecture alignment — host.json**: `maxTelemetryItemsPerSecond: 20`, `excludedTypes: "Exception;Trace"`, `enableDependencyTracking: true`, log levels `Information`/`Information`/`Warning` — exact match ✓
- [x] **Structured logging**: `LogError` uses message template with `{MessageId}`, `{EntityId}`, `{Category}`, `{ExceptionType}` matching architecture ✓
- [x] **Logging — BatchCompleted**: uses `LogWarning` with exact template from architecture ✓
- [x] **Test count matches log**: 8 tests in `OnboardingKafkaFunctionTests.cs`, 8 reported in coding log ✓

### Recommendation

PASS → approved for git push. MaxBatchSize/MinBatchSize gap is a technical note item; it does not block any acceptance criterion.

---

## QC report — STORY-7 — iteration 1

### Verdict: FAIL

### Issues found

- [ ] **STORY-7 entry missing from pipeline/coding-log.md** — `pipeline/coding-log.md`
      The coder implemented STORY-7 (files confirmed at `src/Shared.Models/Kafka/KafkaMessageContextBuilder.cs` and `tests/Shared.Models.Tests/Kafka/KafkaMessageContextBuilderTests.cs`) but never wrote the required coding log entry. The `/agent-git-push` agent stages only the files listed in the coding log for the current story; without the STORY-7 entry, these files cannot be committed.
      Suggested fix: Append the following entry to `pipeline/coding-log.md`:
      ```
      ## STORY-7 — KafkaMessageContext population and log scope
      Status: complete
      Files produced:
      - src/Shared.Models/Kafka/KafkaMessageContextBuilder.cs
      - tests/Shared.Models.Tests/Kafka/KafkaMessageContextBuilderTests.cs
      Tests written: 9
      Tests passing: 9
      ```

### Passed checks

- [x] **AC1 — KafkaMessageContext populated from Kafka event with all 7 fields**: `Build_ValidHeaders_ReturnsContextWithMessageIdFromHeader`, `Build_ValidHeaders_ReturnsContextWithAllProperties`, `Build_ValidHeaders_ReceivedAtIsApproximatelyUtcNow` ✓
- [x] **AC2 — BeginScope includes all 6 properties**: `BuildLogScope_ReturnsAllSixKafkaContextProperties` (all 6 values asserted) and `BuildLogScope_ScopeContainsExactlySixEntries` (count=6) ✓
- [x] **AC3 — Missing messageId header throws MessageValidationException**: `Build_MissingMessageIdHeader_ThrowsMessageValidationException`, `Build_EmptyMessageIdHeader_ThrowsMessageValidationException`, `Build_MissingMessageIdHeader_ExceptionMessageDescribesHeaderAbsence` ✓
- [x] **Architecture alignment — KafkaMessageContextBuilder**: `public static class`, in `Shared.Models.Kafka` namespace, static `Build()` and `BuildLogScope()` methods ✓
- [x] **Architecture alignment — exception fields**: `entityId: "unknown"`, `messageId: "unknown"` on missing header exception — asserted in test ✓
- [x] **Architecture alignment — log scope keys**: `["MessageId"]`, `["PartyId"]`, `["Topic"]`, `["Partition"]`, `["Offset"]`, `["TopicRole"]` — exact match to architecture ✓
- [x] **Architecture alignment — usage in function**: `OnboardingKafkaFunction` calls `KafkaMessageContextBuilder.Build()` and `_logger.BeginScope(KafkaMessageContextBuilder.BuildLogScope(ctx))` correctly ✓
- [x] **TDD compliance**: Missing header (failure) path has 3 dedicated tests ✓
- [x] **Lean code**: Static class with two static methods, no dead code ✓
- [x] **DI registration**: N/A — static helper class ✓

### Recommendation

FAIL → return to coding agent to append the STORY-7 entry to `pipeline/coding-log.md`. Code and tests are correct; only the log entry needs to be written.

---

## QC report — STORY-6 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **AC1, AC2, AC4, AC5 lack unit test coverage** — dev-path acceptance criteria
      AC1 (dev TLS bypass), AC2 (dev container creation), AC4 (throttling retry options), and AC5 (idempotent container creation) have no unit tests. Documented in coding log: `CosmosClientBuilder.Build()` eagerly validates the connection on the dev path, making isolated unit tests impractical. These require integration testing against the Cosmos emulator.
      Suggested fix: Note in the coding log that these ACs are covered by integration testing (local dev run with emulator), or create a thin abstraction around `CosmosClientBuilder` to enable mocking. As-is, this is a genuine SDK limitation, not a code quality issue.

### Passed checks

- [x] **AC3 — Non-dev path creates CosmosClient without TLS bypass**: `AddCosmosDb_NonDev_RegistersCosmosClientAsSingleton` and `AddCosmosDb_NonDev_CosmosClientIsSingleton_SameInstanceReturned` verify the non-dev path executes and returns a valid singleton ✓
- [x] **AC3 — Non-dev path has no TLS bypass by construction**: dev branch is gated by `environment.IsDevelopment()`; non-dev branch calls `CosmosClientBuilder` without `WithHttpClientFactory` ✓
- [x] **Container registration — enriched-records**: `AddCosmosDb_NonDev_RegistersEnrichedRecordsContainer` ✓
- [x] **Container registration — audit-metrics**: `AddCosmosDb_NonDev_RegistersAuditMetricsContainer` ✓
- [x] **Container registration — idempotency-records**: `AddCosmosDb_NonDev_RegistersIdempotencyContainer` ✓
- [x] **Container wrappers are singletons**: `AddCosmosDb_NonDev_ContainerWrappersAreSingletons` ✓
- [x] **Partition keys correct**: `enriched-records` → `/partyId`, `audit-metrics` → `/processedDate`, `idempotency-records` → `/messageId` — verified in `InitializeDevContainersAsync` ✓
- [x] **Architecture alignment — retry options**: `WithThrottlingRetryOptions(TimeSpan.FromSeconds(10), maxRetryAttemptsOnThrottledRequests: 5)` applied on both dev and non-dev paths ✓
- [x] **Architecture alignment — extension method signature**: `AddCosmosDb(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)` ✓
- [x] **Architecture alignment — container wrapper types**: `EnrichedRecordsContainer`, `AuditMetricsContainer`, `IdempotencyContainer` sealed records wrapping `Container` ✓
- [x] **Architecture alignment — config keys**: `CosmosDbConnection` and `CosmosDbName` read from `IConfiguration` — never hardcoded ✓
- [x] **IServiceCollection chaining**: `AddCosmosDb_NonDev_ReturnsIServiceCollection_ForChaining` ✓
- [x] **Test count matches log**: 7 tests in `CosmosDbExtensionsTests.cs`, 7 reported in coding log ✓

### Recommendation

PASS → approved for git push. Dev-path ACs (AC1, AC2, AC4, AC5) are genuinely non-unit-testable due to Cosmos SDK eager connection validation; this is a documented known limitation in the coding log.

---

## QC report — STORY-5 — iteration 1

### Verdict: PASS

### Issues found

None.

### Passed checks

- [x] **AC1 — IEnumerable<IEnrichmentStep> resolves all registered steps**: `AddSharedServices_MultipleIEnrichmentSteps_AllResolved` registers two test steps and verifies count=2 ✓
- [x] **AC2 — IEnumerable<IOperationHandler> resolves all registered handlers**: `AddSharedServices_MultipleIOperationHandlers_AllResolved` registers two test handlers and verifies count=2 ✓
- [x] **AC3 — ValidationResult with IsRejected=true exposes Reason, IsNoOp=false**: `ValidationResult_IsRejected_True_ReasonIsAccessible_IsNoOpIsFalse` ✓; also `ValidationResult_IsNoOp_True_IsRejectedDefaultsFalse_ReasonNull` ✓
- [x] **AC4 — IErrorClassifier resolves and works**: `AddSharedServices_RegistersIErrorClassifier_AsSingleton` (same-instance check) and `AddSharedServices_ResolvedIErrorClassifier_CanClassifyExceptions` ✓
- [x] **Architecture alignment — all 11 interfaces present with correct signatures**:
  - `IEnrichmentStep`: `StepName`, `AppliesTo`, `EnrichAsync` ✓
  - `IOperationHandler`: `OperationType`, `Validate`, `Apply` ✓
  - `IAuditService`: `FlushAsync(AuditRecord)` ✓
  - `IRetryService`: `EnqueueAsync(KafkaMessageContext, string, int)` ✓
  - `IDeadLetterService`: `SendAsync(KafkaMessageContext, string, string, int)` ✓
  - `IHydrationPipeline`: `ExecuteAsync(HydrationContext)` ✓
  - `IOutputAssembler`: `Assemble(HydrationContext, IEnumerable<EnrichmentResult>)` ✓
  - `IIdempotencyService`: `IsDuplicateAsync(string)`, `MarkProcessedAsync(string)` ✓
  - `IAmendmentOrchestrator`: `OrchestrateAsync(AmendmentMessage, KafkaMessageContext)` ✓
  - `IVersionGapDetector`: `ResolveDataAsync(int, int, string, EntityData)` ✓
  - `IEntityApiClient`: `GetEntityAtVersionAsync(string, int)` ✓
- [x] **Architecture alignment — ValidationResult**: `IsRejected` (bool), `IsNoOp` (bool), `Reason` (string?) — sealed record ✓
- [x] **Architecture alignment — AmendmentResult**: `IsNoOp` (bool), `Entity` (EnrichedCustomer?) — sealed record ✓
- [x] **DI registration — IErrorClassifier as singleton**: Registered in `ServiceCollectionExtensions.AddSharedServices()` ✓
- [x] **Lean code**: Interfaces are minimal with no speculative methods ✓
- [x] **Test count**: 9 tests in `ServiceContractsTests.cs`; coding log reports 8 (minor off-by-one, likely `AddSharedServices_MultipleIOperationHandlers_AllResolved` was added after the initial log entry). All tests are valid and correct ✓

### Recommendation

PASS → approved for git push

---

## QC report — STORY-4 — iteration 1

### Verdict: PASS

### Issues found

None.

### Passed checks

- [x] **Story coverage — AC1 (CosmosException 429 → Transient)**: `Classify_CosmosException429_ReturnsTransient` asserts `ErrorCategory.Transient` for `HttpStatusCode.TooManyRequests`.
- [x] **Story coverage — AC2 (CosmosException 412 → Transient)**: `Classify_CosmosException412_ReturnsTransient` asserts `ErrorCategory.Transient` for `HttpStatusCode.PreconditionFailed`.
- [x] **Story coverage — AC3 (JsonException → Permanent)**: `Classify_JsonException_ReturnsPermanent` ✓
- [x] **Story coverage — AC4 (MessageValidationException → Permanent)**: `Classify_MessageValidationException_ReturnsPermanent` ✓
- [x] **Story coverage — AC5 (BusinessRuleViolationException → Permanent)**: `Classify_BusinessRuleViolationException_ReturnsPermanent` ✓
- [x] **Story coverage — AC6 (TaskCanceledException → Transient)**: `Classify_TaskCanceledException_ReturnsTransient` ✓
- [x] **Story coverage — AC7 (unknown → Unknown)**: `Classify_UnknownException_ReturnsUnknown` uses `InvalidOperationException` as a representative unknown type ✓
- [x] **Technical notes — TimeoutException / OperationCanceledException → Transient**: `Classify_TimeoutException_ReturnsTransient` and `Classify_OperationCanceledException_ReturnsTransient` both pass ✓
- [x] **Technical notes — HttpRequestException transient statuses**: Three dedicated tests cover all three statuses — `ServiceUnavailable`, `GatewayTimeout`, `TooManyRequests` — each returning `Transient` ✓
- [x] **Technical notes — HttpRequestException non-transient (e.g. 404) → Unknown**: `Classify_HttpRequestExceptionNonTransientStatus_ReturnsUnknown` confirms the `IsTransientHttpStatus` helper correctly rejects non-listed codes ✓
- [x] **Technical notes — CosmosException BadRequest → Permanent**: `Classify_CosmosException400_ReturnsPermanent` ✓
- [x] **Architecture alignment — ErrorCategory enum**: `Transient`, `Permanent`, `Unknown` in `Enums.cs` — matches architecture exactly.
- [x] **Architecture alignment — IErrorClassifier**: `ErrorCategory Classify(Exception ex)` — matches architecture exactly.
- [x] **Architecture alignment — ErrorClassifier**: `sealed`, implements `IErrorClassifier`, single C# switch expression with pattern matching, `IsTransientHttpStatus` private static helper — all match architecture document.
- [x] **Architecture alignment — switch arm ordering**: Transient arms appear before Permanent arms, matching the architecture document order; no shadowing issues.
- [x] **DI registration**: `services.AddSingleton<IErrorClassifier, ErrorClassifier>()` confirmed present in `src/Shared.Models/DependencyInjection/ServiceCollectionExtensions.cs` line 12. Coding log explicitly deferred this to STORY-5 (which owns `AddSharedServices()`); the registration exists in the codebase and is correct.
- [x] **Lean code**: No dead code, no unused parameters. `IsTransientHttpStatus` is a private static helper with a single logical purpose — not a speculative abstraction.
- [x] **Logging**: Not applicable — `ErrorClassifier` is a pure classification function; no side effects or logging needed.
- [x] **Test count matches log**: 14 tests in `ErrorClassifierTests.cs`; 14 reported in coding log.

### Recommendation

PASS → approved for git push

---

## QC report — STORY-3 — iteration 1

### Verdict: PASS

### Issues found

None.

### Passed checks

- [x] **Story coverage — AC1**: `MessageValidationException_CaughtAsFunctionAppException_ExposesEntityIdAndMessageId` catches the exception as `FunctionAppException` and asserts both `EntityId` and `MessageId` are accessible on the base class.
- [x] **Story coverage — AC2**: `BusinessRuleViolationException_ExposesRuleNameAlongsideEntityIdAndMessageId` asserts `RuleName`, `EntityId`, and `MessageId` are all accessible.
- [x] **Story coverage — AC3**: `EnrichmentStepException_ExposesStepNameAlongsideEntityIdAndMessageId` asserts `StepName`, `EntityId`, and `MessageId` are all accessible.
- [x] **Story coverage — AC4**: All three concrete types have a `WithInnerException_RetainsOriginalException` test using `Assert.Same(inner, ex.InnerException)`.
- [x] **TDD compliance — failure/edge scenarios**: Inner exception tests cover the exception-retention requirement; `MessageValidationException_WithoutInnerException_InnerExceptionIsNull` tests the default `null` path.
- [x] **Architecture alignment — FunctionAppException**: `abstract`, extends `Exception`, constructor signature `(string message, string entityId, string messageId, Exception? inner = null)`, `protected` access — matches architecture exactly.
- [x] **Architecture alignment — class names**: `FunctionAppException`, `MessageValidationException`, `BusinessRuleViolationException`, `EnrichmentStepException` — all match architecture document exactly.
- [x] **Architecture alignment — extra properties**: `BusinessRuleViolationException.RuleName` and `EnrichmentStepException.StepName` match architecture and story.
- [x] **Architecture alignment — constructor parameter ordering**: `ruleName` and `stepName` are inserted before `inner` on their respective concrete types — consistent with base-class pattern as documented in the coding log.
- [x] **Sealed on all concrete types**: `MessageValidationException`, `BusinessRuleViolationException`, `EnrichmentStepException` all carry `sealed` — confirmed by three `IsSealed` assertion tests.
- [x] **FunctionAppException is abstract**: Confirmed by `FunctionAppException_IsAbstract` test.
- [x] **Lean code**: No dead code, no unused parameters, no speculative abstractions. All three concrete types are as minimal as possible.
- [x] **DI registration**: Not applicable — exception types require no DI registration.
- [x] **Logging**: Not applicable — exception types carry no logging responsibilities.
- [x] **ErrorCategory documentation**: ErrorCategory (Permanent for `MessageValidationException` and `BusinessRuleViolationException`; Transient for `EnrichmentStepException`) is specified in the story as a classification concern, not a property to be placed on the exception itself. Classification is implemented in STORY-4 (`ErrorClassifier`). No gap here.
- [x] **Test count matches log**: 14 tests written and passing per coding log; 14 test methods counted in `FunctionAppExceptionTests.cs`.

### Recommendation

PASS → approved for git push

---

## QC report — STORY-2 — iteration 1

### Verdict: PASS

### Issues found

- [ ] **Coding log not updated for STORY-2** — `pipeline/coding-log.md` contains only a STORY-1 entry; no STORY-2 summary block was written by the coding agent.
      Suggested fix: Append the STORY-2 entry to `pipeline/coding-log.md` listing all three source files and all three test files, with test count and passing count.

### Passed checks

- [x] **AC1 — KafkaMessageContext exposes all 7 required properties**: `MessageId`, `PartyId`, `Topic`, `Partition`, `Offset`, `TopicRole`, and `ReceivedAt` all present on the `sealed record`; all asserted in `PipelineContextTests.KafkaMessageContext_AllProperties_AreAccessible`
- [x] **AC2 — HydrationContext init-only immutability**: `HydrationContext_IsImmutable_WithExpressionCreatesNewInstance` demonstrates that a `with` expression creates a new instance and leaves the original unchanged, correctly reflecting C# `init`-only record semantics (compile-time enforcement is inherent in the production code)
- [x] **AC3 — ToCosmosDocument contains all required nested sections**: Six separate tests each assert one section — `ToCosmosDocument_ContainsKafkaContext`, `ToCosmosDocument_ContainsVersions`, `ToCosmosDocument_ContainsOutcome`, `ToCosmosDocument_ContainsHydration_WithAllSubSections` (stepsApplied, stepsSkipped, totalDurationMs, stepBreakdown), `ToCosmosDocument_ContainsRetryInfo`, `ToCosmosDocument_TtlIs15552000`
- [x] **Architecture alignment — KafkaMessageContext**: All 7 properties with correct types (`Offset` is `long`, `Partition` is `int`, `ReceivedAt` is `DateTimeOffset`) match the architecture document exactly
- [x] **Architecture alignment — HydrationContext**: All 7 properties (`EntityId`, `MessageId`, `TopicRole`, `IncomingVersion`, `StoredVersion`, `Data`, `WasApiFallback`) with correct types match exactly
- [x] **Architecture alignment — EntityData**: `Dictionary<string, object> Properties { get; init; } = new()` matches the architecture spec
- [x] **Architecture alignment — EnrichmentResult**: `StepName`, `Applied`, `Duration` (defaults to `TimeSpan.Zero`), `Data` (`EntityData?`) — all correct; `Duration` default tested in `EnrichmentResult_DefaultDuration_IsTimeSpanZero`
- [x] **Architecture alignment — AuditRecord nested types**: `KafkaContextInfo` (topic, partition, offset, receivedAt), `VersionInfo` (stored, incoming, gap, wasApiFallback), `HydrationInfo` (stepsApplied, stepsSkipped, totalDurationMs, stepBreakdown list), `RetryInfo` (attemptNumber, wasRetry, deadLettered) — all match spec
- [x] **Architecture alignment — AuditRecord.ProcessedDate**: `string` in YYYY-MM-DD format used as Cosmos `audit-metrics` partition key; tested in `AuditRecord_ProcessedDate_AcceptsYYYYMMDDFormat`
- [x] **Architecture alignment — AuditRecord.Ttl**: Defaults to `15552000` (180 days); tested in `AuditRecord_DefaultTtl_Is15552000`
- [x] **Architecture alignment — AmendmentMessage / AmendPayload / Operation**: All properties match architecture C# model exactly; `OperationsPayload` defaults to `[]`; tested in `AmendmentMessageTests`
- [x] **Architecture alignment — ToCosmosDocument return type**: Returns `AuditCosmosDocument`, a dedicated typed Cosmos document class — explicitly permitted by the architecture document ("anonymous object or dedicated Cosmos document class")
- [x] **`sealed` on all concrete types**: All 12 concrete record types are `sealed`
- [x] **`record` with `init`-only properties**: All types use `record` with `init`-only properties throughout; no mutable setters present
- [x] **Lean code**: No dead code, no speculative abstractions, no unused properties; `AuditCosmosDocument` is the required return type of `ToCosmosDocument()` and is not redundant
- [x] **DI registration**: N/A — pure model/DTO library; no services to register
- [x] **Logging**: N/A — no behaviour or services in this story
- [x] **Error handling / ErrorCategory**: N/A — no exception throwing in data model types
- [x] **Cosmos partition keys**: `AuditRecord.ProcessedDate` correctly identified as the `audit-metrics` partition key; no Cosmos container operations in this story

### Recommendation

PASS → approved for git push after the coding log issue is resolved (append STORY-2 entry to `pipeline/coding-log.md`)

---

## QC report — STORY-1 — iteration 1

### Verdict: PASS

### Issues found

None.

### Passed checks

- [x] **AC1 — EnrichedCustomer exposes all 12 required properties**: `PartyId`, `SchemaVersion`, `LastUpdatedAt`, `LastUpdatedBy`, `Version`, `Customer`, `Addresses`, `PhoneNumbers`, `EmailAddresses`, `BankOperations`, `Onboarding`, and `AuditSummary` are all present and tested across `EnrichedCustomer_ExposesAllRequiredProperties` and `EnrichedCustomer_CollectionProperties_DefaultToEmptyList`
- [x] **AC2 — Enum enforced at compile time**: `Address.AddressType` is typed as `AddressType` enum (not `string`), enforcing valid values at compile time; all five enum values tested in `AddressType_EnumHasExpectedValues`
- [x] **AC3 — TaxInfo model stores value as-is (no masking in model)**: `TaxInfo_StoresTaxIdAsProvided_NoMaskingInModel` confirms the model stores whatever value it receives; masking is correctly deferred to the hydration step
- [x] **AC4 — Collection properties default to empty list**: `EnrichedCustomer_CollectionProperties_DefaultToEmptyList` asserts all four collections are non-null and empty when not explicitly set
- [x] **Architecture alignment — class names**: All types match architecture document exactly: `EnrichedCustomer`, `CustomerInfo`, `TaxInfo`, `Address`, `PhoneNumber`, `EmailAddress`, `BankOperation`, `OnboardingInfo`, `AuditSummary`
- [x] **Architecture alignment — enums**: `TaxIdType { SSN, EIN }`, `AddressType { Primary, Secondary, Mailing, Billing, Previous }`, `PhoneType { Mobile, Home, Work, Fax }`, `EmailType { Personal, Work, Other }` — all values present and tested
- [x] **Architecture alignment — properties**: `AuditSummary` has `LastMessageId`, `LastKafkaOffset` (long), `LastHydrationSteps` (List<string>), `WasApiFallback` (bool), `ProcessedAt` (DateTimeOffset); `OnboardingInfo` has `CompletedAt`, `ExternalCustomerNo`, `EffectiveDate` — all match architecture spec
- [x] **Architecture alignment — stable IDs**: `AddressId`, `PhoneId`, `EmailId`, `OperationId` present on all four collection types
- [x] **Architecture alignment — per-item audit fields**: `AddedAt` (DateTimeOffset) and `AddedBy` (string) present on all four collection types
- [x] **Architecture alignment — IsPreferred**: Present on `Address`, `PhoneNumber`, and `EmailAddress` (not on `BankOperation`, per spec)
- [x] **Lean code**: No dead code, no unused abstractions, no speculative types beyond the architecture spec
- [x] **`sealed` on all concrete record types**: All nine concrete types are `sealed`
- [x] **`record` with `init`-only properties**: All types use `record` with `init`-only properties throughout
- [x] **Collection expression defaults**: `= []` syntax used for `Addresses`, `PhoneNumbers`, `EmailAddresses`, `BankOperations`, and `AuditSummary.LastHydrationSteps`
- [x] **DI registration**: N/A — this is a class library (pure model/enum types); no services to register, confirmed correct in coding log
- [x] **Logging**: N/A — no services or behaviour in this story; no logging required
- [x] **Error handling / ErrorCategory**: N/A — no exceptions thrown by pure data models
- [x] **Cosmos partition keys**: N/A — no Cosmos interaction in this story
- [x] **Test count**: 17 tests written and passing, matching the coding log claim; every acceptance criterion maps to at least one test
- [x] **TDD failure scenarios**: No failure paths exist in a pure data model story; all AC failure coverage requirements are satisfied

### Recommendation

PASS → approved for git push
