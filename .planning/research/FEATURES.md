# Feature Landscape

**Domain:** .NET decompilation MCP server (ILSpy feature parity)
**Researched:** 2026-04-07

## Table Stakes

Features users expect from a .NET decompilation tool exposed via MCP. Missing any of these means the tool cannot replace manual ILSpy usage in AI-assisted reverse engineering workflows.

### Already Implemented (Baseline)

| Feature | Status | Notes |
|---------|--------|-------|
| Decompile type to C# | Done | `decompile_type` tool |
| Decompile method to C# | Done | `decompile_method` tool |
| List assembly types | Done | `list_assembly_types` with namespace filter |
| Analyze assembly structure | Done | `analyze_assembly` — public types, namespace counts |
| Get type members | Done | `get_type_members` — methods, properties, fields, events |
| Find type hierarchy | Done | `find_type_hierarchy` — base types and interfaces |
| Search members by name | Done | `search_members` — methods, properties, fields, events by name substring |
| Find extension methods | Done | `find_extension_methods` for a target type |

### Cross-Reference Analysis (P0 — Critical)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Find usages ("Used By") | Core of ILSpy's Analyze window. Every decompiler (ILSpy, dnSpy, dotPeek) has this. Without it, AI cannot trace call chains or understand impact of changes. | High | Requires IL scanning across all types in assembly. ILSpy implements this via `TypeUsedByAnalyzer`, `MethodUsedByAnalyzer` etc. in its Analyzers module. Must scan method bodies for `call`, `callvirt`, `newobj`, `ldfld`, `stfld` etc. opcodes referencing the target. |
| Find implementors | Part of Analyze window — shows which types implement an interface or override a virtual method. dotPeek calls this "Derived Symbols." | Medium | Type system query: iterate types checking `DirectBaseTypes` for interface matches and method overrides. Simpler than full IL scan. |
| Find instantiations ("Instantiated By") | Analyze window feature. Shows where `newobj` instructions target a specific type. Essential for understanding object creation patterns. | Medium | Subset of IL scanning — filter for `newobj` opcode with matching type token. Shares infrastructure with find_usages. |
| Find dependencies ("Uses") | Analyze window's reverse direction — what does this type/method reference? Shows outgoing dependencies rather than incoming. | Medium | Scan the specific type/method's IL for all referenced types and members. Simpler scope than find_usages (single method vs whole assembly). |

### IL Disassembly Output (P0 — Critical)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Disassemble type to IL | ILSpy's "IL" language view dropdown. Every serious decompiler shows raw IL. Critical for understanding compiler-generated code, performance analysis, and verifying decompilation accuracy. | Low | `ICSharpCode.Decompiler` includes `ReflectionDisassembler` with `DisassembleType()`. Already in the library — just needs a new service method and tool wrapping it. |
| Disassemble method to IL | Same as above but method-scoped. `MethodBodyDisassembler` handles individual method IL output. | Low | `ReflectionDisassembler.DisassembleMethod()` or use `MethodBodyDisassembler` directly. Output is text — straightforward to expose via MCP tool. |

### Assembly Metadata (P1 — Expected)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Assembly references list | ILSpy tree view shows "References" node. dotPeek's Assembly Explorer shows dependencies with version info. Needed for dependency analysis. | Low | `System.Reflection.Metadata` API: iterate `AssemblyReferenceHandles` from `MetadataReader`. Returns name, version, culture, public key token. |
| Assembly metadata (target framework, PE info, strong name, entry point) | ILSpy shows this in the assembly node. dotPeek has Metadata Explorer. Basic orientation info for any reverse engineering session. | Low | Combination of `PEReader` for PE headers (bitness, subsystem) and `MetadataReader` for assembly-level attributes (TargetFramework), entry point token, strong name key. |
| Custom attribute inspection | ILSpy displays `[assembly: ...]` attributes. Important for understanding assembly configuration, security attributes, COM interop settings. | Low | `MetadataReader.GetCustomAttributes()` for assembly-level and type-level attributes. Decode attribute arguments via `CustomAttributeValue` provider. |

