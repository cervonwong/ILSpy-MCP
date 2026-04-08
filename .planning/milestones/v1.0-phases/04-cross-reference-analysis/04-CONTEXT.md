# Phase 4: Cross-Reference Analysis - Context

**Gathered:** 2026-04-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Enable tracing of execution flow within an assembly — find all usages of a member, find all implementors of an interface/base class, find all outward dependencies of a type or method, and find all instantiation sites of a type. Exposed as 4 dedicated MCP tools plus 1 unified dispatcher tool, powered by a new ICrossReferenceService domain interface with IL scanning infrastructure.

</domain>

<decisions>
## Implementation Decisions

### Tool Design
- **D-01:** 4 separate dedicated MCP tools: `find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations` — one tool per cross-reference concern. Follows the 1-tool-per-concern pattern established by decompile/disassemble tools.
- **D-02:** 1 unified `analyze_references` tool that acts as a thin dispatcher — takes an `analysis_type` parameter (`usages`, `implementors`, `dependencies`, `instantiations`) and routes to the same use case as the dedicated tool. No additional logic beyond dispatch.
- **D-03:** Total tool count goes from 10 to 15 (5 new tools).
- **D-04:** `find_usages` requires `member_name` (not optional). For type-level references, users should use `find_instantiations` or `find_implementors` instead.
- **D-05:** Parameters follow existing conventions: `assembly_path`, `type_name`, `member_name` (where applicable).

### Result Format
- **D-06:** Method context detail level — each hit includes: containing type full name, containing method signature, IL offset, and the opcode used (e.g., `callvirt`, `ldfld`, `newobj`).
- **D-07:** Results grouped by containing type, ordered by IL offset within each method.
- **D-08:** `find_implementors` returns type names only with their relationship (implements interface / extends base class). Users call `get_type_members` on results for member details.

### IL Scanner Service Architecture
- **D-09:** New `ICrossReferenceService` domain interface with 4 methods: `FindUsagesAsync`, `FindImplementorsAsync`, `FindDependenciesAsync`, `FindInstantiationsAsync`. All four methods live on this single interface — conceptually unified as "cross-reference analysis" even though `FindImplementorsAsync` uses type-system metadata rather than IL scanning.
- **D-10:** Infrastructure implementation (`ILSpyCrossReferenceService`) uses `System.Reflection.Metadata` APIs: `MetadataReader`, `MethodBodyBlock.GetILReader()`, `ILOpCode` enum for IL scanning; and `ITypeDefinition` hierarchy for implementor discovery.
- **D-11:** Shared IL scanning helper methods within the infrastructure layer — private helpers that Phase 6 (Search) can later extract into a shared utility when needed. Don't over-abstract now.
- **D-12:** 4 dedicated use cases: `FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase` — each injecting `ICrossReferenceService`, timeout, and concurrency limiter following the established pattern.

### Test Strategy (TEST-02)
- **D-13:** Add focused cross-reference test types to TestTargets assembly — ~5-8 new purpose-built types with clear, traceable relationships: an interface with known implementors, a class that calls specific methods, a type that instantiates others, and methods with known outward dependencies.
- **D-14:** One test class per tool (matching Phase 1 convention): `FindUsagesToolTests`, `FindImplementorsToolTests`, `FindDependenciesToolTests`, `FindInstantiationsToolTests`, `AnalyzeReferencesToolTests`.
- **D-15:** Structural assertions against known cross-reference patterns in TestTargets — verify expected containing types, method signatures, and IL opcodes appear in results.

### Claude's Discretion
- Exact domain model types for results (`UsageResult`, `ImplementorResult`, `DependencyResult`, `InstantiationResult`) — field names and shapes
- Infrastructure service internal organization and IL scanning helper structure
- Specific TestTargets type names and relationship graph
- `analyze_references` dispatcher implementation pattern (switch statement vs dictionary dispatch)
- Error handling for edge cases (abstract methods with no body, extern methods, generic instantiations)
- Whether `find_dependencies` member_name parameter is optional (type-level vs method-level dependency analysis)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Patterns
- `Domain/Services/IDecompilerService.cs` — Existing domain interface pattern to follow
- `Domain/Services/IDisassemblyService.cs` — Second domain interface pattern (Phase 3)
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` — Infrastructure implementation pattern, `CSharpDecompiler` and `MetadataReader` usage
- `Infrastructure/Decompiler/ILSpyDisassemblyService.cs` — Second infrastructure service pattern
- `Application/UseCases/DecompileMethodUseCase.cs` — Use case pattern with timeout, concurrency limiter, error handling
- `Transport/Mcp/Tools/DecompileTypeTool.cs` — MCP tool registration pattern with `[McpServerToolType]`/`[McpServerTool]`

### SDK Documentation
- `CLAUDE.md` — Technology stack, System.Reflection.Metadata key APIs (`MetadataReader`, `MethodBodyBlock.GetILReader()`, `ILOpCode`), ICSharpCode.Decompiler 10.0 details

### Test Infrastructure
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture for DI setup
- `Tests/TestTargets/` — Custom test assembly to extend with cross-reference types

### Prior Phase Context
- `.planning/phases/01-test-infrastructure-baseline/01-CONTEXT.md` — Test patterns, one class per tool, structural assertions
- `.planning/phases/02-sdk-upgrades-bug-fixes/02-CONTEXT.md` — SDK upgrade state, ConcurrencyLimiter/TimeoutService patterns
- `.planning/phases/03-il-infrastructure-disassembly/03-CONTEXT.md` — Disassembly service pattern, separate interface per concern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILSpyDecompilerService` already creates `CSharpDecompiler` instances with `DecompilerSettings` — new service follows same pattern for assembly loading
- `CSharpDecompiler.TypeSystem.MainModule` provides `ITypeDefinition` access for implementor discovery (already used in `ListTypesAsync`, `FindExtensionMethodsAsync`)
- `ToolTestFixture` shared test fixture with DI container — extend for new cross-reference tools and service
- `TestTargets` assembly with existing type hierarchy (interfaces, classes, inheritance) — some may be reusable for implementor tests
- `McpToolException` error handling with error codes — reuse for cross-reference error cases

### Established Patterns
- Domain interface → Infrastructure impl → Application use case → Transport tool (4-layer)
- Tools expose `ExecuteAsync` called directly in tests (not through MCP protocol)
- Use cases inject service + `TimeoutService` + `ConcurrencyLimiter` + `ILogger`
- Services registered as Scoped, cross-cutting concerns as Singleton
- One test class per tool, FluentAssertions, xUnit 2.9.x

### Integration Points
- New `ICrossReferenceService` registered in `Program.cs` DI container
- 5 new MCP tool classes auto-discovered by `WithToolsFromAssembly()`
- 4 new use case classes injected into tools
- TestTargets project extended with cross-reference type graph

</code_context>

<specifics>
## Specific Ideas

- User wants both dedicated tools AND a unified dispatcher — AI assistants get targeted calls for known analysis needs and a single discovery entry point for exploratory analysis
- Result format previewed and approved: numbered list with type, method signature, IL offset, and opcode per hit

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 04-cross-reference-analysis*
*Context gathered: 2026-04-08*
