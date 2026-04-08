# Phase 2: SDK Upgrades & Bug Fixes - Research

**Researched:** 2026-04-07
**Domain:** C# SDK upgrades (ICSharpCode.Decompiler 9.1->10.0), concurrency bug fixes, CTS disposal, constructor exposure
**Confidence:** HIGH

## Summary

Phase 2 upgrades ICSharpCode.Decompiler from 9.1.0.7988 to 10.0.0.8330 (the MCP SDK upgrade to 1.2.0 was already completed in quick task 260407-hz7), fixes three known bugs (semaphore not enforced, CancellationTokenSource leak, constructors missing from get_type_members), and validates everything against the 31-test Phase 1 baseline.

The decompiler upgrade is low-risk: the codebase has zero usage of the removed APIs (`ITypeReference`, `ToTypeReference`, `UnresolvedUsingScope`, `ResolvedUsingScope`) -- confirmed by grep of all source files. The APIs actually used (`CSharpDecompiler`, `FullTypeName`, `ITypeDefinition`, `IMethod`, `IProperty`, `IField`, `IEvent`, `DecompilerSettings`) are stable across 9.1 to 10.0. The three bug fixes are well-scoped: all 8 use cases share the identical CTS pattern (making it a single fix applied 8 times), the semaphore is a new cross-cutting concern at the use case layer, and constructor exposure requires changes in 3 files (domain model, decompiler service, use case formatter).

**Primary recommendation:** Upgrade decompiler first (compile-fix-test), then fix bugs one at a time (fix-test each), running the full 31-test baseline after every change.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-01: Big bang upgrade -- bump ICSharpCode.Decompiler to 10.0.0.8330 in one step, fix all compilation errors, then run baseline tests
- D-02: Verify no transitive usage of removed APIs by grepping before upgrading (DONE -- confirmed zero hits)
- D-03: Handle ILInstruction.Extract() returning ILVariable? in new code (not currently used, relevant for Phase 3+)
- D-04: MCP SDK 1.2.0 upgrade already done. Validate existing tools still compile and pass tests.
- D-05: Add SemaphoreSlim at the use case layer, wrapping each use case's ExecuteAsync
- D-06: Implement as decorator/wrapper service to avoid duplicating semaphore logic
- D-07: Semaphore should be a singleton initialized from ILSpyOptions.MaxConcurrentOperations
- D-08: Restructure TimeoutService to return a disposable wrapper that owns the CTS lifecycle
- D-09: Consolidate double-CTS pattern -- TimeoutService creates one linked CTS combining timeout + caller cancellation
- D-10: Ensure all code paths dispose CTS via using pattern
- D-11: Add constructors as a new section in get_type_members output, displayed as "Constructors:" between header and Methods
- D-12: For decompile_method, accept .ctor and .cctor names; resolve to actual MethodDefinition handles
- D-13: Multiple constructor overloads all listed; require parameter types to disambiguate when decompiling
- D-14: Each bug fix gets a dedicated regression test
- D-15: Run all Phase 1 baseline tests after each upgrade/fix

### Claude's Discretion
- Exact decorator/wrapper pattern for semaphore enforcement (decorator vs injected service vs base class)
- Exact API shape of the disposable timeout wrapper
- Constructor display formatting details (parameter types, accessibility modifiers)
- Order of operations: upgrade decompiler first, then fix bugs, then run full test suite
- Whether to update Microsoft.Extensions.Hosting to 10.0.0 in the main project (test project already has it)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SDK-01 | Upgrade MCP SDK to 1.2.0 without breaking existing tools | Already done in quick task 260407-hz7. Validate only -- csproj already shows 1.2.0. Run baseline tests. |
| SDK-02 | Upgrade ICSharpCode.Decompiler to 10.x without breaking existing tools | Codebase grep confirms zero usage of removed APIs. Bump version in csproj, compile, fix any errors, run baseline. |
| SDK-03 | MaxConcurrentOperations semaphore is enforced | Add a ConcurrencyLimiter service wrapping SemaphoreSlim. Inject into all 8 use cases or use decorator. See Architecture section. |
| SDK-04 | CancellationTokenSource properly disposed | Restructure ITimeoutService to return IDisposable wrapper. All 8 use cases have identical pattern to fix. See Architecture section. |
| SDK-05 | Constructors in get_type_members and decompile_method | Add Constructors to TypeInfo model, include in MapToTypeInfo, add "Constructors:" section in formatter, handle .ctor/.cctor in DecompileMethodAsync. |
| TEST-04 | Bug fixes have regression tests | Add tests for semaphore throttling, CTS disposal, constructor listing/decompilation. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK -- no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes
- **Compatibility**: Must not break existing 8 tools during upgrades
- **Do NOT add**: Mono.Cecil, dnlib, ILSpyX, Moq/NSubstitute, explicit System.Reflection.Metadata package
- **Target framework**: net10.0 (already migrated in Phase 1)
- **xUnit**: Stay on v2.9.x (v3 migration deferred)

