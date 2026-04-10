---
phase: 12-il-token-resolution-search-enrichment-truncation-reporting
plan: 01
subsystem: disassembly
tags: [il-output, resolve-deep, type-expansion, disassembly]
dependency_graph:
  requires: []
  provides: [resolveDeep-parameter, il-type-expansion]
  affects: [disassemble_method, disassemble_type]
tech_stack:
  added: []
  patterns: [regex-post-processing, IL-type-abbreviation-expansion]
key_files:
  created: []
  modified:
    - Domain/Services/IDisassemblyService.cs
    - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
    - Application/UseCases/DisassembleMethodUseCase.cs
    - Application/UseCases/DisassembleTypeUseCase.cs
    - Transport/Mcp/Tools/DisassembleMethodTool.cs
    - Transport/Mcp/Tools/DisassembleTypeTool.cs
    - Tests/Tools/DisassembleMethodToolTests.cs
    - Tests/Tools/DisassembleTypeToolTests.cs
    - Tests/Tools/NativeDllGuardTests.cs
decisions:
  - Regex-based post-processing on PlainTextOutput string rather than custom ITextOutput wrapper to minimize coupling to ICSharpCode.Decompiler internals
  - Line-by-line processing to avoid expanding types inside comments or opcode names
  - string/object handled separately with stricter boundary matching to avoid false positives inside identifiers
metrics:
  duration: 5m
  completed: "2026-04-10T11:43:50Z"
  tasks_completed: 1
  tasks_total: 1
  files_modified: 9
requirements_completed: [IL-01, IL-02, IL-03]
---

# Phase 12 Plan 01: IL Token Resolution - resolveDeep Parameter Summary

Add resolveDeep flag to disassemble tools with regex-based IL type abbreviation expansion (string->System.String, int32->System.Int32, etc.) for agent-readable fully-qualified output.

## What Changed

### Task 1: Add resolveDeep parameter through all layers and implement deep resolution post-processing (TDD)

**RED:** Added 5 new failing tests across DisassembleMethodToolTests (3) and DisassembleTypeToolTests (2) verifying resolveDeep parameter existence and IL type expansion behavior.

**GREEN:** Implemented resolveDeep parameter through the full layered architecture:

1. **Domain/Services/IDisassemblyService.cs** -- Added `bool resolveDeep = false` to both `DisassembleTypeAsync` and `DisassembleMethodAsync` signatures
2. **Infrastructure/Decompiler/ILSpyDisassemblyService.cs** -- Added `resolveDeep` parameter to both methods, implemented `ApplyDeepResolution()` private helper that:
   - Expands IL type abbreviations to FQNs (string->System.String, int32->System.Int32, bool->System.Boolean, etc.)
   - Handles string/object with stricter boundary matching to avoid false positives
   - Processes line-by-line, skipping comment lines
   - Uses regex with lookbehind/lookahead to match only in operand contexts
3. **Application/UseCases** -- Both DisassembleMethodUseCase and DisassembleTypeUseCase pass resolveDeep through to the domain service
4. **Transport/Mcp/Tools** -- Both tools expose resolveDeep with Description attribute for MCP schema
5. **Tests** -- Fixed all existing test call sites for new parameter position (CancellationToken now needs named arg)

**Commit:** ce6c436 (RED), 0f14276 (GREEN)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed existing test call sites for new parameter position**
- **Found during:** GREEN phase build
- **Issue:** Adding `bool resolveDeep` before `CancellationToken` in tool signatures broke all existing test calls that passed CancellationToken positionally
- **Fix:** Added `cancellationToken:` named parameter to all affected test calls in DisassembleMethodToolTests, DisassembleTypeToolTests, and NativeDllGuardTests
- **Files modified:** Tests/Tools/DisassembleMethodToolTests.cs, Tests/Tools/DisassembleTypeToolTests.cs, Tests/Tools/NativeDllGuardTests.cs
- **Commit:** 0f14276

## Test Results

- 21/21 disassemble tests pass (12 method + 9 type)
- 228/230 full suite pass (2 pre-existing failures in AnalyzeAssemblyToolTests and FindInstantiationsToolTests, unrelated)

## Decisions Made

1. **Regex post-processing over custom ITextOutput**: Chose regex-based string post-processing on the PlainTextOutput result to minimize coupling to ICSharpCode.Decompiler internals. This is O(n) on output length and easily maintainable.
2. **Line-by-line with comment skip**: Process each line individually and skip comment lines to avoid expanding type names inside the summary header comments.
3. **Strict boundary matching for string/object**: Used `[\s\(\[,]` lookbehind and `[\s\)\],]` lookahead for `string` and `object` to prevent matching inside identifiers like `StringBuilder`.

## Self-Check: PASSED
