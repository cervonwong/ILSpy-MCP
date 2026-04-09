---
phase: 02-sdk-upgrades-bug-fixes
plan: 03
subsystem: domain-infrastructure-application
tags: [bug-fix, constructors, type-members, regression-tests]
dependency_graph:
  requires: [02-02]
  provides: [constructor-exposure, constructor-decompilation]
  affects: [TypeInfo-model, MapToTypeInfo, GetTypeMembersUseCase-output]
tech_stack:
  added: []
  patterns: [constructor-filtering, output-formatting]
key_files:
  created: []
  modified:
    - Domain/Models/TypeInfo.cs
    - Infrastructure/Decompiler/ILSpyDecompilerService.cs
    - Application/UseCases/GetTypeMembersUseCase.cs
    - Tests/Tools/GetTypeMembersToolTests.cs
    - Tests/Tools/DecompileMethodToolTests.cs
decisions:
  - Constructors listed before Methods in output to match typical C# class layout
  - Static class constructors (.cctor) display as "private static .cctor()" which is correct IL-level representation
requirements-completed: [SDK-05, TEST-04]
metrics:
  duration: 3m
  completed: "2026-04-07T08:05:00Z"
  tasks: 1
  files: 5
  tests_added: 3
  tests_total: 42
requirements:
  - SDK-05
  - TEST-04
---

# Phase 02 Plan 03: Constructor Exposure and Regression Tests Summary

**One-liner:** Added Constructors collection to TypeInfo model, populated from IsConstructor in MapToTypeInfo, and rendered as "Constructors:" section before "Methods:" in get_type_members output with 3 regression tests.

## What Was Done

### Task 1: Expose constructors in TypeInfo model and decompiler service, add Constructors section to get_type_members output (TDD)

- Added `IReadOnlyList<MethodInfo> Constructors` property to `TypeInfo` record, positioned before `Methods`
- Added `Constructors = type.Methods.Where(m => m.IsConstructor).Select(MapToMethodInfo).ToList()` to `MapToTypeInfo` in `ILSpyDecompilerService`
- Added "Constructors:" output section in `GetTypeMembersUseCase` before "Methods:" section, using same formatting pattern (accessibility, modifiers, parameters)
- `DecompileMethodAsync` already matched `.ctor`/`.cctor` by name -- no change needed
- 3 new tests: constructor listing, constructor ordering before methods, constructor decompilation
- **Commits:** `c875d78` (RED), `87a65ce` (GREEN)

## Verification Results

- `dotnet test` exits 0 with 42 passing tests (39 prior + 3 new)
- `dotnet test --filter "FullyQualifiedName~Constructor"` -- 3 constructor-specific tests pass
- Constructors: section appears in get_type_members output for SimpleClass
- .ctor decompilation works via decompile_method tool

## Deviations from Plan

None -- plan executed exactly as written.

## Decisions Made

1. **Constructors before Methods in output**: Follows typical C# class layout convention where constructors appear before methods.
2. **No change to DecompileMethodAsync**: The existing `m.Name == methodName` matching already supports `.ctor` and `.cctor` -- no modification needed.

## Known Stubs

None -- all functionality is fully wired.

## Self-Check: PASSED

- All 5 modified files exist
- Both commits (c875d78, 87a65ce) verified in git log
- TypeInfo.cs contains Constructors property
- ILSpyDecompilerService.cs contains 3 IsConstructor references (1 new for constructors, 2 existing)
- GetTypeMembersUseCase.cs contains Constructors: output section
