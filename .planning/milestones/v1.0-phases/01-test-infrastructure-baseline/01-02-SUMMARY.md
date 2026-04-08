---
phase: 01-test-infrastructure-baseline
plan: 02
subsystem: test-regression-baseline
tags: [testing, regression, tools]
dependency_graph:
  requires: [TestTargets-assembly, ToolTestFixture, ToolTestCollection]
  provides: [regression-baseline-all-8-tools]
  affects: [Tests/Tools/]
tech_stack:
  added: []
  patterns: [xUnit-Collection-per-tool, FluentAssertions-structural]
key_files:
  created:
    - Tests/Tools/ListAssemblyTypesToolTests.cs
    - Tests/Tools/DecompileTypeToolTests.cs
    - Tests/Tools/DecompileMethodToolTests.cs
    - Tests/Tools/AnalyzeAssemblyToolTests.cs
    - Tests/Tools/GetTypeMembersToolTests.cs
    - Tests/Tools/FindTypeHierarchyToolTests.cs
    - Tests/Tools/SearchMembersByNameToolTests.cs
    - Tests/Tools/FindExtensionMethodsToolTests.cs
  modified: []
  deleted:
    - Tests/ToolsIntegrationTests.cs
    - Tests/TruncationTests.cs
decisions:
  - Invalid assembly path triggers INTERNAL_ERROR (not ASSEMBLY_LOAD_FAILED) due to FileNotFoundException from AssemblyPath.Create validation
  - Generic types appear without backtick notation in ListAssemblyTypes output (decompiler strips arity suffix)
  - FindTypeHierarchy only shows direct base type, not full inheritance chain
metrics:
  duration: 4m
  completed: "2026-04-07T05:43:00Z"
---

# Phase 01 Plan 02: Regression Test Baseline for All 8 Tools Summary

31 regression tests across 8 tool-specific test classes using deterministic assertions against TestTargets assembly, replacing 18 old non-deterministic tests.

## What Was Done

### Task 1: Regression Tests for All 8 Tools (1db86eb)
Created 8 test class files in `Tests/Tools/` with 31 total tests:

- **ListAssemblyTypesToolTests** (5 tests): No-filter listing, namespace filtering, generic type names, delegate types, invalid assembly error
- **DecompileTypeToolTests** (4 tests): SimpleClass decompilation, interface decompilation, type-not-found error, invalid assembly error
- **DecompileMethodToolTests** (4 tests): GetGreeting method, Calculate method, method-not-found error, type-not-found error
- **AnalyzeAssemblyToolTests** (3 tests): Structural info, namespaces listing, invalid assembly error
- **GetTypeMembersToolTests** (4 tests): SimpleClass members, interface members, generic type members, type-not-found error
- **FindTypeHierarchyToolTests** (4 tests): AdminUser chain, Dog interface implementation, Circle abstract base, type-not-found error
- **SearchMembersByNameToolTests** (4 tests): Method search, property search, no-results case, invalid assembly error
- **FindExtensionMethodsToolTests** (3 tests): String extensions found, no-extensions case, invalid assembly error

All tests use:
- `[Collection("ToolTests")]` with shared `ToolTestFixture`
- `_fixture.CreateScope()` for per-test DI isolation
- `_fixture.TestAssemblyPath` for deterministic TestTargets assembly
- FluentAssertions for structural output verification
- No conditional passes or exception-swallowing

### Task 2: Delete Old Test Files (4bf4de0)
Removed `Tests/ToolsIntegrationTests.cs` (18 tests) and `Tests/TruncationTests.cs` (3 tests) which used runtime DLLs with conditional passes and duplicated DI setup.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Corrected invalid assembly error code expectations**
- **Found during:** Task 1
- **Issue:** Plan assumed invalid assembly path would trigger `ASSEMBLY_LOAD_FAILED`, but `AssemblyPath.Create()` throws `FileNotFoundException` (not `AssemblyLoadException`), which falls through to general `Exception` catch returning `INTERNAL_ERROR`.
- **Fix:** Changed all 5 invalid-assembly tests to assert `INTERNAL_ERROR` instead of `ASSEMBLY_LOAD_FAILED`.
- **Files modified:** All 5 test files with invalid assembly tests

**2. [Rule 1 - Bug] Corrected generic type name format in assertions**
- **Found during:** Task 1
- **Issue:** Plan assumed generic types would show backtick notation (e.g., `Repository\`1`), but the decompiler strips arity suffixes.
- **Fix:** Changed assertions to match actual output: `ILSpy.Mcp.TestTargets.Generics.Repository`.
- **Files modified:** Tests/Tools/ListAssemblyTypesToolTests.cs

**3. [Rule 1 - Bug] Corrected hierarchy depth expectation**
- **Found during:** Task 1
- **Issue:** Plan assumed FindTypeHierarchy would show full chain (AdminUser -> User -> BaseEntity -> Object), but it only shows direct base type.
- **Fix:** Removed `BaseEntity` assertion from AdminUser hierarchy test.
- **Files modified:** Tests/Tools/FindTypeHierarchyToolTests.cs

## Verification

- `dotnet test Tests/ILSpy.Mcp.Tests.csproj` passes with 31 tests, 0 failures
- `Tests/ToolsIntegrationTests.cs` and `Tests/TruncationTests.cs` no longer exist
- Each tool has its own test class in Tests/Tools/
- No test uses runtime DLLs

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 1db86eb | Regression tests for all 8 tools (31 tests) |
| 2 | 4bf4de0 | Remove old test files (ToolsIntegrationTests, TruncationTests) |

## Known Stubs

None - all tests contain real assertions against real tool output.

## Self-Check: PASSED

- All 8 test files exist in Tests/Tools/
- Both old test files deleted (ToolsIntegrationTests, TruncationTests)
- Both commit hashes (1db86eb, 4bf4de0) found in git log
- 31 tests pass with 0 failures
