# Phase 1: Test Infrastructure & Baseline - Research

**Researched:** 2026-04-07
**Domain:** xUnit integration testing, custom test assembly design, DI fixture patterns
**Confidence:** HIGH

## Summary

Phase 1 creates a comprehensive integration test suite for all 8 existing MCP tools against a custom test assembly. The existing tests use runtime DLLs (System.Collections.dll) which produce non-deterministic, platform-dependent results -- many tests silently pass by catching expected failures on type-forwarded types. The new suite uses a purpose-built `ILSpy.Mcp.TestTargets` class library with known types, enabling deterministic structural assertions.

The primary technical challenges are: (1) designing a test assembly rich enough to exercise all 8 tools and anticipate future phase needs (cross-refs, IL, metadata, search), (2) consolidating duplicated DI registration from two test classes into a shared fixture, and (3) handling a runtime mismatch where the project targets net9.0 but only net10.0 is installed on this machine.

**Primary recommendation:** Create a `TestTargets/` class library with ~15-20 carefully designed types covering all type kinds (class, interface, struct, enum, delegate, generics, nested, static, abstract), use xUnit `ICollectionFixture` for shared DI, and one test class per tool with 3-5 tests each. The test project and TestTargets project must both target net10.0 (or use a RollForward policy) since net9.0 runtime is not available.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Build a custom test assembly (`ILSpy.Mcp.TestTargets`) as a separate C# class library project within the same repo/solution. Tests read the compiled DLL path at runtime.
- **D-02:** Include a comprehensive set of types: classes, interfaces, abstract classes, structs, enums, generics, nested types, static classes, extension methods, and inheritance hierarchies -- covering all 8 current tools plus anticipated future phase needs.
- **D-03:** Do NOT use runtime DLLs (System.Collections.dll, etc.) as test targets. Custom assembly gives deterministic, version-independent assertions.
- **D-04:** Use xUnit `IClassFixture` or `ICollectionFixture` for shared test setup (DI container, assembly paths). Eliminate the duplicated DI registration currently in ToolsIntegrationTests and TruncationTests.
- **D-05:** Organize as one test class per tool (e.g., `DecompileTypeToolTests.cs`, `ListAssemblyTypesToolTests.cs`). Scales well as tools are added in later phases.
- **D-06:** Thorough baseline -- each tool gets ~3-5 tests: happy path with known types, edge cases (empty namespaces, generic types, nested types), and error cases (invalid path, missing type).
- **D-07:** Use structural assertions -- verify output contains expected sections, headers, type/member names from the custom assembly, and correct counts. Not just non-empty checks.
- **D-08:** Replace existing test files (`ToolsIntegrationTests.cs`, `TruncationTests.cs`) entirely with new tests against the custom assembly. No need to maintain two patterns.

### Claude's Discretion
- Specific type names and structure within the test assembly
- Exact fixture implementation (IClassFixture vs ICollectionFixture) based on what works best with the DI setup
- Test method naming convention

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-01 | All existing tools have regression tests that pass after SDK upgrades | Custom test assembly design (deterministic types), one-class-per-tool structure with 3-5 tests each, shared DI fixture pattern, structural assertions against known output |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK -- no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes
- **Compatibility**: Must not break existing 8 tools during upgrades
- **Test framework**: xUnit 2.9.x (stay on v2, do NOT upgrade to v3)
- **Assertions**: FluentAssertions 8.8.0+ (already in use)
- **Do NOT add**: Moq, NSubstitute, or other mocking frameworks

## Standard Stack

### Core (Already in Project)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit | 2.9.2 | Test framework | Already in use; v2 is locked decision per CLAUDE.md |
| FluentAssertions | 8.8.0 | Assertion library | Already in use; fluent API for structural assertions |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test runner infrastructure | Already in use; compatible with xUnit 2.x |
| coverlet.collector | 6.0.2 | Code coverage | Already in use |

