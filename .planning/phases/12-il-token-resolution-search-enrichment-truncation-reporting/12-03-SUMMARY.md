---
phase: 12-il-token-resolution-search-enrichment-truncation-reporting
plan: 03
subsystem: application/pagination, application/use-cases
tags: [truncation, structured-output, pagination, dos-mitigation]
dependency_graph:
  requires:
    - 12-01 (resolveDeep parameter on disassemble use cases)
  provides:
    - TruncationEnvelope helper for structured truncation footers
    - Structured [truncation:{...}] footer on all 7 bounded-output tools
    - MaxDecompilationSize enforcement on disassemble tools
    - maxDisplayTypes=200 cap on analyze_assembly public types listing
  affects:
    - DecompileTypeUseCase, DecompileMethodUseCase
    - DisassembleTypeUseCase, DisassembleMethodUseCase
    - DecompileNamespaceUseCase, ExportProjectUseCase, AnalyzeAssemblyUseCase
    - SecurityAndRobustnessTests
tech_stack:
  added: []
  patterns:
    - TruncationEnvelope static helper alongside PaginationEnvelope
    - TruncateSource helper for byte-cap + line-count computation
key_files:
  created:
    - Application/Pagination/TruncationEnvelope.cs
  modified:
    - Application/UseCases/DecompileTypeUseCase.cs
    - Application/UseCases/DecompileMethodUseCase.cs
    - Application/UseCases/DisassembleTypeUseCase.cs
    - Application/UseCases/DisassembleMethodUseCase.cs
    - Application/UseCases/DecompileNamespaceUseCase.cs
    - Application/UseCases/ExportProjectUseCase.cs
    - Application/UseCases/AnalyzeAssemblyUseCase.cs
    - Tests/Tools/DecompileTypeToolTests.cs
    - Tests/Tools/DecompileMethodToolTests.cs
    - Tests/Tools/DisassembleTypeToolTests.cs
    - Tests/Tools/DisassembleMethodToolTests.cs
    - Tests/Tools/DecompileNamespaceToolTests.cs
    - Tests/Tools/ExportProjectToolTests.cs
    - Tests/Tools/AnalyzeAssemblyToolTests.cs
    - Tests/Security/SecurityAndRobustnessTests.cs
decisions:
  - "TruncationEnvelope uses line counts (totalLines, returnedLines) as the unit for source-returning tools -- agents reason about lines, not bytes"
  - "Footer always present even when not truncated (truncated:false) for consistent machine parsing"
  - "Disassemble tools now inject IOptions<ILSpyOptions> for MaxDecompilationSize enforcement (previously unbounded)"
  - "ExportProjectUseCase captures totalTypeCount from PE metadata inside Task.Run lambda to avoid re-opening the PE file"
  - "AnalyzeAssemblyUseCase caps at 200 displayed types with '... and N more types' overflow message"
metrics:
  duration: 10m
  completed: 2026-04-10
  tasks_completed: 2
  tasks_total: 2
  files_touched: 16
requirements_completed:
  - PAGE-07
  - PAGE-08
---

# Phase 12 Plan 03: Truncation Reporting Summary

Structured JSON truncation footers on all 7 bounded-output tools, replacing ad-hoc text messages with machine-parseable [truncation:{...}] format and adding byte-cap enforcement to previously unbounded disassemble tools.

## What Changed

### Task 1: TruncationEnvelope helper + 5 source-returning tools
- **Created** `Application/Pagination/TruncationEnvelope.cs` with `AppendSourceFooter`, `AppendExportFooter`, `AppendAnalysisFooter`, and `TruncateSource` static methods
- **DecompileTypeUseCase / DecompileMethodUseCase**: Replaced `[Output truncated at N bytes...]` ad-hoc message with `TruncationEnvelope.TruncateSource` + `AppendSourceFooter`
- **DisassembleTypeUseCase / DisassembleMethodUseCase**: Added `IOptions<ILSpyOptions>` constructor parameter for MaxDecompilationSize enforcement (previously had no truncation at all -- T-12-04 mitigated). Added truncation + footer.
- **DecompileNamespaceUseCase**: Replaced ad-hoc byte truncation with `TruncationEnvelope.TruncateSource` + `AppendSourceFooter`. Footer always present.
- **Tests**: 5 new tests verifying `[truncation:{...}]` footer presence on each tool

### Task 2: export_project and analyze_assembly
- **ExportProjectUseCase**: Captures `totalTypeCount` from `peFile.Metadata.TypeDefinitions.Count` inside the `Task.Run` lambda. `FormatOutput` now accepts `totalTypeCount` and `maxTypes`, appends `TruncationEnvelope.AppendExportFooter`.
- **AnalyzeAssemblyUseCase**: Added `maxDisplayTypes = 200` cap on the "Key Public Types" section using `.Take(200)`. Appends `TruncationEnvelope.AppendAnalysisFooter` with `totalPublicTypes`, `displayedTypes`, `truncated` fields.
- **Tests**: 2 new tests verifying truncation footer on export_project and analyze_assembly

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated SecurityAndRobustnessTests for new truncation format**
- **Found during:** Task 1 verification
- **Issue:** `DecompileType_SmallMaxSize_TruncatesOutput` test checked for old `[Output truncated at 100 bytes` string which no longer exists
- **Fix:** Updated assertion to check for `[truncation:` and `"truncated":true`
- **Files modified:** Tests/Security/SecurityAndRobustnessTests.cs
- **Commit:** 41f8693

### Pre-existing Failures (Out of Scope)

Two pre-existing test failures exist that are NOT caused by this plan's changes:
1. `AnalyzeAssemblyToolTests.AnalyzeAssembly_ShowsNamespaces` -- Test data change (Pagination.Implementors namespace dominates, Animals namespace not in NamespaceCounts output). Confirmed failing before any plan 03 changes.
2. `FindInstantiationsToolTests.FindInstantiations_Enrichment_ShowsFqnMethodSignatureAndIlOffset` -- Pre-existing from wave 1 changes. Confirmed failing before any plan 03 changes.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Line counts as truncation unit | Agents reason about "how many lines did I get" not "how many bytes" |
| Footer always present (truncated:false) | Consistent with pagination contract -- agents always parse the same shape |
| IOptions<ILSpyOptions> on disassemble use cases | Follows established pattern from decompile use cases; DI resolves automatically |
| totalTypeCount from PE metadata | TypeDefinitions.Count is the canonical source; avoids counting exported types post-hoc |

## Threat Mitigations

| Threat ID | Mitigation |
|-----------|------------|
| T-12-04 (DoS via unbounded disassemble output) | DisassembleTypeUseCase and DisassembleMethodUseCase now enforce MaxDecompilationSize byte cap |
| T-12-05 (DoS via large public type listing) | AnalyzeAssemblyUseCase caps displayed types at 200 |

## Self-Check: PASSED

All created/modified files exist. All commits verified.
