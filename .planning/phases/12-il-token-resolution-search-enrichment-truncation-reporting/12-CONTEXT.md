# Phase 12: IL Token Resolution, Search Enrichment & Truncation Reporting - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Enhance IL disassembly output with inline metadata token resolution, enrich search_strings/search_constants with method FQN and surrounding IL context, and make silent truncation visible across all source-returning and bounded-output tools. Requirements: IL-01, IL-02, IL-03, OUTPUT-06, OUTPUT-07, PAGE-07, PAGE-08.

**In scope:**
- `disassemble_method` — inline-resolve metadata tokens (IL-01)
- `disassemble_type` — inline-resolve metadata tokens (IL-02)
- Both disassemble tools — `resolveDeep` opt-in flag for expanded resolution (IL-03)
- `search_strings` — enrich matches with method FQN and surrounding IL window (OUTPUT-06)
- `search_constants` — enrich matches with method FQN (OUTPUT-07)
- `decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method` — structured truncation reporting (PAGE-07)
- `export_project`, `analyze_assembly` — structured truncation/bounding reporting (PAGE-08)

**Not in scope:**
- Pagination params for these tools (they are not list-returning tools)
- Description rewrites (Phase 13)
- New tools or capabilities

</domain>

<decisions>
## Implementation Decisions

### IL Token Resolution — disassemble tools (IL-01, IL-02)

- **D-01:** ICSharpCode.Decompiler's `ReflectionDisassembler` already resolves metadata tokens to readable IL names (e.g., `call instance string [System.Runtime]System.Object::ToString()`). The base resolution in IL-01/IL-02 is largely satisfied by existing infrastructure. The work is: (a) verify that `[assembly]` scope prefixes appear consistently for all cross-assembly refs, (b) enhance resolution for cases where `ReflectionDisassembler` abbreviates (e.g., intra-assembly refs may omit the `[assembly]` prefix, generic type parameters may show as `!!0`/`!0` instead of resolved names), (c) add structured comments for key opcodes where additional context helps the agent. Per Principle 2 (nested references by default): every token operand the agent sees should be self-sufficient — no follow-up call needed to understand it.

- **D-02:** Opcodes requiring inline resolution (minimum set from requirements): `call`, `callvirt`, `newobj`, `ldfld`, `ldstr`. Additionally resolve: `stfld`, `ldsfld`, `stsfld`, `ldftn`, `ldvirtftn`, `castclass`, `isinst`, `box`, `unbox`, `newarr` — anything with a metadata token operand. The implementation should post-process `ReflectionDisassembler` output only where gaps exist, not rewrite the entire disassembler.

- **D-03:** For `disassemble_type` (IL-02): the current implementation uses headers-only mode (field headers, method headers, properties, events — no IL bodies). Token resolution applies to the operands visible in these headers (field types, method signatures, property types). The type-level view already shows resolved type names via `ReflectionDisassembler`; verify and enhance where needed.

### resolveDeep flag (IL-03)

- **D-04:** Add `resolveDeep` boolean parameter (default `false`) to both `disassemble_method` and `disassemble_type`. Per Principle 5 (rich but not flooding): default output is standard resolution (FQN + assembly scope). `resolveDeep=true` adds:
  - Full parameter type signatures for method references (e.g., `Process(System.String, System.Int32)` instead of just `Process`)
  - Generic type arguments expanded (e.g., `List<System.String>` instead of `List<!!0>`)
  - Parameter names where available from metadata
  - This flag flows through `IDisassemblyService` → infrastructure layer. Implementation may use a custom `ITextOutput` wrapper or post-processing pass.

### Search enrichment — search_strings (OUTPUT-06)

- **D-05:** Enrich `StringSearchResult` domain model with:
  - `MethodSignature` (string) — full method signature with parameter types, e.g., `ProcessOrder(System.String, System.Int32)`. Per Principle 3 (assume lazy agent): the agent needs the full method identity to locate the match without a follow-up call.
  - `SurroundingInstructions` (IReadOnlyList<string>) — 3 IL instructions before and 3 after the `ldstr` instruction, with resolved token references. Per Principle 2: the surrounding context tells the agent how the string is used (passed to which method, stored in which field) without calling `disassemble_method`.

