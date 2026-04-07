# ILSpy MCP Server Tests

Integration tests for all 8 MCP tools using a deterministic custom test assembly.

## Test Coverage

Each tool has a dedicated test class in `Tests/Tools/` with 3-5 regression tests:

| Tool | Test Class | Tests |
|------|-----------|-------|
| `decompile_type` | `DecompileTypeToolTests` | 4 |
| `decompile_method` | `DecompileMethodToolTests` | 4 |
| `list_assembly_types` | `ListAssemblyTypesToolTests` | 5 |
| `analyze_assembly` | `AnalyzeAssemblyToolTests` | 3 |
| `get_type_members` | `GetTypeMembersToolTests` | 4 |
| `find_type_hierarchy` | `FindTypeHierarchyToolTests` | 4 |
| `search_members_by_name` | `SearchMembersByNameToolTests` | 4 |
| `find_extension_methods` | `FindExtensionMethodsToolTests` | 3 |

**Total: 31 tests, all passing.**

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run a specific tool's tests
dotnet test --filter "FullyQualifiedName~DecompileTypeToolTests"
```

## Test Assembly

Tests use a custom **TestTargets** class library (`ILSpy.Mcp.TestTargets.dll`) instead of runtime assemblies. This provides deterministic, version-stable types for assertions.

The TestTargets project contains 15+ types across 5 namespaces:

- `ILSpy.Mcp.TestTargets` — classes, enums, delegates, nested types, attributes
- `ILSpy.Mcp.TestTargets.Animals` — interfaces and implementations
- `ILSpy.Mcp.TestTargets.Shapes` — abstract classes, structs
- `ILSpy.Mcp.TestTargets.Generics` — generic types with constraints
- `ILSpy.Mcp.TestTargets.Services` — service classes for cross-reference testing

## Test Infrastructure

- **`Fixtures/ToolTestFixture.cs`** — Shared DI container with all 8 tools registered. Resolves the TestTargets DLL path via `AppContext.BaseDirectory`.
- **`Fixtures/ToolTestCollection.cs`** — xUnit `ICollectionFixture` definition. All test classes share the same fixture instance via `[Collection("ToolTests")]`.

Each test creates its own DI scope via `_fixture.CreateScope()` to avoid cross-test contamination.

## Dependencies

- xUnit — Test framework
- FluentAssertions — Assertion library
- ILSpy.Mcp.TestTargets — Custom test assembly (ProjectReference)