## Standard Stack

### Core (Upgrades)
| Library | Current | Target | Purpose | Verified |
|---------|---------|--------|---------|----------|
| ICSharpCode.Decompiler | 9.1.0.7988 | 10.0.0.8330 | Decompilation engine | NuGet latest confirmed via `dotnet list package --outdated` |
| ModelContextProtocol | 1.2.0 | 1.2.0 (done) | MCP server framework | Already upgraded in csproj |
| ModelContextProtocol.AspNetCore | 1.2.0 | 1.2.0 (done) | HTTP transport | Already upgraded in csproj |

### Test (Minor Updates -- Optional)
| Library | Current | Target | Purpose | Breaking? |
|---------|---------|--------|---------|-----------|
| FluentAssertions | 8.8.0 | 8.9.0 | Assertions | No |
| xunit | 2.9.2 | 2.9.3 | Test framework | No |

**Installation:**
```xml
<!-- ILSpy.Mcp.csproj: change version only -->
<PackageReference Include="ICSharpCode.Decompiler" Version="10.0.0.8330" />
```

## Architecture Patterns

### Pattern 1: Disposable Timeout Wrapper (SDK-04 fix)

**What:** Replace `ITimeoutService.CreateTimeoutToken()` returning a bare `CancellationToken` with a method returning a disposable wrapper that owns both the timeout CTS and the linked CTS.

**Why:** Current code creates two CTS objects per call -- one in TimeoutService (never disposed) and one linked CTS in each use case (disposed via `using`). The TimeoutService CTS leaks.

**Current broken pattern (all 8 use cases):**
```csharp
// TimeoutService -- CTS created but never disposed
public CancellationToken CreateTimeoutToken(CancellationToken cancellationToken = default)
{
    var timeoutCts = new CancellationTokenSource(timeout); // LEAK
    if (cancellationToken != default)
        return CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token).Token; // LEAK #2
    return timeoutCts.Token;
}

// Use case -- creates ANOTHER linked CTS on top
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken,
    _timeout.CreateTimeoutToken()); // passes leaked token
```

**Fixed pattern:**
```csharp
// New interface
public interface ITimeoutService
{
    TimeoutToken CreateTimeoutToken(CancellationToken cancellationToken = default);
    TimeSpan GetDefaultTimeout();
}

// Disposable wrapper owning all CTS lifecycle
public sealed class TimeoutToken : IDisposable
{
    private readonly CancellationTokenSource _timeoutCts;
    private readonly CancellationTokenSource? _linkedCts;

    public CancellationToken Token { get; }

    public TimeoutToken(TimeSpan timeout, CancellationToken externalToken = default)
    {
        _timeoutCts = new CancellationTokenSource(timeout);
        if (externalToken != default)
        {
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                externalToken, _timeoutCts.Token);
            Token = _linkedCts.Token;
        }
        else
        {
            Token = _timeoutCts.Token;
        }
    }

    public void Dispose()
    {
        _linkedCts?.Dispose();
        _timeoutCts.Dispose();
    }
}

// Use case -- clean single-owner pattern
using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
var result = await _decompiler.SomeMethodAsync(assembly, type, timeout.Token);
```

**Impact:** All 8 use cases get simplified -- remove their `CancellationTokenSource.CreateLinkedTokenSource` call, replace with `using var timeout = _timeout.CreateTimeoutToken(cancellationToken)`.

