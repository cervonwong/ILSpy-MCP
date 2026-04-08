---
phase: 01-test-infrastructure-baseline
plan: 01
subsystem: test-infrastructure
tags: [testing, fixtures, test-targets]
dependency_graph:
  requires: []
  provides: [TestTargets-assembly, ToolTestFixture, ToolTestCollection]
  affects: [Tests/ILSpy.Mcp.Tests.csproj, ILSpy.Mcp.csproj, ILSpy.Mcp.sln]
tech_stack:
  added: []
  patterns: [xUnit-ICollectionFixture, DI-ServiceCollection]
key_files:
  created:
    - TestTargets/ILSpy.Mcp.TestTargets.csproj
    - TestTargets/Types/SimpleClass.cs
    - TestTargets/Types/InterfaceTypes.cs
    - TestTargets/Types/AbstractTypes.cs
    - TestTargets/Types/StructTypes.cs
    - TestTargets/Types/EnumTypes.cs
    - TestTargets/Types/GenericTypes.cs
    - TestTargets/Types/NestedTypes.cs
    - TestTargets/Types/StaticClassWithExtensions.cs
    - TestTargets/Types/DelegateTypes.cs
    - TestTargets/Types/InheritanceHierarchy.cs
    - TestTargets/Types/ServiceTypes.cs
    - TestTargets/Types/AttributedTypes.cs
    - Tests/Fixtures/ToolTestFixture.cs
    - Tests/Fixtures/ToolTestCollection.cs
  modified:
    - Tests/ILSpy.Mcp.Tests.csproj
    - ILSpy.Mcp.csproj
    - ILSpy.Mcp.sln
decisions:
  - Updated all projects from net9.0 to net10.0 since only .NET 10 runtime is available
  - Excluded TestTargets from main project compilation to prevent source file leakage
metrics:
  duration: 3m
  completed: "2026-04-07T05:35:00Z"
---

# Phase 01 Plan 01: TestTargets and Test Fixture Infrastructure Summary

TestTargets class library with 15+ types across 5 namespaces plus shared xUnit ICollectionFixture providing DI container with all 8 tools registered.

## What Was Done

### Task 1: TestTargets Class Library (0c55dc9)
Created `TestTargets/ILSpy.Mcp.TestTargets.csproj` targeting net10.0 with 12 source files containing 15+ types across 5 namespaces:

- **ILSpy.Mcp.TestTargets**: SimpleClass, BaseEntity/User/AdminUser inheritance chain, Outer with nested types, StringExtensions, DelegateTypes, EnumTypes (DayOfWeek, Permissions with Flags), AttributedTypes
- **ILSpy.Mcp.TestTargets.Animals**: IAnimal interface, Dog, Cat implementations
- **ILSpy.Mcp.TestTargets.Shapes**: Abstract Shape with Circle/Rectangle, Point/Color structs
- **ILSpy.Mcp.TestTargets.Generics**: Repository<T>, Pair<T1,T2>
- **ILSpy.Mcp.TestTargets.Services**: ServiceA, ServiceB with cross-call patterns

Types cover all 8 existing tool scenarios plus future phase needs (string literals for search, cross-references, attributes).

### Task 2: Shared Test Fixture (0d69054)
Created `Tests/Fixtures/ToolTestFixture.cs` implementing IDisposable:
- Resolves TestTargets DLL path via AppContext.BaseDirectory
- Builds ServiceCollection with ILSpyOptions, TimeoutService, ILSpyDecompilerService, all 8 use cases, all 8 tools
- Exposes `CreateScope()` for per-test isolation

Created `Tests/Fixtures/ToolTestCollection.cs` with `[CollectionDefinition("ToolTests")]` and `ICollectionFixture<ToolTestFixture>`.

Added ProjectReference from test project to TestTargets so the DLL is copied to output.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated all projects to net10.0**
- **Found during:** Task 2
- **Issue:** Only .NET 10 SDK/runtime available on machine. TestTargets targeting net10.0 was incompatible with test project targeting net9.0 (NU1201 error).
- **Fix:** Updated ILSpy.Mcp.csproj and Tests/ILSpy.Mcp.Tests.csproj from net9.0 to net10.0.
- **Files modified:** ILSpy.Mcp.csproj, Tests/ILSpy.Mcp.Tests.csproj

**2. [Rule 3 - Blocking] Excluded TestTargets from main project compilation**
- **Found during:** Task 2
- **Issue:** Main project SDK globbing picked up TestTargets source files (same directory tree), causing duplicate compilation and warnings.
- **Fix:** Added `<Compile Remove="TestTargets/**" />` (and Content/EmbeddedResource/None) to ILSpy.Mcp.csproj.
- **Files modified:** ILSpy.Mcp.csproj

## Verification

- `dotnet build ILSpy.Mcp.sln` succeeds with 0 warnings, 0 errors
- `Tests/bin/Debug/net10.0/ILSpy.Mcp.TestTargets.dll` exists (12800 bytes)
- Solution contains all 3 projects

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 0c55dc9 | TestTargets class library with 12 type files |
| 2 | 0d69054 | Shared test fixture, collection, project references |

## Known Stubs

None - all types contain real implementations, no placeholder data.

## Self-Check: PASSED

- All 15 created files exist on disk
- Both commit hashes (0c55dc9, 0d69054) found in git log
- Solution builds with 0 warnings, 0 errors
