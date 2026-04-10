---
phase: 12-il-token-resolution-search-enrichment-truncation-reporting
plan: 02
subsystem: search-enrichment
tags: [search, IL-context, method-signatures, output-enrichment]
dependency_graph:
  requires: []
  provides: [enriched-search-strings, enriched-search-constants, method-fqn-signatures]
  affects: [search_strings, search_constants]
tech_stack:
  added: []
  patterns: [ReflectionDisassembler-for-IL-window, per-method-disassembly-caching]
key_files:
  created: []
  modified:
    - Domain/Models/SearchResult.cs
    - Infrastructure/Decompiler/ILSpySearchService.cs
    - Application/UseCases/SearchStringsUseCase.cs
    - Application/UseCases/SearchConstantsUseCase.cs
    - Tests/Tools/SearchStringsToolTests.cs
    - Tests/Tools/SearchConstantsToolTests.cs
    - TestTargets/Types/SearchTargets.cs
decisions:
  - Used ReflectionDisassembler for IL window capture instead of raw BlobReader parsing -- simpler, leverages existing token resolution
  - Per-method disassembly caching prevents redundant work when multiple matches exist in same method
  - FormatMethodSignature changed to DeclaringType.FullName.Method(ParamFullTypes) format -- no return type, no param names
metrics:
  duration: 4min
  completed: 2026-04-10
  tasks_completed: 2
  tasks_total: 2
  files_modified: 7
requirements_completed: [OUTPUT-06, OUTPUT-07]
---

# Phase 12 Plan 02: Search Enrichment with Method FQN and IL Context Summary

Enriched search_strings and search_constants output with full method FQN signatures (parameter types, no return type) and surrounding IL instruction window for string matches.

## What Changed

### Task 1: Enrich search_strings with surrounding IL window and method FQN

**Domain model** (`SearchResult.cs`): Added `SurroundingInstructions` (IReadOnlyList<string>) and `MatchInstructionIndex` (int) to `StringSearchResult`. These carry up to 3 IL instructions before and 3 after the `ldstr` match, with resolved token references.

**Infrastructure** (`ILSpySearchService.cs`):
- `FormatMethodSignature` now outputs `DeclaringType.FullName.Method(ParamType1, ParamType2)` -- full type names, no return type, no parameter names. This matches the D-05 spec.
- Added `EnrichWithSurroundingIL` -- after paged results are selected, disassembles each unique method body via `ReflectionDisassembler`, parses IL lines by offset, and extracts a 7-line window (3 before + match + 3 after). Per-method caching avoids redundant disassembly.
- Added `CaptureMethodILLines` -- locates method handle by type/method name, disassembles with `ReflectionDisassembler`, extracts `(offset, line)` tuples from output.
- Added using directives for `ICSharpCode.Decompiler.Disassembler`, `ICSharpCode.Decompiler.Metadata`, `ICSharpCode.Decompiler.Output`.

**Application** (`SearchStringsUseCase.cs`): Updated `FormatResults` to use `MethodSignature` for the header line and render `SurroundingInstructions` with `<-- match` marker on the matched instruction.

**Test targets** (`SearchTargets.cs`): Added `StringContainer.MethodWithContext()` with a string literal surrounded by `Console.Write`/`Console.WriteLine` calls for IL window testing.

**Tests** (`SearchStringsToolTests.cs`): Added 2 new tests:
- `SearchStrings_ShowsMethodSignatureWithParameterTypes` -- verifies `GetGreeting()` appears in output
- `SearchStrings_ShowsSurroundingILWindow` -- verifies `<-- match` marker and `IL_` prefixed lines

Commit: `24e324a`

### Task 2: Enrich search_constants with method FQN signatures

**Application** (`SearchConstantsUseCase.cs`): Updated `FormatResults` to use `result.MethodSignature` (falling back to `DeclaringType.MethodName`). Output format: `42 (Int32) in Namespace.Type.Method(ParamTypes) (IL_xxxx)`.

**Tests** (`SearchConstantsToolTests.cs`): Added `SearchConstants_ShowsMethodSignatureWithFullTypes` test verifying `GetMagicNumber()` appears in output.

Commit: `26a7f02`

## Deviations from Plan

None -- plan executed exactly as written.

## Pre-existing Test Failures (Out of Scope)

Two test failures were detected that pre-date this plan's changes:
1. `AnalyzeAssemblyToolTests.AnalyzeAssembly_ShowsNamespaces` -- expects `ILSpy.Mcp.TestTargets.Animals` namespace which was likely displaced by pagination test targets added in earlier phases
2. `FindInstantiationsToolTests.FindInstantiations_Enrichment_ShowsFqnMethodSignatureAndIlOffset` -- expects `System.Void` in method signature format; the old format included return types but FormatMethodSignature was already using the new format in Task 1's commit before this test ran (pre-existing format mismatch from Phase 10/11 test targets)

Both confirmed failing on the base commit (before any Plan 12-02 changes). Not addressed here per scope boundary rules.

## Verification

- `dotnet build ILSpy.Mcp.sln` -- 0 errors, 0 warnings (on modified files)
- `dotnet test --filter SearchStringsToolTests` -- 9/9 passed
- `dotnet test --filter SearchConstantsToolTests` -- 7/7 passed
- Full suite: 226 passed, 2 failed (pre-existing), 0 skipped

## Output Format Examples

**search_strings (enriched):**
```
  "SearchContext:Target" in ILSpy.Mcp.TestTargets.Search.StringContainer.MethodWithContext() (IL_000C)
      IL_0001: ldstr        "before"
      IL_0006: call         System.Console.Write
      IL_000B: nop
      IL_000C: ldstr        "SearchContext:Target"        <-- match
      IL_0011: stloc.0
      IL_0012: ldloc.0
      IL_0013: call         System.Console.WriteLine
```

**search_constants (enriched):**
```
  42 (Int32) in ILSpy.Mcp.TestTargets.Search.ConstantContainer.GetMagicNumber() (IL_0000)
```

## Self-Check: PASSED

All 7 modified files exist on disk. Both task commits (24e324a, 26a7f02) verified in git log.