### Pattern 2: Concurrency Limiter Service (SDK-03 fix)

**What:** A singleton service wrapping `SemaphoreSlim` that throttles concurrent use case executions.

**Recommended approach -- injected service (simplest, matches existing DI patterns):**
```csharp
// Application/Services/ConcurrencyLimiter.cs
public interface IConcurrencyLimiter
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}

public sealed class ConcurrencyLimiter : IConcurrencyLimiter, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public ConcurrencyLimiter(IOptions<ILSpyOptions> options)
    {
        _semaphore = new SemaphoreSlim(options.Value.MaxConcurrentOperations);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
```

**Usage in use cases:**
```csharp
// Each use case injects IConcurrencyLimiter
return await _limiter.ExecuteAsync(async () =>
{
    using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
    return await _decompiler.GetTypeInfoAsync(assembly, type, timeout.Token);
}, cancellationToken);
```

**Registration:**
```csharp
services.AddSingleton<IConcurrencyLimiter, ConcurrencyLimiter>();
```

**Why not decorator:** A decorator pattern (wrapping each use case with a throttled proxy) would require either a shared base class/interface for all use cases, or individual decorator classes for each. The injected service is simpler and follows the existing pattern where use cases already inject `ITimeoutService`. Adding `IConcurrencyLimiter` is the same pattern.

### Pattern 3: Constructor Exposure (SDK-05 fix)

**Three files need changes:**

1. **Domain/Models/TypeInfo.cs** -- Add `Constructors` property:
```csharp
public sealed record TypeInfo
{
    // ... existing properties ...
    public IReadOnlyList<MethodInfo> Constructors { get; init; } = Array.Empty<MethodInfo>();
    public IReadOnlyList<MethodInfo> Methods { get; init; } = Array.Empty<MethodInfo>();
    // ...
}
```

2. **Infrastructure/Decompiler/ILSpyDecompilerService.cs** -- Include constructors in MapToTypeInfo:
```csharp
private static TypeInfo MapToTypeInfo(ITypeDefinition type)
{
    return new TypeInfo
    {
        // ...
        Constructors = type.Methods.Where(m => m.IsConstructor).Select(MapToMethodInfo).ToList(),
        Methods = type.Methods.Where(m => !m.IsConstructor).Select(MapToMethodInfo).ToList(),
        // ...
    };
}
```

Also fix `DecompileMethodAsync` to handle `.ctor`/`.cctor`:
```csharp
// Current: var methods = type.Methods.Where(m => m.Name == methodName).ToList();
// This already works for .ctor/.cctor because IMethod.Name returns ".ctor"/".cctor"
// BUT the current code might not find them if the name matching is off.
// Key: ITypeDefinition.Methods includes constructors. m.Name == ".ctor" will match.
```

3. **Application/UseCases/GetTypeMembersUseCase.cs** -- Add "Constructors:" section:
```csharp
if (typeInfo.Constructors.Any())
{
    result.AppendLine("Constructors:");
    foreach (var ctor in typeInfo.Constructors)
    {
        var accessibility = ctor.Accessibility.ToString().ToLower();
        var modifiers = new List<string>();
        if (ctor.IsStatic) modifiers.Add("static");
        var parameters = string.Join(", ", ctor.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var mods = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
        result.AppendLine($"  {accessibility} {mods}{ctor.Name}({parameters})");
    }
    result.AppendLine();
}
```

**Constructor name matching for decompile_method:** In ICSharpCode.Decompiler's type system, `IMethod.Name` returns `.ctor` for instance constructors and `.cctor` for static constructors. The existing `DecompileMethodAsync` code already does `type.Methods.Where(m => m.Name == methodName)`, which will match `.ctor` and `.cctor` directly. The methods collection (`type.Methods`) includes constructors -- it is only the `MapToTypeInfo` call that filters them with `!m.IsConstructor`. So the decompile path likely works already; the main fix is ensuring it does not accidentally exclude constructors.

