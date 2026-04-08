---
phase: quick
plan: 260408-flk
subsystem: infrastructure/decompiler
tags: [bug-fix, error-handling, native-dll]
key-files:
  created:
    - Tests/Tools/NativeDllGuardTests.cs
  modified:
    - Domain/Errors/AssemblyLoadException.cs
    - Infrastructure/Decompiler/ILSpyDecompilerService.cs
    - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
decisions:
  - Used kernel32.dll as native PE test fixture (always present on Windows)
  - CrossReference service not modified (does not exist in main branch yet)
metrics:
  duration: 4m
  completed: 2026-04-08
  tasks: 3
  files: 4
---

# Quick Task 260408-flk: Add Native DLL Guard (Check PE CLR Header) Summary

Guard against MetadataFileNotSupportedException when users pass native (non-.NET) DLLs to decompilation/disassembly tools, returning a clear "not a .NET assembly" error message.

## Completed Tasks

| # | Task | Commit | Key Files |
|---|------|--------|-----------|
| 1 | Add custom message constructor to AssemblyLoadException | 7196b4d | Domain/Errors/AssemblyLoadException.cs |
| 2 | Catch MetadataFileNotSupportedException in services | 036d770 | Infrastructure/Decompiler/ILSpyDecompilerService.cs, ILSpyDisassemblyService.cs |
| 3 | Add native DLL guard tests | 0309d3a | Tests/Tools/NativeDllGuardTests.cs |

## What Changed

### AssemblyLoadException (Domain)
Added a second constructor that accepts a custom message string, enabling specific error messages rather than the generic "Failed to load assembly" default.

### ILSpyDecompilerService (Infrastructure)
Added `catch (MetadataFileNotSupportedException)` blocks before the generic `catch (Exception)` in all 7 public methods: DecompileTypeAsync, DecompileMethodAsync, GetTypeInfoAsync, ListTypesAsync, GetAssemblyInfoAsync, FindExtensionMethodsAsync, SearchMembersAsync. Each catch logs a warning and throws AssemblyLoadException with a user-friendly message.

### ILSpyDisassemblyService (Infrastructure)
Added the same catch pattern to both public methods: DisassembleTypeAsync and DisassembleMethodAsync. Also added `using ICSharpCode.Decompiler.Metadata;` import.

### Tests
Three new tests using kernel32.dll (native PE) to verify the guard works through the tool layer (AnalyzeAssemblyTool, DecompileTypeTool, DisassembleTypeTool). All assert McpToolException with error code "ASSEMBLY_LOAD_FAILED" and message containing "not a .NET assembly".

## Deviations from Plan

### Scope Adjustment
**ILSpyCrossReferenceService** was listed in the plan but does not exist in the main branch (it was implemented in a separate worktree for phase 04-01). The catch blocks will need to be added when that branch is merged. This is not a blocker.

## Verification

- Build: succeeded (0 errors)
- Tests: 61/61 passed (58 existing + 3 new)
- No existing tests broken

## Known Stubs

None.
