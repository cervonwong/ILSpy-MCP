# Phase 7: Bulk Operations & Documentation - Context

**Gathered:** 2026-04-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Namespace-level decompilation (summary listing with full signatures + drill-down), full project export to disk via WholeProjectDecompiler, and comprehensive README documentation for all 28 tools. Two new MCP tools: `decompile_namespace` and `export_project`.

</domain>

<decisions>
## Implementation Decisions

### Namespace Decompilation (BULK-01)
- **D-01:** `decompile_namespace` returns a **summary listing**, not concatenated source. Lists all types in the namespace with full signatures (type name, kind, base type, member count, public method signatures). User calls `decompile_type` individually for full source.
- **D-02:** Include nested types in the listing, indented under their parent type.
- **D-03:** Types ordered by kind (interfaces, enums, structs, classes) then alphabetically within each group.
- **D-04:** Return NAMESPACE_NOT_FOUND error when the namespace doesn't exist, with suggestion to use `list_namespaces`. Consistent with TYPE_NOT_FOUND pattern.
- **D-05:** Timeout via existing TimeoutService + explicit `max_types` parameter to bound work.

### Project Export (BULK-02)
- **D-06:** `export_project` uses `WholeProjectDecompiler` to write .csproj + .cs files to disk.
- **D-07:** Returns file listing + stats: output directory path, total file count, list of generated .cs file paths (relative to output dir), and .csproj path.
- **D-08:** `output_directory` is a required parameter. Tool auto-creates the directory if it doesn't exist.
- **D-09:** Fail if output directory is non-empty — return an error. User must specify an empty or non-existent directory.
- **D-10:** Optional `namespace` parameter to limit export scope to a specific namespace. Full assembly export by default.
- **D-11:** Partial failure handling: continue exporting when individual types fail, include list of failed types in response. Matches Phase 6 skip-with-warning pattern.
- **D-12:** Timeout via existing TimeoutService + explicit `max_types` parameter.

### Documentation (DOC-01)
- **D-13:** README focuses on installation, configuration, and tool reference only. No architecture section (that stays in ARCHITECTURE.md/docs/).
- **D-14:** Tools grouped by category: Decompilation, IL Disassembly, Type Analysis, Cross-References, Assembly Inspection, Search, Bulk Operations.
- **D-15:** Every tool gets a usage example showing JSON input + trimmed output snippet.
- **D-16:** Examples wrapped in HTML `<details>` disclosure accordions to keep README scannable.

### Claude's Discretion
- Exact `max_types` default value for both tools
- WholeProjectDecompiler configuration and namespace filtering implementation
- Domain model types for namespace listing results and project export results
- README category groupings for the 28 tools (suggested grouping above, Claude finalizes)
- How nested types are visually distinguished in namespace listing output

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Patterns
- `Domain/Services/IDecompilerService.cs` — Has `ListTypesAsync` and `DecompileTypeAsync` used by namespace decompilation
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` — `CSharpDecompiler` and `DecompilerSettings` setup; extend for `WholeProjectDecompiler`
- `Application/UseCases/DecompileTypeUseCase.cs` — Reference pattern for type-level use case
- `Transport/Mcp/Tools/DecompileTypeTool.cs` — Reference pattern for MCP tool registration

### SDK Documentation
- `CLAUDE.md` — Technology stack, WholeProjectDecompiler API details
- `.planning/research/ARCHITECTURE.md:330-352` — Planned architecture for both tools
- `.planning/research/FEATURES.md:61-66` — Feature descriptions and complexity notes

### Test Infrastructure
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture for DI setup
- `TestTargets/` — Test assembly with known types and namespaces

### Prior Phase Context
- `.planning/phases/06-search-cross-assembly/06-CONTEXT.md` — Pagination and skip-with-warning patterns

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IDecompilerService.ListTypesAsync(assemblyPath, namespaceFilter)` — already supports namespace filtering, use for `decompile_namespace`
- `IDecompilerService.DecompileTypeAsync` — decompile individual types for drill-down
- `IDecompilerService.GetTypeInfoAsync` — get type metadata for summary listing
- `McpToolException` with error codes — reuse for NAMESPACE_NOT_FOUND
- `TimeoutService` + `ConcurrencyLimiter` — reuse for bulk operation timeouts
- `WholeProjectDecompiler` from ICSharpCode.Decompiler — API for project export

### Established Patterns
- Domain interface -> Infrastructure impl -> Application use case -> Transport tool (4-layer)
- Tools expose `ExecuteAsync` called directly in tests
- Use cases inject service + `TimeoutService` + `ConcurrencyLimiter` + `ILogger`
- Services registered as Scoped, cross-cutting concerns as Singleton
- One test class per tool, FluentAssertions, xUnit 2.9.x
- Error codes: TYPE_NOT_FOUND, METHOD_NOT_FOUND, ASSEMBLY_LOAD_FAILED, MEMBER_NOT_FOUND

### Integration Points
- New methods on `IDecompilerService` or new `IBulkDecompilerService` (Claude's discretion)
- 2 new MCP tool classes auto-discovered by `WithToolsFromAssembly()`
- 2 new use case classes injected into tools
- DI registration in `Program.cs`
- README.md rewrite with all 28 tools documented

</code_context>

<specifics>
## Specific Ideas

- Namespace listing is a "table of contents" — AI reads it to decide which types to drill into, similar to how ILSpy's tree view works
- Project export mirrors `ilspycmd -p` behavior for familiarity
- README examples in disclosure accordions keep the page clean while being comprehensive for users who need details
- JSON input/output format for examples matches how AI assistants actually invoke MCP tools

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 07-bulk-operations-documentation*
*Context gathered: 2026-04-08*
