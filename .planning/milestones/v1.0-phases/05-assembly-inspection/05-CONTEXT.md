# Phase 5: Assembly Inspection - Context

**Gathered:** 2026-04-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Expose assembly metadata, custom attributes, embedded resources, and compiler-generated type discovery via 7 new MCP tools. Users can inspect PE headers, trace attribute usage, extract resources, and find compiler-generated types with parent context. Tool count goes from 15 to 22.

</domain>

<decisions>
## Implementation Decisions

### Tool Design — Broad Tools with Hierarchical Discoverability
- **D-01:** 7 new tools total, organized by capability area. Prioritize fewer broad tools with clear names over fine-grained single-purpose tools.
- **D-02:** Tool naming follows existing conventions: verb_noun pattern (`get_assembly_metadata`, `find_compiler_generated_types`).

### Assembly Metadata (META-01, META-02)
- **D-03:** Single unified `get_assembly_metadata` tool returns all PE header info AND referenced assemblies in one call. Fields: target framework, runtime version, PE bitness (32/64), strong name info, entry point, plus full list of assembly references (name, version, culture, public key token). AI assistants get the full picture in one shot.

### Custom Attributes (META-03, META-04)
- **D-04:** Three separate tools by scope level: `get_assembly_attributes`, `get_type_attributes`, `get_member_attributes`. Each returns custom attributes with constructor arguments and named properties.
- **D-05:** Declared attributes only — no inherited attribute traversal. Users trace inheritance via `get_base_types` if needed. Simpler, predictable output.
- **D-06:** `get_assembly_attributes` takes only `assembly_path`. `get_type_attributes` takes `assembly_path` + `type_name`. `get_member_attributes` takes `assembly_path` + `type_name` + `member_name`.

### Embedded Resources (RES-01, RES-02)
- **D-07:** Two separate tools: `list_embedded_resources` for catalog (type, size per resource), `extract_resource` for content retrieval.
- **D-08:** `extract_resource` returns text resources inline, binary as base64. Supports `offset` and `limit` parameters for paginated binary extraction — handles any size without blowing up AI context windows.

### Compiler-Generated Types (TYPE-01, TYPE-02)
- **D-09:** New dedicated `find_compiler_generated_types` tool separate from `list_assembly_types`. Shows DisplayClass, async state machines, closures, iterators.
- **D-10:** Each compiler-generated type shows its parent context when detectable (e.g., `<>c__DisplayClass0 from MyClass.MyMethod`). More useful for reverse engineering than a flat list.

### Architecture
- **D-11:** New `IAssemblyInspectionService` domain interface — separate from `IDecompilerService` and `ICrossReferenceService`. Assembly inspection is a distinct concern (reading metadata/resources vs decompiling/scanning IL).
- **D-12:** Infrastructure implementation uses `System.Reflection.Metadata` APIs (`MetadataReader`, `PEHeaders`, `AssemblyDefinition`, `CustomAttributeHandleCollection`, `ManifestResourceHandleCollection`) and ICSharpCode.Decompiler's type system for compiler-generated type parent resolution.

### Test Strategy
- **D-13:** One test class per tool (7 new test classes), following Phase 1 convention.
- **D-14:** TestTargets assembly extended with: assembly-level attributes, types with custom attributes, embedded resources (text + binary), nested types, and a method with compiler-generated closure/async state machine.
- **D-15:** Structural assertions: verify expected metadata fields present, attribute arguments parsed, resource content matches, compiler-generated types linked to parent methods.

### Claude's Discretion
- Exact domain model types for results (metadata record shapes, attribute result format, resource info format)
- Infrastructure service internal organization and method signatures
- Specific TestTargets additions (type names, attribute choices, resource content)
- How `find_compiler_generated_types` detects parent method relationship (naming convention heuristics vs metadata analysis)
- Whether `extract_resource` offset/limit operates on bytes or base64 characters
- How attribute constructor arguments are formatted in output (positional vs named display)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Patterns
- `Domain/Services/IDecompilerService.cs` — Existing domain interface pattern to follow
- `Domain/Services/IDisassemblyService.cs` — Second domain interface pattern (Phase 3)
- `Domain/Services/ICrossReferenceService.cs` — Third domain interface pattern (Phase 4)
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` — Infrastructure implementation pattern, `MetadataReader` and `PEFile` usage
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` — IL scanning infrastructure pattern
- `Application/UseCases/DecompileTypeUseCase.cs` — Use case pattern with timeout, concurrency limiter, error handling
- `Transport/Mcp/Tools/AnalyzeAssemblyTool.cs` — Existing assembly-level tool pattern (error handling, parameter style)
- `Domain/Models/AssemblyInfo.cs` — Existing assembly info model (may need extending or paralleling)

### SDK Documentation
- `CLAUDE.md` — Technology stack, System.Reflection.Metadata key APIs, ICSharpCode.Decompiler 10.0 details, Assembly Metadata and Embedded Resources API sections

### Test Infrastructure
- `Tests/Fixtures/ToolTestFixture.cs` — Shared test fixture for DI setup
- `Tests/TestTargets/` — Custom test assembly to extend with metadata/resource/attribute test types

### Prior Phase Context
- `.planning/phases/01-test-infrastructure-baseline/01-CONTEXT.md` — Test patterns, one class per tool, structural assertions
- `.planning/phases/02-sdk-upgrades-bug-fixes/02-CONTEXT.md` — SDK upgrade state, service registration patterns
- `.planning/phases/03-il-infrastructure-disassembly/03-CONTEXT.md` — Separate interface per concern decision
- `.planning/phases/04-cross-reference-analysis/04-CONTEXT.md` — IL scanning patterns, TestTargets extension approach

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILSpyDecompilerService` already loads `PEFile` and creates `CSharpDecompiler` — both provide `MetadataReader` access needed for metadata/attribute/resource reading
- `ToolTestFixture` shared test fixture with DI container — extend for 7 new tools
- `TestTargets` assembly — extend with attributed types, embedded resources, nested types
- `McpToolException` error handling with error codes — reuse for inspection errors
- `Domain/Models/AssemblyInfo.cs` — existing assembly info record (basic fields)

### Established Patterns
- Domain interface → Infrastructure impl → Application use case → Transport tool (4-layer)
- Tools expose `ExecuteAsync` called directly in tests
- Use cases inject service + `TimeoutService` + `ConcurrencyLimiter` + `ILogger`
- Services registered as Scoped, cross-cutting as Singleton
- One test class per tool, FluentAssertions, xUnit 2.9.x

### Integration Points
- New `IAssemblyInspectionService` registered in `Program.cs` DI container
- 7 new MCP tool classes auto-discovered by `WithToolsFromAssembly()`
- 7 new use case classes injected into tools
- TestTargets project extended with inspection-focused types and resources

</code_context>

<specifics>
## Specific Ideas

- User wants hierarchical discoverability — tools should let AI assistants drill down from assembly overview → metadata → attributes → resources in a natural progression
- Broad tools preferred over narrow ones — consolidate where it makes sense (e.g., unified metadata tool) but keep concerns separated when they serve different use patterns (e.g., list vs extract resources)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 05-assembly-inspection*
*Context gathered: 2026-04-08*
