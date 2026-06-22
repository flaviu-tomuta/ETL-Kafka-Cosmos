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