### No New Packages Needed
This phase requires zero new NuGet packages. The test assembly (`ILSpy.Mcp.TestTargets`) is a plain class library with no dependencies. All testing packages are already in the test project.

## Architecture Patterns

### Recommended Project Structure
```
ILSpy.Mcp.sln
  ILSpy.Mcp.csproj                    # Main project (unchanged)
  TestTargets/
    ILSpy.Mcp.TestTargets.csproj      # NEW: Class library with test types
    Types/
      SimpleClass.cs
      GenericTypes.cs
      InheritanceHierarchy.cs
      InterfaceTypes.cs
      StructTypes.cs
      EnumTypes.cs
      NestedTypes.cs
      StaticClassWithExtensions.cs
      DelegateTypes.cs
      AbstractTypes.cs
  Tests/
    ILSpy.Mcp.Tests.csproj            # Updated: references TestTargets
    Fixtures/
      ToolTestFixture.cs              # Shared DI + assembly path fixture
      ToolTestCollection.cs           # xUnit collection definition
    Tools/
      DecompileTypeToolTests.cs       # One file per tool
      DecompileMethodToolTests.cs
      ListAssemblyTypesToolTests.cs
      AnalyzeAssemblyToolTests.cs
      GetTypeMembersToolTests.cs
      FindTypeHierarchyToolTests.cs
      SearchMembersByNameToolTests.cs
      FindExtensionMethodsToolTests.cs
```

### Pattern 1: xUnit Collection Fixture for Shared DI
**What:** A single DI container and assembly path shared across all tool test classes via `ICollectionFixture<T>`.
**When to use:** When multiple test classes need the same expensive setup (DI container, assembly loading).
**Why ICollectionFixture over IClassFixture:** All 8 tool test classes need the same DI container and assembly path. `ICollectionFixture` shares one instance across multiple test classes. `IClassFixture` would create a separate instance per class, which is wasteful but functionally identical. Use `ICollectionFixture` since the DI setup is identical across all classes.

**Example:**
```csharp
// Fixtures/ToolTestFixture.cs
public sealed class ToolTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public string TestAssemblyPath { get; }

    public ToolTestFixture()
    {
        // Build path to TestTargets DLL
        // The test project references TestTargets, so the DLL is in the output dir
        TestAssemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "ILSpy.Mcp.TestTargets.dll");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.Configure<ILSpyOptions>(options =>
        {
            options.DefaultTimeoutSeconds = 30;
            options.MaxDecompilationSize = 1_048_576;
            options.MaxConcurrentOperations = 10;
        });

        // Same registrations as Program.cs RegisterServices()
        services.AddSingleton<ITimeoutService, TimeoutService>();
        services.AddScoped<IDecompilerService, ILSpyDecompilerService>();

        // Use cases
        services.AddScoped<DecompileTypeUseCase>();
        services.AddScoped<DecompileMethodUseCase>();
        services.AddScoped<ListAssemblyTypesUseCase>();
        services.AddScoped<AnalyzeAssemblyUseCase>();
        services.AddScoped<GetTypeMembersUseCase>();
        services.AddScoped<FindTypeHierarchyUseCase>();
        services.AddScoped<SearchMembersByNameUseCase>();
        services.AddScoped<FindExtensionMethodsUseCase>();

        // Tools
        services.AddScoped<DecompileTypeTool>();
        services.AddScoped<DecompileMethodTool>();
        services.AddScoped<ListAssemblyTypesTool>();
        services.AddScoped<AnalyzeAssemblyTool>();
        services.AddScoped<GetTypeMembersTool>();
        services.AddScoped<FindTypeHierarchyTool>();
        services.AddScoped<SearchMembersByNameTool>();
        services.AddScoped<FindExtensionMethodsTool>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}

// Fixtures/ToolTestCollection.cs
[CollectionDefinition("ToolTests")]
public class ToolTestCollection : ICollectionFixture<ToolTestFixture> { }

// Tools/ListAssemblyTypesToolTests.cs
[Collection("ToolTests")]
public class ListAssemblyTypesToolTests
{
    private readonly ToolTestFixture _fixture;

    public ListAssemblyTypesToolTests(ToolTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListAssemblyTypes_NoFilter_ReturnsAllTypes()
    {
        var tool = _fixture.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            namespaceFilter: null,
            CancellationToken.None);

        result.Should().Contain("Assembly:");
        result.Should().Contain("Types found:");
        result.Should().Contain("SimpleClass");
        result.Should().Contain("IAnimal");
    }
}
```