### Anti-Patterns to Avoid
- **Sharing SemaphoreSlim instance across async continuations without proper release:** Always use try/finally in the limiter.
- **Checking `cancellationToken != default` for CTS linking:** The `default` check is fragile. Instead, always create a linked CTS -- if the external token is `CancellationToken.None`, linking is cheap and correct.
- **Adding constructor logic in the Tool layer:** Keep it in the Use Case and Service layers. Tools are thin wrappers.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Timeout + cancellation linking | Manual CTS creation in every use case | Single TimeoutToken wrapper class | Current double-CTS pattern is the bug being fixed |
| Concurrency limiting | Per-method SemaphoreSlim.WaitAsync calls | Centralized ConcurrencyLimiter service | One place to maintain, one place to test |
| Constructor filtering | Custom IL inspection for constructors | `IMethod.IsConstructor` property from ICSharpCode.Decompiler | Already built into the type system |

## Common Pitfalls

### Pitfall 1: Decompiler Output Format Changes After Upgrade
**What goes wrong:** ICSharpCode.Decompiler 10.0 may produce slightly different whitespace, comments, or code formatting compared to 9.1. Tests that assert exact string matches will fail even though decompilation is correct.
**Why it happens:** Decompiler internal improvements change output format between major versions.
**How to avoid:** Existing tests use `.Should().Contain()` (substring checks), not exact matches. This is the correct pattern. Do NOT add exact-match assertions.
**Warning signs:** Tests fail with "expected X but got X" where the difference is whitespace or line ordering.

### Pitfall 2: SemaphoreSlim.WaitAsync Not Respecting Cancellation
**What goes wrong:** If you pass `CancellationToken.None` to `SemaphoreSlim.WaitAsync()`, a request blocked on the semaphore will wait indefinitely even if the MCP client disconnects.
**How to avoid:** Always pass the caller's cancellation token to `WaitAsync`. The limiter should accept and forward the token.

### Pitfall 3: CTS Disposal After Cancellation
**What goes wrong:** Disposing a `CancellationTokenSource` after it has fired can throw `ObjectDisposedException` in continuations that still reference the token.
**How to avoid:** The `using` pattern disposes at scope exit, which is after the awaited operation completes (or throws). This is safe. Do NOT dispose CTS in a fire-and-forget manner.

### Pitfall 4: Constructor Overload Disambiguation
**What goes wrong:** A type with 3 `.ctor` overloads -- calling `decompile_method` with just `.ctor` returns all 3 concatenated (current behavior for regular methods too). This is actually the existing behavior for overloaded methods and is acceptable per D-13.
**How to avoid:** The current pattern already handles this: `type.Methods.Where(m => m.Name == methodName)` returns all overloads, and each is decompiled with a `// Overload with N parameter(s)` comment. Same pattern works for constructors.

### Pitfall 5: Static Constructor (.cctor) Edge Cases
**What goes wrong:** `.cctor` (static constructor) has no parameters and no accessibility modifier in IL. MapToMethodInfo may show it as "private" (which is technically correct in IL).
**How to avoid:** Accept that `.cctor` displays as `private static .cctor()` -- this matches the IL-level reality. Document in output formatting.

## Code Examples

### Verified: IMethod.IsConstructor in ICSharpCode.Decompiler
The `IMethod` interface has an `IsConstructor` property that returns true for both `.ctor` and `.cctor`. This is already used in the codebase at line 357 of `ILSpyDecompilerService.cs`:
```csharp
Methods = type.Methods.Where(m => !m.IsConstructor).Select(MapToMethodInfo).ToList(),
```
And at line 279 of `SearchMembersAsync`:
```csharp
.Where(m => m.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) && !m.IsConstructor)
```
Both explicitly exclude constructors. The fix is to include them.

### Verified: IMethod.Name for constructors
`IMethod.Name` returns the string `.ctor` for instance constructors and `.cctor` for type initializers. The existing `DecompileMethodAsync` does string matching on `m.Name == methodName`, so passing `.ctor` or `.cctor` will match.

