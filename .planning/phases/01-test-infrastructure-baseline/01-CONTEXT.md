# Phase 1: Test Infrastructure & Baseline - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish a comprehensive integration test suite for all 8 existing MCP tools against a custom test assembly, providing a known-good regression baseline before any SDK upgrades or code changes.

</domain>

<decisions>
## Implementation Decisions

### Test Assembly Strategy
- **D-01:** Build a custom test assembly (`ILSpy.Mcp.TestTargets`) as a separate C# class library project within the same repo/solution. Tests read the compiled DLL path at runtime.
- **D-02:** Include a comprehensive set of types: classes, interfaces, abstract classes, structs, enums, generics, nested types, static classes, extension methods, and inheritance hierarchies — covering all 8 current tools plus anticipated future phase needs.
- **D-03:** Do NOT use runtime DLLs (System.Collections.dll, etc.) as test targets. Custom assembly gives deterministic, version-independent assertions.

### Test Structure
- **D-04:** Use xUnit `IClassFixture` or `ICollectionFixture` for shared test setup (DI container, assembly paths). Eliminate the duplicated DI registration currently in ToolsIntegrationTests and TruncationTests.
- **D-05:** Organize as one test class per tool (e.g., `DecompileTypeToolTests.cs`, `ListAssemblyTypesToolTests.cs`). Scales well as tools are added in later phases.

### Coverage Depth
- **D-06:** Thorough baseline — each tool gets ~3-5 tests: happy path with known types, edge cases (empty namespaces, generic types, nested types), and error cases (invalid path, missing type).
- **D-07:** Use structural assertions — verify output contains expected sections, headers, type/member names from the custom assembly, and correct counts. Not just non-empty checks.

### Existing Test Handling
- **D-08:** Replace existing test files (`ToolsIntegrationTests.cs`, `TruncationTests.cs`) entirely with new tests against the custom assembly. No need to maintain two patterns.

### Claude's Discretion
- Specific type names and structure within the test assembly
- Exact fixture implementation (IClassFixture vs ICollectionFixture) based on what works best with the DI setup
- Test method naming convention

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Test Infrastructure
- `Tests/ILSpy.Mcp.Tests.csproj` — Current test project configuration, package references
- `Tests/ToolsIntegrationTests.cs` — Current test patterns (to be replaced), DI setup pattern to understand
- `Tests/TruncationTests.cs` — Current truncation tests (to be replaced)

### Tool Implementations (what tests must cover)
- `Transport/Mcp/Tools/DecompileTypeTool.cs` — Tool #1
- `Transport/Mcp/Tools/DecompileMethodTool.cs` — Tool #2
- `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` — Tool #3
- `Transport/Mcp/Tools/AnalyzeAssemblyTool.cs` — Tool #4
- `Transport/Mcp/Tools/GetTypeMembersTool.cs` — Tool #5
- `Transport/Mcp/Tools/FindTypeHierarchyTool.cs` — Tool #6
- `Transport/Mcp/Tools/SearchMembersByNameTool.cs` — Tool #7
- `Transport/Mcp/Tools/FindExtensionMethodsTool.cs` — Tool #8

### Architecture Layers
- `Domain/Services/` — Service interfaces (IDecompilerService, ITimeoutService)
- `Infrastructure/Decompiler/` — ILSpy decompiler service implementation
- `Application/UseCases/` — Use case classes that tools depend on
- `Application/Configuration/` — ILSpyOptions configuration
- `Application/Services/` — TimeoutService and other application services

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- DI registration pattern in existing tests shows all required services and their registrations
- `ILSpyOptions` configuration with `DefaultTimeoutSeconds`, `MaxDecompilationSize`, `MaxConcurrentOperations`
- `McpToolException` error handling pattern with error codes (TYPE_NOT_FOUND, METHOD_NOT_FOUND, ASSEMBLY_LOAD_FAILED)

### Established Patterns
- Tools expose `ExecuteAsync` methods called directly in tests (not through MCP protocol layer)
- Services registered as Scoped, TimeoutService as Singleton
- FluentAssertions used for assertions, xUnit 2.9.x as test framework

### Integration Points
- Test project references main project via `<ProjectReference Include="..\ILSpy.Mcp.csproj" />`
- New TestTargets project will need to be added to the solution and referenced by the test project

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches for the test assembly structure and fixture design.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-test-infrastructure-baseline*
*Context gathered: 2026-04-07*