### Pattern 2: TestTargets DLL Path Resolution
**What:** The test project has a `<ProjectReference>` to TestTargets, making the DLL appear in the test output directory automatically.
**When to use:** Always. This avoids hardcoded paths and works in CI.

**Example (csproj reference):**
```xml
<!-- Tests/ILSpy.Mcp.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\TestTargets\ILSpy.Mcp.TestTargets.csproj" />
</ItemGroup>
```

The key: add the ProjectReference but DON'T reference the TestTargets namespace in test code. The reference ensures the DLL is built and copied to the output directory. Tests load it by file path using `IDecompilerService`, just like a real user would.

### Pattern 3: Test Assembly Type Design for Future Phases
**What:** Design test types that cover not just current tools but anticipated Phase 3-7 needs.
**When to use:** When designing the TestTargets assembly.

Types needed per future phase:
- **Phase 3 (IL/Disassembly):** Types with methods that have distinct IL patterns (loops, try/catch, async)
- **Phase 4 (Cross-refs):** Types that call each other's methods, implement interfaces, create instances of other types
- **Phase 5 (Metadata/Resources):** Types with custom attributes, embedded resources in the assembly
- **Phase 6 (Search):** Types with string literals (`ldstr`), numeric constants, enum usage
- **Phase 7 (Bulk):** Multiple types in multiple namespaces for bulk decompilation

### Anti-Patterns to Avoid
- **Conditional test passes:** The existing tests have `if (typeLine == null) return;` patterns that silently pass when no types are found. New tests MUST assert concrete known values from the custom assembly.
- **Exception-swallowing assertions:** Existing tests catch `McpToolException` in the happy path and treat it as passing. New tests should have separate test methods for success and error paths.
- **Duplicated DI setup:** Current `ToolsIntegrationTests` and `TruncationTests` each have 40+ lines of identical DI registration. The fixture pattern eliminates this.
- **Testing against runtime DLLs:** System.Collections.dll contains type-forwarded types that cannot be decompiled, making most "success" tests unreliable.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| DI container for tests | Manual service creation in each test class | xUnit `ICollectionFixture` with `ServiceCollection` | Eliminates duplication, ensures consistent configuration |
| Test assembly path resolution | Hardcoded paths or environment variables | `<ProjectReference>` + `AppContext.BaseDirectory` | Works in CI and local dev, auto-builds dependency |
| Test data types | In-memory mocks or stubs | Real compiled TestTargets assembly | Tests the actual decompiler path, not an abstraction |

## Common Pitfalls

### Pitfall 1: TestTargets DLL Not Copied to Output
**What goes wrong:** Tests fail at runtime because `ILSpy.Mcp.TestTargets.dll` is not in the test output directory.
**Why it happens:** Missing `<ProjectReference>` or the TestTargets project is not in the solution.
**How to avoid:** Add ProjectReference in the test csproj AND add the TestTargets project to the solution file. Verify with `dotnet build` that the DLL appears in `Tests/bin/Debug/net10.0/`.
**Warning signs:** `FileNotFoundException` or `ASSEMBLY_LOAD_FAILED` errors in test output.

