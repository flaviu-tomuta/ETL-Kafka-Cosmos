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
