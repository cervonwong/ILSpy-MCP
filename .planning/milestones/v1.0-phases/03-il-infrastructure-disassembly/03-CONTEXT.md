# Phase 3: IL Infrastructure & Disassembly - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Expose raw CIL disassembly output for types and methods via two new MCP tools (`disassemble_type`, `disassemble_method`), using ICSharpCode.Decompiler's `ReflectionDisassembler`. Build the domain/infrastructure/application/transport layers following existing patterns. Integration tests validate IL structural correctness.

</domain>

<decisions>
## Implementation Decisions

### IL Output Content
- **D-01:** Use `ReflectionDisassembler` resolved IL output (annotated with resolved type/method names, not raw tokens). Add a summary header above the IL with type metadata for orientation.
- **D-02:** Type-level disassembly shows structure and method signatures only (no IL bodies). Users drill down to specific methods via `disassemble_method` for full IL instruction listings. This keeps type-level output manageable for large types.
- **D-03:** Method-level disassembly returns the complete IL body with resolved names, `.maxstack`, `IL_xxxx` labels, and full instruction listings.

### Tool Design
- **D-04:** Two separate tools: `disassemble_type` and `disassemble_method` — follows the existing `decompile_type`/`decompile_method` pattern. Tool count goes from 8 to 10.
- **D-05:** `disassemble_` prefix — distinct from `decompile_` (C# output), mirrors ILSpy's own terminology.
- **D-06:** Same base parameters as decompile counterparts (`assembly_path`, `type_name`, `method_name`) plus optional IL-specific flags (`show_bytes` for raw opcode byte sequences, `show_tokens` for metadata token numbers). Consistent parameter naming and validation.
- **D-07:** Method overload disambiguation matches existing `decompile_method` behavior — if ambiguous, return error listing available overloads so user can specify parameter types.

### Test Strategy (TEST-03)
- **D-08:** Validate IL structural correctness via IL structure markers: assert output contains `.method` directives, `.maxstack`, `IL_xxxx` labels, known opcodes (`ldarg`, `call`, `ret`, etc.). Test against TestTargets methods with known signatures.
- **D-09:** Dedicated tests for each optional flag — verify `show_bytes` adds hex byte sequences, `show_tokens` adds metadata token numbers to output.
- **D-10:** Comprehensive error coverage: standard errors (invalid assembly path, type not found, method not found) matching existing `decompile_` tool error codes, plus IL-specific edge cases (abstract methods with no IL body, extern methods, types with no methods).

### Claude's Discretion
- Summary header content and formatting for type-level disassembly (Claude picks what ReflectionDisassembler naturally produces)
- Exact implementation of `show_bytes` and `show_tokens` flags (how they map to ReflectionDisassembler options)
- Domain model types for IL output (whether to reuse `DecompilationResult` or create new types)
- Infrastructure service method signatures and internal organization
- Whether to add new methods to `IDecompilerService` or create a separate `IDisassemblyService` interface

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Patterns
- `Domain/Services/IDecompilerService.cs` — Domain interface to extend or parallel with new disassembly methods
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` — Implementation pattern; `ReflectionDisassembler` will be used here
- `Application/UseCases/DecompileTypeUseCase.cs` — Reference pattern for type-level use case (timeout, cancellation, error handling)
- `Application/UseCases/DecompileMethodUseCase.cs` — Reference pattern for method-level use case (overload disambiguation)
- `Transport/Mcp/Tools/DecompileTypeTool.cs` — Reference pattern for MCP tool registration and parameter handling
- `Transport/Mcp/Tools/DecompileMethodTool.cs` — Reference pattern for method tool with parameter types

### SDK Documentation
- `CLAUDE.md` — Technology stack, ICSharpCode.Decompiler 10.0 API details, `ReflectionDisassembler` with `DisassembleType()` and `DisassembleMethod()` methods

### Test Infrastructure
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture for DI setup
- `Tests/TestTargets/` — Custom test assembly with known types for deterministic assertions

### Prior Phase Context
- `.planning/phases/01-test-infrastructure-baseline/01-CONTEXT.md` — Test patterns (one class per tool, structural assertions)
- `.planning/phases/02-sdk-upgrades-bug-fixes/02-CONTEXT.md` — SDK upgrade decisions, ILInstruction.Extract() nullable handling

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILSpyDecompilerService` already creates `CSharpDecompiler` instances — `ReflectionDisassembler` uses the same `MetadataFile` (via `PEFile`)
- `ToolTestFixture` shared test fixture with DI container registration — extend for new tools
- `TestTargets` assembly with known types — test IL output against these
- `McpToolException` error handling with error codes (TYPE_NOT_FOUND, METHOD_NOT_FOUND, ASSEMBLY_LOAD_FAILED)
- `DecompilerSettings` already configured in `ILSpyDecompilerService` constructor

### Established Patterns
- Domain interface → Infrastructure impl → Application use case → Transport tool (4-layer)
- Tools expose `ExecuteAsync` called directly in tests (not through MCP protocol)
- Use cases handle timeout/cancellation via `TimeoutService` and `ConcurrencyLimiter`
- Services registered as Scoped (use cases, decompiler), Singleton (TimeoutService, ConcurrencyLimiter)
- One test class per tool, FluentAssertions, xUnit 2.9.x

### Integration Points
- New tools register via `[McpServerToolType]` / `[McpServerTool]` attributes — auto-discovered by `WithToolsFromAssembly()`
- DI registration in `Program.cs` for new use cases and any new services
- Test project needs new test classes for `DisassembleTypeToolTests` and `DisassembleMethodToolTests`

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches for the implementation details within the decisions above.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 03-il-infrastructure-disassembly*
*Context gathered: 2026-04-07*