### Pitfall 2: Target Framework Mismatch
**What goes wrong:** Tests cannot run because the project targets net9.0 but only net10.0 runtime is installed.
**Why it happens:** This machine has only .NET 10.0 SDK/runtime. The project currently targets net9.0.
**How to avoid:** Either (a) change `<TargetFramework>` to `net10.0` in all three projects, or (b) add `<RollForward>LatestMajor</RollForward>` to the project properties. Option (a) is cleaner since SDK upgrade is Phase 2 anyway, and this machine cannot run net9.0 tests. However, changing TargetFramework is arguably a Phase 2 concern. The pragmatic solution: add `<RollForward>LatestMajor</RollForward>` to all projects so tests can run on net10.0 while still targeting net9.0.
**Warning signs:** "You must install or update .NET to run this application" error (already observed on this machine).

### Pitfall 3: Scoped Services in Singleton Provider
**What goes wrong:** xUnit fixture creates a `ServiceProvider` once, but scoped services (IDecompilerService, all UseCases, all Tools) need a scope to resolve properly.
**Why it happens:** `BuildServiceProvider()` gives a root provider. Getting scoped services from the root works but generates warnings and may cause disposal issues.
**How to avoid:** Create an `IServiceScope` per test (or per test class) for resolving scoped services. The fixture provides the `ServiceProvider`, but each test method calls `ServiceProvider.CreateScope()` and resolves from `scope.ServiceProvider`.
**Warning signs:** `InvalidOperationException` about scope, or services not being disposed between tests.

### Pitfall 4: Generic Type Name Formatting
**What goes wrong:** Tests assert on type names like `GenericClass<T>` but the decompiler returns `GenericClass`1`.
**Why it happens:** .NET reflection uses backtick-arity notation for generic types (e.g., `List`1` instead of `List<T>`).
**How to avoid:** Use backtick notation in assertions, or check what format each tool outputs and match accordingly.
**Warning signs:** String assertion failures on generic type names.

### Pitfall 5: Decompile Tools Return Formatted Output, Not Raw Code
**What goes wrong:** Tests try to assert on raw C# syntax but the tool output is formatted with headers, sections, and descriptions.
**Why it happens:** Tools like `decompile_type` and `decompile_method` format output with headers ("Type Members:", "Decompiled Source:") and structural sections.
**How to avoid:** Assert on structural markers (headers, section names) and key content (type names, method signatures) rather than exact C# syntax.
**Warning signs:** Assertion failures due to formatting differences.

## Test Assembly Design

### Required Type Categories (from D-02)

| Category | Example Types | Exercises Tools | Future Phase Value |
|----------|---------------|-----------------|-------------------|
| Simple class | `SimpleClass` with methods, properties, fields | decompile_type, get_type_members, decompile_method | Phase 3 (IL), Phase 6 (string search) |
| Interface + implementors | `IAnimal`, `Dog : IAnimal`, `Cat : IAnimal` | find_type_hierarchy, list_assembly_types | Phase 4 (XREF-02: find implementors) |
| Abstract class + derivations | `Shape`, `Circle : Shape`, `Rectangle : Shape` | find_type_hierarchy, decompile_type | Phase 4 (XREF-02, XREF-03) |
| Generic types | `Repository<T>`, `Pair<T1, T2>` | list_assembly_types, get_type_members | Phase 5 (META-04: attributes on generics) |
| Struct | `Point`, `Color` | list_assembly_types, get_type_members | Phase 3 (IL: value type patterns) |
| Enum | `DayOfWeek`, `Permissions` ([Flags]) | list_assembly_types | Phase 6 (SRCH-02: enum constants) |
| Nested types | `Outer.Inner`, `Outer.InnerEnum` | list_assembly_types, decompile_type | Phase 5 (TYPE-01: nested types) |
| Static class + extensions | `StringExtensions` with extension methods | find_extension_methods | Phase 4 (XREF-01: usage tracing) |
| Delegate | `EventHandler<T>` style delegate | list_assembly_types | Phase 3 (IL) |
| Cross-calling types | `ServiceA` calls `ServiceB.DoWork()` | decompile_method | Phase 4 (XREF-01, XREF-03, XREF-04) |
| String-heavy type | Methods with `ldstr` string literals | decompile_method | Phase 6 (SRCH-01: string search) |
| Attributed types | `[Serializable]`, `[Obsolete]`, custom attributes | get_type_members | Phase 5 (META-03, META-04) |
| Multiple namespaces | `TestTargets.Animals`, `TestTargets.Shapes`, `TestTargets.Services` | list_assembly_types (namespace filter) | Phase 7 (BULK-01: namespace decompile) |

