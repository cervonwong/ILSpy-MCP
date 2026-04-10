---
phase: 10-find-tool-pagination-match-enrichment
plan: 02
subsystem: find_dependencies
tags: [pagination, enrichment, type-forward-resolution, fail-soft]
dependency_graph:
  requires: [10-01]
  provides: [PAGE-02, OUTPUT-02]
  affects: [FindDependenciesTool, FindDependenciesUseCase, ILSpyCrossReferenceService, DependencyResult]
tech_stack:
  added: []
  patterns: [type-forward-resolution, fail-soft-degradation, resolver-cache, pagination-envelope]
key_files:
  created:
    - TestTargets/Types/PaginationTestTargetsDependencies.cs
  modified:
    - Domain/Models/CrossReferenceResult.cs
    - Infrastructure/Decompiler/ILSpyCrossReferenceService.cs
    - Application/UseCases/FindDependenciesUseCase.cs
    - Transport/Mcp/Tools/FindDependenciesTool.cs
    - Transport/Mcp/Tools/AnalyzeReferencesTool.cs
    - Tests/Tools/FindDependenciesToolTests.cs
decisions:
  - Deep-resolution helpers (ResolveDefiningAssembly, ChaseTypeForward) kept inside ILSpyCrossReferenceService rather than pushed to ICrossAssemblyService, because they operate on the same MetadataReader + EntityHandle context as ScanILForDependencies and need the per-scan resolver cache
  - Fixture uses 115 distinct framework references (padded beyond 105 minimum) to ensure stable pagination boundary even if deduplication removes a few
metrics:
  duration: 5m 20s
  completed: 2026-04-10T06:10:47Z
  tasks_completed: 3
  tasks_total: 3
  tests_added: 10
  tests_total_passing: 203
  files_changed: 7
requirements_addressed: [PAGE-02, OUTPUT-02]
---

# Phase 10 Plan 02: find_dependencies Pagination + Match Enrichment Summary

Deep type-forward resolution with fail-soft semantics, flat sorted per-line output with DefiningAssembly brackets, and Phase 9 pagination contract for find_dependencies.

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | a2cc129 | Extend DependencyResult with DefiningAssembly and ResolutionNote |
| 2 | 804eb33 | Implement ResolveDefiningAssembly + ChaseTypeForward in service |
| 3 | 4b85811 | Pagination, flat layout, fixture, tests, tool parameter surface |

## What Changed

### Domain Model (Task 1)
`DependencyResult` record in `Domain/Models/CrossReferenceResult.cs` gained two new properties:
- `public required string DefiningAssembly { get; init; }` -- terminal assembly name after type-forward resolution
- `public string? ResolutionNote { get; init; }` -- populated only on fail-soft degradation

Other records (`UsageResult`, `ImplementorResult`, `InstantiationResult`) and enums unchanged.

### Deep Type-Forward Resolution (Task 2)
Two new private helpers in `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs`:

- **`ResolveDefiningAssembly`** -- Extracts TypeReference from a MemberReference/TypeReference handle, reads the AssemblyReference scope, checks cache, delegates to ChaseTypeForward. Returns the assembly definition name for MethodDefinition/FieldDefinition handles (same-assembly references).

- **`ChaseTypeForward`** -- Loads sibling assemblies from `Path.GetDirectoryName(assemblyPath)` via PEFile, scans ExportedType rows for matching namespace+name, follows forward chain up to 5 hops with cycle detection via HashSet. Returns terminal assembly name on success, immediate name with descriptive note on failure.

`ScanILForDependencies` signature extended with `assemblyDirectory` and `resolverCache` parameters. `FindDependenciesAsync` passes `Path.GetDirectoryName(assemblyPath.Value)` and a fresh `Dictionary<string, (string, string?)>`.

### Output Format (Task 3)
**Before (grouped headers):**
```
Dependencies of MyType: 5 found

MethodCall:
  System.String.Concat
  System.Console.WriteLine
FieldAccess:
  System.String.Empty
```