### String and Constant Search (P1 — Expected)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| String search (ldstr operands) | ILSpy's Search window with "String" filter. Essential for malware analysis, finding hardcoded URLs/paths/keys, locating error messages. dnSpy has this prominently. | Medium | Scan all method bodies for `ldstr` opcode, extract string operand. Shares IL scanning infrastructure with cross-references. Must handle large result sets — pagination required. |
| Constant/enum search | Finding magic numbers, enum values. ILSpy supports searching for numeric constants via `ldc.*` opcodes and `FieldDefinition.HasDefault`. | Medium | Scan for `ldc.i4`, `ldc.i8`, `ldc.r4`, `ldc.r8` opcodes and field definitions with `HasDefault` flag. More niche than string search but valuable for reverse engineering. |

### Resource Extraction (P1 — Expected)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List embedded resources | ILSpy tree view shows Resources node. dotPeek shows resources in Assembly Explorer. Basic discovery feature. | Low | `MetadataReader.ManifestResources` — iterate and return name, type (embedded/linked), size. |
| Extract embedded resource content | ILSpy allows saving resources to disk. For MCP, return base64-encoded content or text content. Important for finding embedded config, SQL, templates. | Medium | `PEReader.GetSectionData()` to read embedded resource bytes. Must handle binary (base64 encode) vs text resources. Size limits critical — don't return a 50MB embedded resource inline. |

### Bulk Decompilation (P2 — Expected for Full Workflows)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Decompile namespace | ILSpy's "Save Code" for a namespace subtree. AI assistants need to decompile related types together for context. | Medium | Iterate types in namespace, decompile each, concatenate. Must handle pagination — a namespace could have hundreds of types. |
| Export to project | ILSpy's "Export to Project" creates a full .csproj with .cs files. `ilspycmd -p` does this from command line. Needed for full codebase recovery. | Medium | `WholeProjectDecompiler.DecompileProject()` API exists in `ICSharpCode.Decompiler`. Writes to a directory. For MCP, return file listing + allow fetching individual files. Cannot inline entire project in one response. |

### Type Navigation (P1 — Expected)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List nested types | ILSpy tree view shows nested types under parent. Compiler-generated types (closures, iterators, async state machines) are nested. | Low | `ITypeDefinition.NestedTypes` from type system. Filter for compiler-generated via `[CompilerGenerated]` attribute. |
| Find compiler-generated types | Understanding async/await, LINQ, yield return implementations requires seeing the generated state machines. | Low | Check `[CompilerGenerated]` attribute on nested types. Naming conventions: `<MethodName>d__N` for async, `<>c__DisplayClassN` for closures. |
| Resolve type across assemblies | Find which assembly in a directory contains a given type. ILSpy does this when you search across loaded assemblies. | Medium | Scan all .dll/.exe files in a directory, load metadata readers, check type definitions. Must be efficient — don't fully decompile each assembly. |
| Load all assemblies from folder | Enable cross-assembly analysis by loading a directory of assemblies. Basis for cross-assembly find_usages, type resolution. | Medium | Enumerate directory, validate each file is a valid PE/.NET assembly, create lightweight metadata index. Error handling critical — skip corrupted files gracefully. |

## Differentiators

