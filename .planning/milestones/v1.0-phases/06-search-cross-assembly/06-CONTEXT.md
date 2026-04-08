# Phase 6: Search & Cross-Assembly - Context

**Gathered:** 2026-04-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Enable string and constant search across assembly IL bytecode, and type resolution across multiple assemblies in a directory. Exposed as 4 dedicated MCP tools backed by 2 new domain service interfaces (ISearchService for search operations, ICrossAssemblyService for directory operations), following the established 4-layer architecture.

</domain>

<decisions>
## Implementation Decisions

### Tool Design
- **D-01:** 4 dedicated tools, no dispatcher: `search_strings`, `search_constants`, `resolve_type`, `load_assembly_directory` — one per requirement (SRCH-01, SRCH-02, XASM-01, XASM-02). Follows the 1-tool-per-concern pattern.
- **D-02:** Total tool count goes from 22 to 26 (4 new tools).

### Service Architecture
- **D-03:** New `ISearchService` domain interface for `search_strings` and `search_constants` — search is a distinct concern from cross-reference tracing (follows Phase 3 pattern of separate IDisassemblyService).
- **D-04:** New `ICrossAssemblyService` domain interface for `resolve_type` and `load_assembly_directory` — clean separation of single-assembly vs multi-assembly concerns.
- **D-05:** IL scanning for search (ldstr, ldc.*) reuses the same BlobReader/ILOpCode pattern from ILSpyCrossReferenceService. Shared helpers may be extracted at Claude's discretion.

### Search Behavior (SRCH-01, SRCH-02)
- **D-06:** `search_strings` accepts a regex pattern parameter and scans all `ldstr` operands in assembly IL. Returns matches with containing method context.
- **D-07:** `search_constants` accepts an exact numeric value and finds all `ldc.*` instructions loading that value. Exact match only (no range queries).
- **D-08:** Search result context per hit: matched value, containing type full name, containing method signature, IL offset. Matches Phase 4 cross-ref result style.
- **D-09:** Default result limit with offset parameter for pagination. Return max results by default with an optional `max_results` parameter to override, and an `offset` parameter for paging. Show total match count even when truncated.

### Cross-Assembly Behavior (XASM-01, XASM-02)
- **D-10:** Directory scanning is recursive with a configurable depth limit parameter. Default depth TBD by Claude.
- **D-11:** File filter: scan `.dll` and `.exe` files only.
- **D-12:** Unloadable assemblies (native DLLs, corrupt files) are skipped with a warning. Return the list of skipped files alongside results so the user knows what was excluded.
- **D-13:** `resolve_type` returns all assemblies that define a matching type when multiple matches exist. Let the AI assistant pick the right one.
- **D-14:** `resolve_type` supports partial type name matching — 'HttpClient' matches 'System.Net.Http.HttpClient'. More useful for exploration without requiring full namespace qualification.

### Claude's Discretion
- Exact domain model types for search results (StringSearchResult, ConstantSearchResult, TypeResolutionResult, etc.)
- Default result limit value and default directory scan depth
- Whether to extract shared IL scanning helpers from ILSpyCrossReferenceService into a common utility
- Infrastructure service internal organization
- Specific TestTargets extensions for search and cross-assembly tests
- Error handling for edge cases (empty assemblies, assemblies with no method bodies, directories with no valid assemblies)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Patterns
- `Domain/Services/IDecompilerService.cs` — Domain interface pattern
- `Domain/Services/IDisassemblyService.cs` — Second domain interface (Phase 3, separate concern)
- `Domain/Services/ICrossReferenceService.cs` — Cross-ref interface with IL scanning methods
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` — IL scanning implementation using BlobReader, ILOpCode, MetadataReader — primary reference for search scanning patterns
- `Application/UseCases/FindUsagesUseCase.cs` — Use case pattern with timeout, concurrency limiter
- `Transport/Mcp/Tools/FindUsagesTool.cs` — MCP tool registration pattern

### SDK Documentation
- `CLAUDE.md` — Technology stack, System.Reflection.Metadata key APIs (MetadataReader, MethodBodyBlock.GetILReader(), ILOpCode), ldstr/ldc opcode details

### Test Infrastructure
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture for DI setup
- `Tests/TestTargets/` — Custom test assembly to extend with search-target types

### Prior Phase Context
- `.planning/phases/04-cross-reference-analysis/04-CONTEXT.md` — IL scanning architecture decisions (D-09 through D-12), especially D-11 on shared helpers

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILSpyCrossReferenceService` has private IL scanning helpers (BlobReader iteration, ILOpCode operand size handling) that can be referenced or extracted for search scanning
- `CSharpDecompiler` instance creation pattern with `DecompilerSettings` — reuse in new services
- `McpToolException` error handling with error codes — reuse for search/cross-assembly error cases
- `ToolTestFixture` shared test fixture with DI container — extend for new tools and services
- Phase 5 added native DLL guard (PE CLR header check) — relevant for cross-assembly directory scanning

### Established Patterns
- Domain interface -> Infrastructure impl -> Application use case -> Transport tool (4-layer)
- Tools expose `ExecuteAsync` called directly in tests
- Use cases inject service + `TimeoutService` + `ConcurrencyLimiter` + `ILogger`
- Services registered as Scoped, cross-cutting concerns as Singleton
- One test class per tool, FluentAssertions, xUnit 2.9.x

### Integration Points
- New `ISearchService` and `ICrossAssemblyService` registered in `Program.cs` DI container
- 4 new MCP tool classes auto-discovered by `WithToolsFromAssembly()`
- 4 new use case classes injected into tools
- TestTargets project extended with string literals, constants, and possibly a second test assembly for cross-assembly resolution

</code_context>

<specifics>
## Specific Ideas

- Result pagination with offset+limit mirrors common API patterns — AI assistants can page through large result sets
- Partial type name matching in resolve_type enables exploratory workflows ("what assembly has HttpClient?") without requiring namespace knowledge
- Skip-with-warning for unloadable assemblies keeps the tool resilient when scanning real-world directories with mixed content

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 06-search-cross-assembly*
*Context gathered: 2026-04-08*