- **D-06:** The `SearchStringsUseCase.FormatResults` method updates to include surrounding IL in the output. Format per match:
  ```
    "Hello, World!" in MyApp.Services.Processor.DoWork(String, Int32) (IL_0023)
      IL_001E: ldarg.1
      IL_001F: call instance void [System.Runtime]System.Console::Write(string)
      IL_0024: ldstr "Hello, World!"        <-- match
      IL_0029: call instance void [System.Runtime]System.Console::WriteLine(string)
      IL_002E: ret
  ```
  Per Principle 5: this is the default. No verbose flag needed since per-match depth is ~7 lines, well within the ~20 line heuristic.

### Search enrichment — search_constants (OUTPUT-07)

- **D-07:** Enrich `ConstantSearchResult` domain model with:
  - `MethodSignature` (string) — full method signature with parameter types, same as OUTPUT-06.
  - No surrounding IL window — the requirement only asks for value + method FQN + IL offset. Constants are less ambiguous than strings (the value IS the context). Per Principle 5: keep it lean; the agent can call `disassemble_method` if it needs the IL context around a constant.

- **D-08:** The `SearchConstantsUseCase.FormatResults` method updates to show method signature:
  ```
    42 (Int32) in MyApp.Services.Processor.DoWork(String, Int32) (IL_0023)
  ```

### Truncation reporting — source tools (PAGE-07)

- **D-09:** Current state: `decompile_type`, `decompile_method`, and `list_namespace_types` already truncate at `MaxDecompilationSize` bytes (1MB default) with ad-hoc message: `[Output truncated at {bytes} bytes. The full output is {total} bytes.]`. `disassemble_type` and `disassemble_method` have NO truncation at all.

- **D-10:** Standardize truncation reporting across all source-returning tools with a structured JSON footer, similar in spirit to `PaginationEnvelope` but semantically distinct (not list pagination). New helper: `TruncationEnvelope.AppendFooter(sb, totalLines, returnedLines, truncated)`. Footer format:
  ```
  [truncation:{"totalLines":N,"returnedLines":N,"truncated":true|false}]
  ```
  - Always appended (even when not truncated, `truncated:false` — consistent with pagination contract where `truncated` is always present).
  - Uses **line count** as the unit, not bytes — agents reason about lines, not bytes.
  - The byte-based `MaxDecompilationSize` cap stays as the enforcement mechanism. When triggered, compute the line count of the truncated output vs the full output and report in the footer.

- **D-11:** Apply truncation reporting to all 5 source-returning tools: `decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method`, `list_namespace_types`. For `disassemble_type` and `disassemble_method`: add the same `MaxDecompilationSize` byte cap that the decompile tools already have, plus the structured footer.

### Truncation reporting — bounded tools (PAGE-08)

- **D-12:** `export_project` already has `maxTypes=500` but does NOT report whether the cap was hit. Add a truncation footer to the output:
  ```
  [truncation:{"totalTypes":N,"exportedTypes":N,"truncated":true|false}]
  ```
  The `totalTypes` comes from counting types in the assembly (or filtered namespace). `exportedTypes` is the actual count written to disk (capped at `maxTypes`).

- **D-13:** `analyze_assembly` currently lists all public types and all namespaces with no cap. For large assemblies (1000+ public types), this could be enormous. Add a `maxTypes=200` display cap on the "Key Public Types" section (the full count is already reported in the header). Report:
  ```
  [truncation:{"totalPublicTypes":N,"displayedTypes":N,"truncated":true|false}]
  ```

### Claude's Discretion

- Whether to post-process `ReflectionDisassembler` output with regex/string replacement for token enhancement, or to use a custom `ITextOutput` wrapper — choose the approach with less coupling to ICSharpCode.Decompiler internals.
- Exact implementation of the surrounding IL window scanner for search_strings — whether to re-scan the method body with `BlobReader` or capture during the existing search scan pass (preferred for efficiency).
- Whether `TruncationEnvelope` lives in `Application/Pagination/` alongside `PaginationEnvelope` or in a new `Application/Output/` namespace — keep it close to PaginationEnvelope for discoverability.
- Plan structure: how to split the 7 requirements across plans. Natural grouping: (a) IL token resolution + resolveDeep, (b) search enrichment, (c) truncation reporting. Or by dependency wave.
- Test fixture design for verifying IL resolution output and surrounding IL windows.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design principles
- `.claude/skills/mcp-tool-design/skill.md` — Principles 2 (nested references), 3 (lazy agent), 4 (pagination mandatory for unbounded), 5 (rich but not flooding). These directly drive all 7 requirements in this phase.