Features that make the MCP server more valuable than "just wrapping ILSpy commands." These exploit the AI-assistant context.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Cursor-based pagination for all list operations | MCP spec mandates cursor-based pagination. No GUI decompiler needs this. AI context windows are limited — sending 10,000 types at once wastes tokens. Server controls page size, returns opaque cursor tokens. | Medium | Implement across all list-returning tools: `list_assembly_types`, `search_members`, `find_usages`, `search_strings`, etc. MCP 2025-03-26 spec defines the pagination protocol. |
| Result count metadata | Return `total_count` alongside paginated results so the AI knows scope. "Found 847 usages (showing 1-50)" lets the AI decide whether to paginate further or summarize. | Low | Count query before pagination slice. Include in response metadata. |
| Structured output (not just text) | GUI decompilers show text. MCP tools can return structured JSON — typed member lists, cross-reference graphs, categorized search results. AI can reason about structure, not parse text. | Low | Already partially done with typed models (TypeInfo, MemberSearchResult). Extend to all new features. |
| Reusable IL scanning infrastructure | Single ILScanner service that cross-refs, string search, and constant search all build on. Not user-facing but enables composition: "find all methods that reference type X AND contain string Y." | Medium | Build once, use for find_usages, find_instantiations, search_strings, search_constants. Each feature is a filter predicate over the scan results. |
| Graceful degradation for corrupted assemblies | ILSpy GUI shows error dialogs. dnSpy sometimes crashes. An MCP server must never crash — return partial results with error annotations. "Decompiled 47/50 types; 3 failed: [reasons]." | Medium | Wrap per-type/per-method operations in try/catch. Accumulate successes and failures. Return both in response. Critical for malware analysis where assemblies are often malformed. |
| Assembly file validation | Before attempting decompilation, verify the file is a valid .NET assembly (not native, not corrupted PE). Return a clear error message rather than a cryptic BadImageFormatException. | Low | Check PE magic bytes, CLI header presence, metadata stream validity. Return structured error: "Not a .NET assembly" vs "Corrupted metadata" vs "Obfuscated (unsupported)." |
| Smart truncation with continuation | Current codebase has truncation (TruncationTests.cs exists). Extend to return "result truncated at 50KB, call with offset=50KB to continue" — streaming-like behavior without actual streaming. | Medium | Already have some truncation. Add offset/limit parameters to decompilation tools. Track byte offset for continuation. |

## Anti-Features

Features to explicitly NOT build. These are traps that would waste effort or create maintenance burden.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Assembly editing / patching | dnSpy's killer feature, but completely wrong for an MCP server. AI assistants should not modify binaries — massive security risk, complex to implement, and the dnSpy project itself is barely maintained. | Stay read-only. The project description explicitly scopes to static analysis. |
| Debugging / runtime analysis | dnSpy offers debugging. ILSpy is static-only. Adding debugging would require a runtime host, process attachment, breakpoint management — entirely different product. | Explicitly out of scope per PROJECT.md. Static analysis only. |
| VB.NET / F# output languages | ILSpy supports VB.NET output. Low value for reverse engineering — C# is the universal .NET lingua franca. F# output would be novel but nobody uses it for RE. | C# and IL output only. Matches `ilspycmd` capabilities. |
| GUI / web interface | This is an MCP server for AI assistants. Building any visual interface would dilute focus. | MCP tools are the interface. Let the AI client handle presentation. |
| Multi-assembly session management | PROJECT.md explicitly defers this to next milestone. It requires architectural changes (session state, assembly resolution contexts). | Single-assembly operations with directory scanning for cross-assembly. Full session management is P3. |
| Real-time file watching | Watching for assembly changes on disk. Unnecessary complexity — assemblies under analysis are typically static artifacts. | Load on demand per request. The TimeoutService already handles operation timeouts. |
| Decompilation caching across requests | Tempting optimization, but adds memory pressure, cache invalidation complexity, and stale result risks. ICSharpCode.Decompiler is fast enough for single-type decompilation. | Let ICSharpCode.Decompiler handle its own internal caching. Don't add a caching layer. |
| Source server / PDB integration | dotPeek's PDB-based source matching is valuable in an IDE but useless for MCP. If you have PDBs and source, you don't need a decompiler. | Skip entirely. Decompilation is the use case, not source retrieval. |

## Feature Dependencies