### Verified: Double-CTS pattern across all 8 use cases
All 8 use cases have this identical pattern:
```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken,
    _timeout.CreateTimeoutToken());
```
This creates a linked CTS from the caller's token and the leaked timeout token. The linked CTS is disposed (good), but the underlying timeout CTS from `CreateTimeoutToken()` is not (bad).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ICSharpCode.Decompiler 9.1 | 10.0.0.8330 | 2026-04-06 | Removed ITypeReference, added IDecompilerTypeSystem, new settings |
| MCP SDK 0.4.0-preview | 1.2.0 stable | 2025-02+ | GA release, same tool registration pattern |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 (targeting 2.9.3) |
| Config file | Tests/ILSpy.Mcp.Tests.csproj |
| Quick run command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build -v minimal` |
| Full suite command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj -v minimal` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SDK-01 | MCP SDK 1.2.0 tools compile and pass | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build -v minimal` | Existing 31 tests cover this |
| SDK-02 | Decompiler 10.0 does not break existing tools | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build -v minimal` | Existing 31 tests cover this |
| SDK-03 | Semaphore limits concurrent operations | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Semaphore" -v minimal` | Wave 0 -- new test needed |
| SDK-04 | CTS disposed properly under repeated calls | unit | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Timeout" -v minimal` | Wave 0 -- new test needed |
| SDK-05 | Constructors in get_type_members and decompile_method | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Constructor" -v minimal` | Wave 0 -- new test needed |
| TEST-04 | Bug fix regression tests exist and pass | meta | All above tests | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build -v minimal`
- **Per wave merge:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj -v minimal`
- **Phase gate:** Full suite green before /gsd:verify-work

### Wave 0 Gaps
- [ ] `Tests/Services/ConcurrencyLimiterTests.cs` -- covers SDK-03 (semaphore throttling)
- [ ] `Tests/Services/TimeoutServiceTests.cs` -- covers SDK-04 (CTS disposal)
- [ ] `Tests/Tools/GetTypeMembersToolTests.cs` -- extend with constructor assertions (SDK-05)
- [ ] `Tests/Tools/DecompileMethodToolTests.cs` -- extend with .ctor/.cctor decompilation (SDK-05)
- [ ] TestTargets already has `SimpleClass` with 2 constructor overloads -- sufficient for constructor tests

## Open Questions

1. **Microsoft.Extensions.Hosting version in main project**
   - What we know: The Web SDK auto-resolves hosting dependencies. The test project already has 10.0.0 explicitly. The main project uses `Microsoft.NET.Sdk.Web` which pulls hosting transitively.
   - Recommendation: Do NOT add an explicit Hosting reference to the main project -- the Web SDK handles it. Only upgrade if a build error occurs after the decompiler upgrade.

2. **ParameterModifiers vs ReferenceKind (Pitfall 6 from PITFALLS.md)**
   - What we know: PITFALLS.md mentions `ParameterModifiers` replaced with `ReferenceKind` in 10.0. However, our code only accesses `method.Parameters[i].Type` and `method.Parameters[i].Name` -- not parameter modifiers.
   - Recommendation: LOW risk, but verify compilation. If it breaks, the fix is straightforward.

3. **SearchMembersAsync also excludes constructors**
   - What we know: Line 279 of ILSpyDecompilerService has `&& !m.IsConstructor` in SearchMembersAsync.
   - Recommendation: Include constructors in search results too, or at minimum do not actively exclude them. This is adjacent to SDK-05 but not explicitly required. Handle during implementation.

## Sources

### Primary (HIGH confidence)
- Codebase analysis: All 8 use cases, TimeoutService, ILSpyDecompilerService, TypeInfo model, Program.cs DI setup
- `dotnet list package --outdated` confirms ICSharpCode.Decompiler 10.0.0.8330 is latest
- `dotnet build` and `dotnet test` confirm 31 tests passing on current baseline
- CLAUDE.md migration guide for both SDK upgrades

### Secondary (MEDIUM confidence)
- PITFALLS.md research on ParameterModifiers -> ReferenceKind change (needs compilation verification)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- versions verified against NuGet, csproj confirmed
- Architecture: HIGH -- all patterns derived from reading actual source code
- Pitfalls: HIGH -- bugs identified directly in source; fix patterns verified against .NET API docs

**Research date:** 2026-04-07
**Valid until:** 2026-05-07 (stable domain, no fast-moving dependencies)