### Pagination contract
- `docs/PAGINATION.md` — Canonical pagination contract. PAGE-07/PAGE-08 extend the "truncation visibility" concept to non-list tools.
- `Application/Pagination/PaginationEnvelope.cs` — Reference implementation for structured JSON footers. TruncationEnvelope follows the same pattern.

### Disassembly infrastructure
- `Domain/Services/IDisassemblyService.cs` — Port interface for IL disassembly; needs `resolveDeep` parameter added
- `Infrastructure/Decompiler/ILSpyDisassemblyService.cs` — Adapter using `ReflectionDisassembler`; main implementation target for IL-01/IL-02/IL-03

### Search infrastructure
- `Domain/Services/ISearchService.cs` — Port interface for string/constant search
- `Domain/Models/SearchResult.cs` — `StringSearchResult` and `ConstantSearchResult` records to enrich
- `Infrastructure/Decompiler/ILSpySearchService.cs` — Search implementation using `BlobReader` IL scanning; surrounding IL window captured here

### Source-returning tools (PAGE-07)
- `Application/UseCases/DecompileTypeUseCase.cs` — Already truncates at `MaxDecompilationSize` with ad-hoc message
- `Application/UseCases/DecompileMethodUseCase.cs` — Same truncation pattern
- `Application/UseCases/DisassembleTypeUseCase.cs` — No truncation currently
- `Application/UseCases/DisassembleMethodUseCase.cs` — No truncation currently
- `Application/UseCases/ListNamespaceTypesUseCase.cs` — Already truncates at `MaxDecompilationSize`

### Bounded tools (PAGE-08)
- `Application/UseCases/ExportProjectUseCase.cs` — Has `maxTypes=500` but no truncation reporting
- `Application/UseCases/AnalyzeAssemblyUseCase.cs` — No display cap or truncation reporting

### Configuration
- `Application/Configuration/ILSpyOptions.cs` — `MaxDecompilationSize` (1MB default byte cap)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PaginationEnvelope.AppendFooter(sb, total, returned, offset)` — Pattern to follow for TruncationEnvelope (structured JSON footer)
- `ReflectionDisassembler` from ICSharpCode.Decompiler — Already resolves most metadata tokens to readable IL names; the phase enhances gaps
- `ILSpySearchService` BlobReader scanning — Existing IL scan loop for strings/constants; surrounding IL window can be captured in the same pass
- `PlainTextOutput` — ITextOutput implementation used by disassembler; a wrapping implementation could intercept and enhance output
- `MaxDecompilationSize` byte cap — Already applied in 3 of 5 source tools; extend to remaining 2

### Established Patterns
- Domain models are `sealed record` with `required` properties and `init`-only collections
- Transport validates, use case accepts without re-validating
- Infrastructure adapters translate between ICSharpCode.Decompiler types and domain models
- Truncation currently uses ad-hoc string concatenation — standardize with TruncationEnvelope
- Search results already include `DeclaringType`, `MethodName`, `ILOffset` — add `MethodSignature` and `SurroundingInstructions`

### Integration Points
- `IDisassemblyService` interface — add `resolveDeep` parameter to both methods
- `ILSpyDisassemblyService` — implement enhanced resolution
- `StringSearchResult` / `ConstantSearchResult` records — add new fields
- `ILSpySearchService` — capture method signatures and surrounding IL during scan
- 5 source-tool use cases — replace ad-hoc truncation with TruncationEnvelope
- `ExportProjectUseCase.FormatOutput` — add truncation footer
- `AnalyzeAssemblyUseCase` — add display cap and truncation footer

</code_context>

<specifics>
## Specific Ideas

- `ReflectionDisassembler` output is already high quality — IL-01/IL-02 work is gap-filling (intra-assembly scope prefixes, generic parameter resolution), not a rewrite. Research should verify exact gaps by running the disassembler against a test assembly with cross-assembly calls, generics, and field accesses.
- The surrounding IL window for OUTPUT-06 should be captured during the existing BlobReader scan in `ILSpySearchService` rather than re-scanning — this is an efficiency optimization the researcher/planner should consider.
- `TruncationEnvelope` and `PaginationEnvelope` are siblings — same JSON footer pattern, different semantic domain. Keep them adjacent in the codebase.
- The `maxTypes` parameter on `export_project` is currently undocumented in terms of truncation visibility — PAGE-08 closes this gap.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 12-il-token-resolution-search-enrichment-truncation-reporting*
*Context gathered: 2026-04-10*