```
Assembly file validation
  --> All other features (everything needs a valid assembly)

IL scanning infrastructure (ILScanner service)
  --> find_usages (cross-reference analysis)
  --> find_instantiations
  --> find_dependencies
  --> search_strings (ldstr scan)
  --> search_constants (ldc.* scan)

ReflectionDisassembler wrapper
  --> disassemble_type
  --> disassemble_method

MetadataReader utilities
  --> list_assembly_references
  --> get_assembly_metadata
  --> get_custom_attributes
  --> list_embedded_resources
  --> extract_embedded_resource

Pagination infrastructure
  --> list_assembly_types (retrofit)
  --> search_members (retrofit)
  --> find_usages
  --> find_implementors
  --> search_strings
  --> search_constants
  --> decompile_namespace

Directory scanning
  --> resolve_type_across_assemblies
  --> load_assemblies_from_folder
  --> cross-assembly find_usages (future)

WholeProjectDecompiler wrapper
  --> export_project

Existing get_type_members
  --> list_nested_types (extension)
  --> find_compiler_generated_types (extension)
```

## MVP Recommendation

For this milestone, prioritize in this order:

### Phase 1: Foundation + Quick Wins
1. **IL disassembly** (Low complexity, high value, `ReflectionDisassembler` API exists)
2. **Assembly metadata** (Low complexity, `MetadataReader` API, immediate orientation value)
3. **Assembly references** (Low complexity, same infrastructure as metadata)
4. **Custom attributes** (Low complexity, same infrastructure)
5. **List nested types** (Low complexity, type system query)

### Phase 2: IL Scanning Infrastructure + Cross-References
6. **ILScanner service** (Medium complexity, enables 4 features below)
7. **find_usages** (High complexity, the most demanded analysis feature)
8. **find_implementors** (Medium, simpler type-system query path)
9. **find_instantiations** (Medium, reuses ILScanner)
10. **find_dependencies** (Medium, reuses ILScanner)

### Phase 3: Search + Resources
11. **String search** (Medium, reuses ILScanner, critical for RE workflows)
12. **Constant search** (Medium, reuses ILScanner)
13. **List embedded resources** (Low complexity)
14. **Extract embedded resources** (Medium, size handling needed)

### Phase 4: Bulk Operations + Cross-Assembly
15. **Pagination infrastructure** (Medium, retrofit to existing + new tools)
16. **Decompile namespace** (Medium, needs pagination)
17. **Resolve type across assemblies** (Medium)
18. **Load assemblies from folder** (Medium)
19. **Export project** (Medium, `WholeProjectDecompiler` API exists)

Defer: Full cross-assembly session management (P3 per PROJECT.md), assembly editing, debugging.

## Sources

- [ILSpy GitHub repository](https://github.com/icsharpcode/ILSpy) — analyzer architecture, ReflectionDisassembler, WholeProjectDecompiler
- [ILSpy complete guide](https://ilspy.org/2025/10/09/complete-guide-to-the-net-decompiler/) — Analyze window features, IL view, Search, Export
- [dotPeek features](https://www.jetbrains.com/decompiler/features/) — Find Usages, Metadata Explorer, Assembly Dependencies, IL viewer
- [ICSharpCode.Decompiler wiki](https://github.com/icsharpcode/ILSpy/wiki/Getting-Started-With-ICSharpCode.Decompiler) — API usage patterns
- [WholeProjectDecompiler source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/CSharp/ProjectDecompiler/WholeProjectDecompiler.cs) — project export API
- [MCP Pagination spec](https://modelcontextprotocol.io/specification/2025-03-26/server/utilities/pagination) — cursor-based pagination protocol
- [MCP best practices](https://thenewstack.io/15-best-practices-for-building-mcp-servers-in-production/) — large output handling, streaming
- [ILSpy BadImageFormatException issues](https://github.com/icsharpcode/ILSpy/issues/3218) — corrupted assembly handling patterns
- [NDepend decompiler comparison](https://blog.ndepend.com/in-the-jungle-of-net-decompilers/) — feature matrix across 7 decompilers
- [dnSpy features](https://dnspy.org/) — editing, debugging capabilities (anti-features for this project)