### Namespace Organization
```
ILSpy.Mcp.TestTargets
  ILSpy.Mcp.TestTargets.Animals    (IAnimal, Dog, Cat)
  ILSpy.Mcp.TestTargets.Shapes     (Shape, Circle, Rectangle, Point)
  ILSpy.Mcp.TestTargets.Generics   (Repository<T>, Pair<T1,T2>)
  ILSpy.Mcp.TestTargets.Services   (ServiceA, ServiceB -- cross-calling)
  ILSpy.Mcp.TestTargets             (SimpleClass, enums, delegates, statics, nested)
```

## Code Examples

### Tool Signatures (for test method design)

All 8 tools and their `ExecuteAsync` signatures (the public API tests call):

```csharp
// 1. list_assembly_types
ListAssemblyTypesTool.ExecuteAsync(string assemblyPath, string? namespaceFilter, CancellationToken)

// 2. decompile_type
DecompileTypeTool.ExecuteAsync(string assemblyPath, string typeName, string? query, CancellationToken)

// 3. decompile_method
DecompileMethodTool.ExecuteAsync(string assemblyPath, string typeName, string methodName, string? query, CancellationToken)

// 4. analyze_assembly
AnalyzeAssemblyTool.ExecuteAsync(string assemblyPath, string? query, CancellationToken)

// 5. get_type_members
GetTypeMembersTool.ExecuteAsync(string assemblyPath, string typeName, CancellationToken)

// 6. find_type_hierarchy
FindTypeHierarchyTool.ExecuteAsync(string assemblyPath, string typeName, CancellationToken)

// 7. search_members_by_name
SearchMembersByNameTool.ExecuteAsync(string assemblyPath, string searchTerm, string? memberKind, CancellationToken)

// 8. find_extension_methods
FindExtensionMethodsTool.ExecuteAsync(string assemblyPath, string targetTypeName, CancellationToken)
```

### Error Code Constants (for error case assertions)

```csharp
// McpToolException error codes used across all tools:
"TYPE_NOT_FOUND"        // Type doesn't exist in assembly
"METHOD_NOT_FOUND"      // Method doesn't exist on type
"ASSEMBLY_LOAD_FAILED"  // Assembly path invalid or file corrupt
"TIMEOUT"               // Operation exceeded timeout
"CANCELLED"             // CancellationToken triggered
"INTERNAL_ERROR"        // Unexpected exception
```

### Structural Assertion Examples

```csharp
// Good: Structural assertions on known types
result.Should().Contain("Assembly:");
result.Should().Contain("Types found: 15");  // exact count from known assembly
result.Should().Contain("ILSpy.Mcp.TestTargets.Animals.Dog");
result.Should().Contain("class");

// Good: Error case with known error code
var act = () => tool.ExecuteAsync(assemblyPath, "NonExistent.Type", CancellationToken.None);
var ex = await act.Should().ThrowAsync<McpToolException>();
ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");

// Bad: Conditional pass (existing pattern)
if (typeLine == null) return;  // NEVER do this -- silently passes
```

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build + test | Yes | 10.0.100 | -- |
| .NET Runtime 9.0 | Current TargetFramework | No | -- | Add `<RollForward>LatestMajor</RollForward>` or change to net10.0 |
| .NET Runtime 10.0 | Available runtime | Yes | 10.0.0 | -- |
| dotnet test | Test execution | Yes | via SDK 10.0.100 | -- |

**Missing dependencies with no fallback:**
- None (RollForward resolves the net9.0 gap)

