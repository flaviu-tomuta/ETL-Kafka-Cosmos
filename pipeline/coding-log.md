# Coding Log

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