**After (flat sorted per-line with assembly brackets):**
```
Dependencies of MyType: 5 found (showing 1-5)
  [FieldAccess] System.String.Empty [System.Private.CoreLib]
  [MethodCall] System.Console.WriteLine [System.Private.CoreLib]
  [MethodCall] System.String.Concat [System.Private.CoreLib]

[pagination:{"total":5,"returned":5,"offset":0,"truncated":false,"nextOffset":null}]
```

Sort order: Kind ascending (enum order), then TargetMember ascending (StringComparer.Ordinal).

### Pagination Surface (Task 3)
- `FindDependenciesTool.ExecuteAsync` gains `int maxResults = 100` and `int offset = 0`
- Ceiling rejection: maxResults > 500 or <= 0 throws McpToolException("INVALID_PARAMETER")
- `FindDependenciesUseCase.ExecuteAsync` performs slice via `.Skip(offset).Take(maxResults)` and calls `PaginationEnvelope.AppendFooter`
- Service interface `ICrossReferenceService.FindDependenciesAsync` signature UNCHANGED (per D-06)

### Test Fixture (Task 3)
`TestTargets/Types/PaginationTestTargetsDependencies.cs` declares `DependencyKitchenSink` with 115 methods each referencing distinct framework members (Console, String, Math, DateTime, Guid, Convert, Path, StringBuilder, Environment, Array, List, Dictionary, Type, File, Directory, Encoding, BitConverter, GC, Thread, Stopwatch, StringComparer, Random).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed AnalyzeReferencesTool call site**
- **Found during:** Task 3
- **Issue:** `AnalyzeReferencesTool.cs` line 53 called `_dependenciesUseCase.ExecuteAsync(assemblyPath, typeName, memberName, cancellationToken)` which broke because the 4th positional argument now matches the `int maxResults` parameter instead of CancellationToken.
- **Fix:** Changed to `cancellationToken: cancellationToken` (named argument).
- **Files modified:** Transport/Mcp/Tools/AnalyzeReferencesTool.cs
- **Commit:** 4b85811

**2. [Rule 3 - Blocking] Fixed existing test call sites**
- **Found during:** Task 3
- **Issue:** All 4 existing `FindDependenciesToolTests` methods passed `CancellationToken.None` as the 4th positional argument, which now matches `int maxResults`.
- **Fix:** Changed all to `cancellationToken: CancellationToken.None` (named argument).
- **Files modified:** Tests/Tools/FindDependenciesToolTests.cs
- **Commit:** 4b85811

## Threat-Model Compliance

| Threat | Status | Evidence |
|--------|--------|----------|
| T-10-02-01 (unbounded forward chain) | Mitigated | `maxHops=5` parameter + `visited` HashSet cycle detection in ChaseTypeForward |
| T-10-02-02 (directory traversal) | Mitigated | Lookup root is `Path.GetDirectoryName(assemblyPath.Value)` only; no separate user-supplied directory |
| T-10-02-03 (path disclosure in notes) | Mitigated | All note assignments are constant string literals; `grep 'note = \$"'` returns zero hits |
| T-10-02-04 (DoS via re-resolution) | Mitigated | `resolverCache` Dictionary keyed by immediate AssemblyReference name deduplicates lookups |
| T-10-02-05 (large result set) | Mitigated | Ceiling rejection block (maxResults > 500) + service-level seen HashSet |
| T-10-02-07 (corrupt PE file) | Mitigated | try/catch for MetadataFileNotSupportedException, BadImageFormatException, and catch-all |

## Fail-Soft Observations

In the test fixture run, most framework references resolve successfully to `System.Private.CoreLib` (since the test assembly's output directory contains the .NET runtime assemblies). The fail-soft path (ResolutionNote populated) would trigger for assemblies not present in the output directory. This is the expected behavior -- the resolution works when assemblies are available and degrades gracefully otherwise.

## Known Stubs

None -- all data paths are wired and functional.

## Self-Check: PASSED

- All 7 key files verified present on disk
- All 3 task commits verified in git log (a2cc129, 804eb33, 4b85811)
- dotnet build: 0 errors
- dotnet test: 203 passed, 0 failed (14 FindDependencies-specific)