**Missing dependencies with fallback:**
- .NET 9.0 Runtime: Not installed. Use `<RollForward>LatestMajor</RollForward>` in all project files to run net9.0-targeted code on net10.0 runtime. This is non-breaking and standard practice.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 8.8.0 |
| Config file | `Tests/ILSpy.Mcp.Tests.csproj` (package refs inline) |
| Quick run command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "Category=Tool"` |
| Full suite command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TEST-01 | list_assembly_types returns known types | integration | `dotnet test --filter "FullyQualifiedName~ListAssemblyTypesToolTests"` | Wave 0 |
| TEST-01 | decompile_type decompiles known type | integration | `dotnet test --filter "FullyQualifiedName~DecompileTypeToolTests"` | Wave 0 |
| TEST-01 | decompile_method decompiles known method | integration | `dotnet test --filter "FullyQualifiedName~DecompileMethodToolTests"` | Wave 0 |
| TEST-01 | analyze_assembly returns structural info | integration | `dotnet test --filter "FullyQualifiedName~AnalyzeAssemblyToolTests"` | Wave 0 |
| TEST-01 | get_type_members lists members | integration | `dotnet test --filter "FullyQualifiedName~GetTypeMembersToolTests"` | Wave 0 |
| TEST-01 | find_type_hierarchy shows inheritance | integration | `dotnet test --filter "FullyQualifiedName~FindTypeHierarchyToolTests"` | Wave 0 |
| TEST-01 | search_members_by_name finds members | integration | `dotnet test --filter "FullyQualifiedName~SearchMembersByNameToolTests"` | Wave 0 |
| TEST-01 | find_extension_methods finds extensions | integration | `dotnet test --filter "FullyQualifiedName~FindExtensionMethodsToolTests"` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj`
- **Per wave merge:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `TestTargets/ILSpy.Mcp.TestTargets.csproj` -- class library project with test types
- [ ] `Tests/Fixtures/ToolTestFixture.cs` -- shared DI + assembly path fixture
- [ ] `Tests/Fixtures/ToolTestCollection.cs` -- xUnit collection definition
- [ ] 8 test class files in `Tests/Tools/` -- one per tool
- [ ] Remove `Tests/ToolsIntegrationTests.cs` and `Tests/TruncationTests.cs`
- [ ] Add `<RollForward>LatestMajor</RollForward>` to enable running on net10.0
- [ ] Add TestTargets project to solution file

## Open Questions

1. **RollForward vs TargetFramework change**
   - What we know: Only net10.0 runtime is available. RollForward is a clean workaround.
   - What's unclear: Phase 2 will change TargetFramework anyway during SDK upgrade. Adding RollForward now means removing it later.
   - Recommendation: Use `<RollForward>LatestMajor</RollForward>` for now. It is the least invasive change and avoids mixing TargetFramework changes into a testing phase. Phase 2 can clean it up.

2. **Exact type count in TestTargets**
   - What we know: Need coverage for all 8 tools plus future phases. ~15-20 types across 4-5 namespaces is the target.
   - What's unclear: Exact count depends on implementation details of each tool's output format.
   - Recommendation: Start with the types listed in the Test Assembly Design section. Add more if needed during implementation.

## Sources

### Primary (HIGH confidence)
- Project codebase: All 8 tool files, existing test files, csproj files, Program.cs -- direct inspection
- xUnit documentation: `ICollectionFixture<T>` pattern -- well-established xUnit 2.x feature
- .NET SDK: `AppContext.BaseDirectory` for DLL path resolution -- standard .NET API

### Secondary (MEDIUM confidence)
- `<RollForward>LatestMajor</RollForward>` behavior -- documented in Microsoft Learn for .NET host runtime resolution

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new packages, all existing tooling
- Architecture: HIGH -- xUnit fixture patterns are well-documented, project structure follows existing conventions
- Pitfalls: HIGH -- identified from direct codebase analysis (runtime mismatch confirmed, DI duplication observed, test quality issues documented)

**Research date:** 2026-04-07
**Valid until:** 2026-05-07 (stable -- no dependency changes in this phase)
