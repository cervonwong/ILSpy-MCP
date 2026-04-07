# Phase 2: SDK Upgrades & Bug Fixes - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Upgrade ICSharpCode.Decompiler from 9.1.0.7988 to 10.x, fix three known bugs (semaphore not enforced, CancellationTokenSource leak, constructors missing from get_type_members), and validate everything against the Phase 1 baseline test suite. MCP SDK upgrade to 1.2.0 was already completed in quick task 260407-hz7.

</domain>

<decisions>
## Implementation Decisions

### Decompiler Upgrade (SDK-02)
- **D-01:** Big bang upgrade — bump ICSharpCode.Decompiler to 10.0.0.8330 in one step, fix all compilation errors, then run baseline tests. The breaking changes are minimal for this codebase (no ITypeReference usage, no ToTypeReference calls, no UnresolvedUsingScope usage).
- **D-02:** Verify no transitive usage of removed APIs by grepping for `ITypeReference`, `ToTypeReference`, `UnresolvedUsingScope`, `ResolvedUsingScope` before upgrading. Fix any hits.
- **D-03:** Handle `ILInstruction.Extract()` returning `ILVariable?` (nullable) in any new code that touches IL analysis — not currently used but relevant for Phase 3+ features.

### MCP SDK Status (SDK-01)
- **D-04:** MCP SDK 1.2.0 upgrade already done (quick task 260407-hz7). Validate existing tools still compile and pass tests with the new SDK. No further migration work expected.

### Semaphore Enforcement (SDK-03)
- **D-05:** Add a `SemaphoreSlim` based on `MaxConcurrentOperations` at the use case layer — wrap each use case's `ExecuteAsync` with semaphore acquire/release. This is the natural chokepoint since all tool calls flow through use cases.
- **D-06:** Implement as a shared base class or a decorator/wrapper service to avoid duplicating semaphore logic in every use case. Prefer a decorator pattern (e.g., `ThrottledUseCaseDecorator` or a `ConcurrencyLimiter` service injected into use cases) for cleaner separation.
- **D-07:** Semaphore should be a singleton (one limiter for the entire server), initialized from `ILSpyOptions.MaxConcurrentOperations` at startup.

### CancellationTokenSource Disposal (SDK-04)
- **D-08:** Restructure `TimeoutService` to return a disposable wrapper that owns the CTS lifecycle. The caller gets a token and disposes the wrapper when done, which disposes the underlying CTS.
- **D-09:** Fix the double-CTS pattern in use cases (e.g., `GetTypeMembersUseCase` creates a linked CTS from the timeout token + caller token). Consolidate so `TimeoutService` creates one linked CTS that combines timeout + caller cancellation, returned as the disposable wrapper. Use cases just `using` the wrapper.
- **D-10:** Ensure all code paths dispose CTS — even on exceptions. The `using` pattern handles this naturally.

### Constructor Exposure (SDK-05)
- **D-11:** Add constructors (.ctor and .cctor) as a new section in `get_type_members` output, displayed as "Constructors:" between the type header and Methods section.
- **D-12:** For `decompile_method`, accept constructor names in the format `.ctor` and `.cctor` (matching IL naming). The decompiler service should resolve these to the actual constructor MethodDefinition handles.
- **D-13:** If a type has multiple constructor overloads, list all of them in get_type_members. For decompile_method, if the name `.ctor` is ambiguous (multiple overloads), require parameter types to disambiguate (same pattern as regular method overloads).

### Testing (TEST-04)
- **D-14:** Each bug fix gets a dedicated regression test proving the fix works. Semaphore: test that concurrent calls beyond the limit are throttled. CTS: test that repeated rapid calls don't accumulate undisposed CTS objects (or at minimum, verify disposal is called). Constructors: test that .ctor/.cctor appear in output and can be decompiled.
- **D-15:** Run all Phase 1 baseline tests after each upgrade/fix to catch regressions.

### Claude's Discretion
- Exact decorator/wrapper pattern for semaphore enforcement (decorator vs injected service vs base class)
- Exact API shape of the disposable timeout wrapper
- Constructor display formatting details (parameter types, accessibility modifiers)
- Order of operations: upgrade decompiler first, then fix bugs, then run full test suite
- Whether to update Microsoft.Extensions.Hosting to 10.0.0 in the main project (test project already has it)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### SDK Upgrade Context
- `CLAUDE.md` — Full migration guide for ICSharpCode.Decompiler 9.1→10.0 and ModelContextProtocol 0.4→1.2 breaking changes
- `.planning/research/PITFALLS.md` — Known pitfalls and risks identified during project research

### Bug Location Files
- `Application/Services/TimeoutService.cs` — CancellationTokenSource leak (SDK-04)
- `Application/Configuration/ILSpyOptions.cs` — MaxConcurrentOperations config (SDK-03, not enforced)
- `Application/UseCases/GetTypeMembersUseCase.cs` — Missing constructor listing (SDK-05), double-CTS pattern (SDK-04)
- `Application/UseCases/DecompileMethodUseCase.cs` — Constructor decompilation support needed (SDK-05)
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` — Core decompiler service, primary upgrade impact

### Architecture & Patterns
- `Program.cs` — Service registration, DI container setup
- `Domain/Services/IDecompilerService.cs` — Domain interface for decompiler operations
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture (Phase 1 baseline)

### Prior Phase Context
- `.planning/phases/01-test-infrastructure-baseline/01-CONTEXT.md` — Test infrastructure decisions

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ToolTestFixture` shared fixture with DI container — use for all regression tests
- Per-tool test classes from Phase 1 — extend with bug fix regression tests
- `TestTargets` assembly — add constructors to test types if not already present

### Established Patterns
- Use cases follow consistent pattern: inject decompiler + timeout + logger, create linked CTS, call decompiler, format output
- Tools are thin wrappers delegating to use cases
- Services registered as Scoped (use cases, decompiler), Singleton (TimeoutService)
- FluentAssertions for test assertions

### Integration Points
- Package references in `ILSpy.Mcp.csproj` — bump ICSharpCode.Decompiler version
- All 8 use cases use `TimeoutService.CreateTimeoutToken()` — disposal fix affects all of them
- `IDecompilerService.GetTypeInfoAsync()` returns `TypeInfo` model — needs constructor data
- `Domain/Models/TypeInfo.cs` — may need Constructors collection added

</code_context>

<specifics>
## Specific Ideas

- User explicitly requested robustness as the priority — favor defensive patterns (proper disposal, enforced limits, clear error messages for constructor resolution failures)
- Full Claude discretion on all implementation details

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-sdk-upgrades-bug-fixes*
*Context gathered: 2026-04-07*
