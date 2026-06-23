# Coding Log

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
