---
phase: 14-v1.2.0-gap-closure-sweep
plan: 04
subsystem: Application/UseCases (pagination conformance)
tags: [pagination, truncation, PAGE-07, PAGE-08, gap-closure]
requires:
  - Application/Pagination/PaginationEnvelope.cs
  - Application/Configuration/ILSpyOptions.cs
provides:
  - Canonical [pagination:{...}] footer on decompile_type, decompile_method, disassemble_type, disassemble_method, export_project, analyze_assembly
affects:
  - DecompileTypeUseCase
  - DecompileMethodUseCase
  - DisassembleTypeUseCase
  - DisassembleMethodUseCase
  - ExportProjectUseCase
  - AnalyzeAssemblyUseCase
  - Tests/Security/SecurityAndRobustnessTests.cs
tech-stack:
  added: []
  patterns:
    - "PaginationEnvelope.AppendFooter with byte-count semantics for source-returning tools"
    - "PaginationEnvelope.AppendFooter with type-count semantics for export_project (total=assembly types, returned=files)"
    - "IOptions<ILSpyOptions> injection into disassemble/analyze use cases for MaxDecompilationSize cap"
key-files:
  created: []
  modified:
    - Application/UseCases/DecompileTypeUseCase.cs
    - Application/UseCases/DecompileMethodUseCase.cs
    - Application/UseCases/DisassembleTypeUseCase.cs
    - Application/UseCases/DisassembleMethodUseCase.cs
    - Application/UseCases/ExportProjectUseCase.cs
    - Application/UseCases/AnalyzeAssemblyUseCase.cs
    - Tests/Security/SecurityAndRobustnessTests.cs
decisions:
  - "AnalyzeAssemblyUseCase reuses MaxDecompilationSize as the byte cap rather than introducing a new option — keeps one configurable knob for all source/summary-returning tools"
  - "ExportProjectUseCase re-reads PE metadata inside the Task.Run lambda finalisation rather than threading the TypeDefinitions count out of the decompiler call — avoids use-after-dispose on peFile and keeps footer semantics exact (total=types in assembly)"
  - "PaginationEnvelope.AppendFooter is invoked unconditionally (even when not truncated) so the footer is always present per docs/PAGINATION.md contract"
metrics:
  duration: ~6 min
  tasks: 2
  files: 7
  completed: 2026-04-12
requirements-completed: [PAGE-07, PAGE-08]
---

# Phase 14 Plan 04: Canonical Truncation Footer in Bounded-Output Tools Summary

One-liner: Replace free-form "[Output truncated at N bytes. The full output is M bytes.]" strings in four source-returning use cases with the canonical `[pagination:{...}]` footer, and add the same footer to export_project (type-count semantics) and analyze_assembly (byte-count semantics), satisfying PAGE-07 and PAGE-08 across all six bounded-output tools.

## What Was Built

All six bounded-output use cases now emit the canonical pagination footer defined in docs/PAGINATION.md:

- `decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method` — byte-count semantics: `total=full bytes`, `returned=included bytes`, `offset=0`; footer marks `truncated=true` when body exceeds `MaxDecompilationSize`.
- `export_project` — type-count semantics: `total=TypeDefinitions.Count` from the assembly, `returned=result.SourceFiles.Count`, `offset=0`.
- `analyze_assembly` — byte-count semantics identical to decompile_*; body capped at `MaxDecompilationSize` with canonical footer appended unconditionally.

`DisassembleTypeUseCase`, `DisassembleMethodUseCase`, and `AnalyzeAssemblyUseCase` now inject `IOptions<ILSpyOptions>` to access `MaxDecompilationSize` (mirrors `DecompileTypeUseCase` pattern).

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Replace free-form truncation string with canonical footer in decompile_type, decompile_method, disassemble_type, disassemble_method | 24302c1 | DecompileTypeUseCase.cs, DecompileMethodUseCase.cs, DisassembleTypeUseCase.cs, DisassembleMethodUseCase.cs, SecurityAndRobustnessTests.cs |
| 2 | Add canonical footer to export_project (type-count semantics) and analyze_assembly (byte-count semantics) | 7b2560a | ExportProjectUseCase.cs, AnalyzeAssemblyUseCase.cs |

## Verification

- `grep -n "PaginationEnvelope.AppendFooter"` finds 1 match in each of the six use case files.
- `grep -n "Output truncated at"` returns no matches across the six use case files.
- `DisassembleTypeUseCase` and `DisassembleMethodUseCase` contain `IOptions<ILSpyOptions>` in constructor; `AnalyzeAssemblyUseCase` likewise.
- `dotnet build ILSpy.Mcp.sln` exits 0 (2 pre-existing warnings in TestTargets unrelated to this plan).
- Filtered tests: Decompile|Disassemble — 28 passed; ExportProject|AnalyzeAssembly — 11 passed.
- Full suite: 234 passed / 0 failed / 0 skipped.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated SecurityAndRobustnessTests.DecompileType_SmallMaxSize_TruncatesOutput assertion**
- **Found during:** Task 1 verification (test referenced literal `[Output truncated at 100 bytes`)
- **Issue:** Existing test asserted the old free-form truncation string which was the subject of this plan's removal.
- **Fix:** Changed assertion to `.Contain("[pagination:").And.Contain("\"truncated\":true")` — asserts canonical footer presence and truncation flag.
- **Files modified:** Tests/Security/SecurityAndRobustnessTests.cs (1 line)
- **Commit:** 24302c1

## Requirements Satisfied

- **PAGE-07** — Bounded-output tools emit canonical truncation footer (decompile_type, decompile_method, disassemble_type, disassemble_method, analyze_assembly).
- **PAGE-08** — export_project emits canonical footer with type-count semantics.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: Application/UseCases/DecompileTypeUseCase.cs (PaginationEnvelope.AppendFooter, no "Output truncated at")
- FOUND: Application/UseCases/DecompileMethodUseCase.cs (PaginationEnvelope.AppendFooter, no "Output truncated at")
- FOUND: Application/UseCases/DisassembleTypeUseCase.cs (PaginationEnvelope.AppendFooter + IOptions<ILSpyOptions>)
- FOUND: Application/UseCases/DisassembleMethodUseCase.cs (PaginationEnvelope.AppendFooter + IOptions<ILSpyOptions>)
- FOUND: Application/UseCases/ExportProjectUseCase.cs (PaginationEnvelope.AppendFooter)
- FOUND: Application/UseCases/AnalyzeAssemblyUseCase.cs (PaginationEnvelope.AppendFooter + IOptions<ILSpyOptions>)
- FOUND: commit 24302c1 (Task 1)
- FOUND: commit 7b2560a (Task 2)
- Full test suite 234/234 passed
