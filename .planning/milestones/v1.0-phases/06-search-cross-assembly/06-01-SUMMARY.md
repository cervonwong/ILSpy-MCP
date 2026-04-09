---
phase: 06-search-cross-assembly
plan: 01
subsystem: search
tags: [il-scanning, string-search, constant-search, mcp-tools]
dependency_graph:
  requires: [04-cross-reference-analysis]
  provides: [search_strings tool, search_constants tool, ISearchService interface]
  affects: [Program.cs, ToolTestFixture.cs]
tech_stack:
  added: []
  patterns: [IL bytecode scanning via BlobReader, ldstr/ldc opcode extraction, paginated search results]
key_files:
  created:
    - Domain/Models/SearchResult.cs
    - Domain/Services/ISearchService.cs
    - Infrastructure/Decompiler/ILSpySearchService.cs
    - Application/UseCases/SearchStringsUseCase.cs
    - Application/UseCases/SearchConstantsUseCase.cs
    - Transport/Mcp/Tools/SearchStringsTool.cs
    - Transport/Mcp/Tools/SearchConstantsTool.cs
    - TestTargets/Types/SearchTargets.cs
    - Tests/Tools/SearchStringsToolTests.cs
    - Tests/Tools/SearchConstantsToolTests.cs
  modified:
    - Program.cs
    - Tests/Fixtures/ToolTestFixture.cs
decisions:
  - Duplicated IL scanning helpers from ILSpyCrossReferenceService to avoid coupling between services
  - Regex validation happens before Task.Run IL scan to fail fast on invalid patterns
  - Integer-only constant search (ldc.i4/ldc.i8) — no float/double per requirement
requirements-completed: [SRCH-01, SRCH-02]
metrics:
  duration: 6m
  completed: "2026-04-08T09:29:54Z"
  tasks_completed: 2
  tasks_total: 2
  test_count: 13
  total_tests: 95
---

# Phase 06 Plan 01: String and Constant Search Summary

IL bytecode search tools scanning ldstr for string literals by regex and ldc.i4/ldc.i8 for integer constants by exact value, with paginated results including method context.

## What Was Built

### Domain Layer
- `SearchResults<T>` generic paginated wrapper with TotalCount, Offset, Limit
- `StringSearchResult` record: MatchedValue, DeclaringType, MethodName, MethodSignature, ILOffset
- `ConstantSearchResult` record: MatchedValue, ConstantType, DeclaringType, MethodName, MethodSignature, ILOffset
- `ISearchService` interface with SearchStringsAsync and SearchConstantsAsync

### Infrastructure Layer
- `ILSpySearchService` — IL scanning implementation using BlobReader
  - String search: scans for `ILOpCode.Ldstr`, reads UserStringHandle, matches against compiled Regex
  - Constant search: handles Ldc_i4_m1 through Ldc_i4_8 (implicit), Ldc_i4_s (sbyte), Ldc_i4 (int32), Ldc_i8 (int64)
  - Duplicated IL helper methods from ILSpyCrossReferenceService (ReadILOpCode, SkipOperand, GetOperandSize, etc.)

### Application Layer
- `SearchStringsUseCase` — validates path, wraps in concurrency limiter + timeout, formats results
- `SearchConstantsUseCase` — same pattern for constant search

### Transport Layer
- `search_strings` MCP tool — regex pattern parameter, INVALID_PATTERN error for bad regex
- `search_constants` MCP tool — exact long value parameter

### Testing
- `SearchTargets.cs` TestTargets with known strings ("Hello, World!", URLs, error messages) and constants (42, -1, 1234567890L, 255, 0)
- 7 SearchStringsToolTests: exact match, regex match, multiple matches, no match, pagination, invalid regex, invalid assembly
- 6 SearchConstantsToolTests: exact constant, negative, large, zero, unused value, invalid assembly

## Commits

| # | Hash | Message |
|---|------|---------|
| 1 | 5d11fd2 | feat(06-01): add search domain models, ISearchService interface, and ILSpySearchService |
| 2 | 903dc4c | feat(06-01): add search_strings and search_constants MCP tools with tests |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed substring match in test assertions**
- **Found during:** Task 2
- **Issue:** Tests using `.NotContain("0 total matches")` failed because "20 total matches" contains "0 total matches" as a substring
- **Fix:** Changed to `.NotStartWith()` pattern to avoid false substring matches
- **Files modified:** Tests/Tools/SearchStringsToolTests.cs, Tests/Tools/SearchConstantsToolTests.cs
- **Commit:** 903dc4c

## Verification Results

- `dotnet build`: 0 errors, 2 warnings (pre-existing, unrelated)
- `dotnet test --filter Search*`: 13/13 passed
- `dotnet test` (full suite): 95/95 passed, 0 failures
- MCP tool count: 17 tools total (15 existing + 2 new search tools)

## Known Stubs

None — all search tools are fully wired with real IL scanning implementation.

## Self-Check: PASSED
