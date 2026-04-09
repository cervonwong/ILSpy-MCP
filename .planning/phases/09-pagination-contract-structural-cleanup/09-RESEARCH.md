# Phase 9: Pagination Contract & Structural Cleanup - Research

**Researched:** 2026-04-09
**Domain:** C# / MCP tool surface hygiene + uniform pagination contract (docs + one reference implementation)
**Confidence:** HIGH (every claim below is verified against source files in the working tree; no web-sourced claims)

## Summary

Phase 9 is a low-risk, highly-constrained cleanup phase. The codebase already has the patterns needed: `SearchResults<T>` (Domain/Models/SearchResult.cs:6) carries `Results / TotalCount / Offset / Limit`; `SearchStringsTool` and `SearchConstantsTool` already use `maxResults=100, offset=0` as `[McpServerTool]` parameter defaults with matching `[Description]` strings; `SearchStringsUseCase.FormatResults` (lines 84-111) is the canonical "prose header + body" formatter that Phase 9 extends with the trailing `[pagination:{...}]` footer. No new libraries, no new domain services, no breaking changes at the `IDecompilerService` port — the `maxTypes` clamp today lives **only** at the Application use-case layer (`DecompileNamespaceUseCase.cs:101`, a `.Take(maxTypes)`), so pagination rewrite is entirely local to the use case.

The four deliverables (PAGE-01 doc, CLEAN-01 dispatcher delete, CLEAN-02 rename + first pagination implementation, CLEAN-03 README sync) touch exactly the file set CONTEXT.md anticipated, plus one additional file the planner should account for: **`Tests/Fixtures/ToolTestFixture.cs` — a second explicit DI registration list that must be updated in lockstep with `Program.cs`**. CONTEXT.md mentioned `Program.cs` but not the test fixture; the fixture mirrors `Program.cs` line-for-line and registers `AnalyzeReferencesTool` at line 87 and `DecompileNamespaceTool` at line 100. Both must change or tests fail to resolve.

One surprise about the roadmap ripple: **ROADMAP.md Phase 11 success criterion #4 (line 85) also literally names `maxTypes=200`** and will be stale after the rename, not just the `**Requirements**:` line (80). The planner's atomic edits must cover two lines in ROADMAP.md, not one.

TestTargets has no namespace with >100 types — the largest (`ILSpy.Mcp.TestTargets` root) holds roughly a dozen top-level types spread across 15 files. The pagination end-to-end test **requires a fixture expansion**: a new TestTargets source file containing a namespace with ~105-110 types (cheap to generate: empty classes in a loop-like layout). This was flagged in CONTEXT.md's "Deferred Ideas" as discretionary — the research finding is that it is **not discretionary** for CLEAN-02's pagination integration test to be meaningful. Recommend NOT deferring.

**Primary recommendation:** Split Phase 9 into two waves — Wave 1 runs three plans in parallel (CLEAN-01 delete, CLEAN-02 rename+pagination+PAGE-01 doc, TestTargets fixture expansion); Wave 2 runs CLEAN-03 README sync + atomic roadmap/requirements edits after Wave 1 merges. This matches Phase 8's proven pattern (Wave 1 parallel, Wave 2 depends) and keeps each plan under ~6-8 files.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Pagination contract — response envelope**

Format: every paginable tool returns a `Task<string>` with three sections in order:

```
[tool-specific natural-language header]

[results body]

[pagination:{"total":N,"returned":N,"offset":N,"truncated":bool,"nextOffset":N|null}]
```

- Leading header — tool-specific prose, not a canonical template. Precedent is `SearchStringsUseCase.FormatResults` line 95: `"String search for 'X': 523 total matches (showing 1-100)"`. Each tool writes its header in its own voice; the header is for human/LLM readability, not the parseable contract.
- Results body — the existing output format for each tool. No change to body shape in Phase 9 (enrichment is Phases 10-12).
- Trailing footer — the parseable contract:
  - Single line
  - Wrapped in `[pagination:...]`
  - Single-line minified JSON object
  - Fields (in this order): `total` (int), `returned` (int), `offset` (int), `truncated` (bool), `nextOffset` (int or `null`)
  - Always present on every response from a paginable tool — including zero-match responses and final-page responses.
  - `nextOffset` is a valid next offset when `truncated=true`, and explicit `null` when `truncated=false`. Never omitted.

Worked examples (these go into the doc):

```
// Zero matches
(no matching types found)

[pagination:{"total":0,"returned":0,"offset":0,"truncated":false,"nextOffset":null}]

// Mid-page
...body rows 1-100...

[pagination:{"total":523,"returned":100,"offset":0,"truncated":true,"nextOffset":100}]

// Final page
...body rows 501-523...

[pagination:{"total":523,"returned":23,"offset":500,"truncated":false,"nextOffset":null}]
```

**Pagination contract — parameters**

- `maxResults` (int, default `100`) — number of items per page
- `offset` (int, default `0`) — number of items to skip
- **Hard ceiling: `maxResults <= 500`.** A request with `maxResults > 500` is rejected with `McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.")`. Reject, do not silently clamp.
- Default is 100 uniformly across every paginated tool in the milestone.
- `offset >= total` → empty results body, `total` populated, `truncated=false`, `nextOffset=null`. Tool does not error.
- `maxResults <= 0` → reject with `McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.")`.

**CLEAN-01 — `analyze_references` hard delete**

- Delete `Transport/Mcp/Tools/AnalyzeReferencesTool.cs`, `Tests/Tools/AnalyzeReferencesToolTests.cs`, and any DI registration for the class.
- Keep `FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase` — the individual `find_*` tools still consume them.
- No deprecation alias.
- Verification: `grep -r "analyze_references\|AnalyzeReferencesTool" Transport/ Application/ Tests/` returns zero hits after the phase.

**CLEAN-02 — `decompile_namespace` → `list_namespace_types` (hard rename, no alias)**

Rename targets:
- `Transport/Mcp/Tools/DecompileNamespaceTool.cs` → `Transport/Mcp/Tools/ListNamespaceTypesTool.cs`
- Class: `DecompileNamespaceTool` → `ListNamespaceTypesTool`
- `[McpServerTool(Name = "decompile_namespace")]` → `[McpServerTool(Name = "list_namespace_types")]`
- `Application/UseCases/DecompileNamespaceUseCase.cs` → `Application/UseCases/ListNamespaceTypesUseCase.cs`
- Class: `DecompileNamespaceUseCase` → `ListNamespaceTypesUseCase`
- All DI registrations in `Program.cs`
- All test files renamed and internals updated
- Error message strings that reference the old tool name

Scope expansion — apply pagination contract during rename (diverges from naive roadmap reading):
- Replace the current `maxTypes = 200` parameter with the full contract: `maxResults = 100, offset = 0`.
- Parameter rename `maxTypes` → `maxResults` is part of the hard rename.
- Enforce the 500 ceiling.
- Emit the trailing JSON footer always.
- This tool becomes the canonical reference implementation of the contract.
- The contract doc (`docs/PAGINATION.md`) and this tool MUST land in the same wave so reviewers can verify the doc against running code.

Description rewrite is OUT OF SCOPE — Phase 13 (DESC-01) owns the full description sweep. Phase 9 changes ONLY the tool name in the attribute.

Test updates:
- Existing `DecompileNamespaceToolTests` assertions on `maxTypes` parameter must be updated to `maxResults`.
- Add at least one new test exercising the pagination contract end-to-end against a namespace with >100 types.

**CLEAN-03 — README.md update**

- Tool count: 28 → 27 (wherever the number appears).
- Delete the `#### \`analyze_references\`` section (README.md line 884).
- Rename the `#### \`decompile_namespace\`` section (README.md line 376) to `#### \`list_namespace_types\``; update the body to reflect `maxResults`, `offset` replacing `maxTypes`.
- Add a one-paragraph "Pagination" note near the top of the tool catalogue linking to `docs/PAGINATION.md`. Do not duplicate the contract in README — link only.
- Do not rewrite other tool descriptions in README. Phase 13 owns that sweep.
- Verification: `grep -n "analyze_references\|decompile_namespace\|28 tools" README.md` returns zero hits after the phase.

**PAGE-01 — documentation target**

- New file: `docs/PAGINATION.md`. The `docs/` directory exists but is currently empty (only `banner.png`), so this is a clean slate.
- Contents: the parameters, defaults, ceiling, rejection behavior, response envelope (with worked examples verbatim from the "worked examples" block above), and a one-paragraph scope note saying which tools are expected to implement it (Phase 9 = `list_namespace_types`, Phases 10-12 = all others).
- Cross-reference: update `.claude/skills/mcp-tool-design/SKILL.md` Principle 4 to point at `docs/PAGINATION.md` for the canonical format.
- Do NOT put the contract in `ARCHITECTURE.md` or `MCP.md`.

### Claude's Discretion

- Exact wording of the PAGINATION.md prose (the decisions above lock the shape; the prose is flexible).
- Exact wording of the rejection error message beyond the "maxResults cannot exceed 500. Use offset to paginate." guidance.
- Whether to bundle CLEAN-01 (dispatcher delete) and CLEAN-02 (rename + contract) into a single plan or split them. They touch different files and can run in parallel, but both must land before CLEAN-03 (README update). Small plan bias: split if it keeps each plan under ~6 files changed; combine if the rename is small enough to fit in one atomic commit with the delete.
- Whether to rename the existing `SearchResults<T>` record in `Domain/Models/SearchResult.cs:6` to something more generic like `PagedResult<T>` now that a second domain (namespace listing) uses it, or leave it as-is and reuse the same type. Either is fine; document the choice in the plan.
- Whether to add a `Returned` property to the existing `SearchResults<T>` record (it currently has `Results`, `TotalCount`, `Offset`, `Limit` — missing `Returned = Results.Count` would be a trivial convenience but not strictly required since the footer serializer can compute it).
- Which Phase 11 artifact (REQUIREMENTS.md PAGE-06 row, ROADMAP.md Phase 11 Plans line) to update to reflect that `list_namespace_types` pagination is landed in Phase 9. Propose the update in the plan; execute it atomically with the rename.

### Deferred Ideas (OUT OF SCOPE)

- **Retrofit `search_strings` / `search_constants` with the trailing JSON footer.** These tools already have `maxResults` / `offset` but emit only the natural-language header today. Defer to Phase 12.
- **Rename `SearchResults<T>` → `PagedResult<T>`.** Not required for Phase 9 to close its goal; decide in the plan.
- **`PaginationEnvelope` helper extraction.** Phase 9 only applies the contract to one tool; extract the helper when the second tool in Phase 10 shows the copy-paste pain.
- **Description rewrites.** Phase 13 owns the sweep.
- **Automated tool count assertion** (test that fails if `tools/list` count != 27). Nice-to-have but not required by Phase 9's success criteria.
- **Expand `TestTargets` fixture** if no existing fixture has a namespace with >100 types. Scope this in the plan if needed.

**Research note:** the "Expand TestTargets fixture" item is in the user's discretionary/deferred list, but Research concludes (see Test Fixture Assessment below) that it is **effectively required** for CLEAN-02's pagination integration test to be meaningful. The planner should either lift it out of deferred and into scope, or explicitly justify why a <12-type namespace is sufficient to exercise pagination transitions.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PAGE-01 | Uniform pagination contract defined and documented — every list-returning tool accepts `(maxResults, offset)` with sensible defaults, returns `(truncated: bool, total: int)`; documented as a reusable pattern | New file `docs/PAGINATION.md` (empty `docs/` dir confirmed, only `banner.png` present). Contract shape already locked in CONTEXT.md; skill SKILL.md Principle 4 (lines 54-68) gets a cross-reference to the new file. No code changes required by PAGE-01 alone — it is the prose that CLEAN-02 proves out. |
| CLEAN-01 | `analyze_references` dispatcher tool removed; four `find_*` tools become the sole cross-reference entry points (tool count: 28 → 27) | Exactly three source files to delete: `Transport/Mcp/Tools/AnalyzeReferencesTool.cs`, `Tests/Tools/AnalyzeReferencesToolTests.cs`. Two DI-registration lines to delete: `Program.cs:188` and `Tests/Fixtures/ToolTestFixture.cs:87`. Grep confirms no other source references. `FindUsagesUseCase` / `FindImplementorsUseCase` / `FindDependenciesUseCase` / `FindInstantiationsUseCase` all stay — the four individual `find_*` tools already consume them directly. |
| CLEAN-02 | `decompile_namespace` renamed to `list_namespace_types` — surface matches actual behavior | Four source files to rename: the tool, the use case, the tool test, and (if introduced) the use case test. Two DI sites update: `Program.cs` lines 171 (use case) + 201 (tool) AND `Tests/Fixtures/ToolTestFixture.cs` lines 71 + 100. Domain layer is untouched — `IDecompilerService.ListTypesAsync` already returns the full list unfiltered; the `.Take(maxTypes)` clamp lives only at `DecompileNamespaceUseCase.cs:101`. Pagination contract application rewrites that same method to: fetch all, compute `totalCount = exactMatches.Count`, slice via `.Skip(offset).Take(maxResults)`, construct `SearchResults<TypeSummaryEntry>` (or new `PagedResult<T>`), serialize with trailing `[pagination:{...}]` footer. |
| CLEAN-03 | README.md and any cross-references updated to match the new surface (27 tools, renamed namespace tool, no dispatcher) | Exact README line numbers: line 58 (tool count "28 tools"), line 376 (`#### \`decompile_namespace\`` section heading), line 384 (`maxTypes` parameter table row), line 884 (`#### \`analyze_references\`` section heading and surrounding example block ending near line 915). `ARCHITECTURE.md` and `MCP.md` have zero references to either tool name (verified by grep). Skill file `.claude/skills/mcp-tool-design/SKILL.md` mentions `analyze_references` once at line 93 (in Principle 7 as the "dispatcher antipattern example") — **does NOT need removal** (historical reference to the audit finding). Principle 4 lines 54-68 need a cross-reference addition to `docs/PAGINATION.md`. |
</phase_requirements>

## Standard Stack

Phase 9 adds **zero** new packages. All capabilities are already in the tree.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ModelContextProtocol | (current, unchanged) | `[McpServerToolType]` / `[McpServerTool]` attribute-based tool registration | The 27 surviving tools use this pattern; CLEAN-02's rename only changes the `Name` parameter on one attribute |
| Microsoft.Extensions.DependencyInjection | (current, unchanged) | Explicit scoped service registration in `Program.cs` and `ToolTestFixture.cs` | Both files are long `services.AddScoped<...>()` lists; Phase 9 adds / removes / renames exactly three lines in each |
| xUnit 2.9.x | (current) | Test framework; `[Fact]` and `[Collection("ToolTests")]` | All tool tests already follow this shape. New pagination test is one more `[Fact]` in the renamed test file. |
| FluentAssertions 8.x | (current) | `result.Should().Contain(...)`, `.Should().ThrowAsync<McpToolException>()` | Precedent in every test file; no new matchers needed — a Regex-based `.Should().MatchRegex(...)` assertion is available for footer-shape checks |
| System.Text.Json | (built-in) | Minified JSON serialization for the `[pagination:{...}]` footer | Already available via .NET 9 BCL. `JsonSerializerOptions` with `WriteIndented = false` produces the required single-line minified output. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Text.StringBuilder` | (BCL) | `FormatResults`-style composition | Already used in `SearchStringsUseCase.FormatResults` — extend that pattern with a `sb.AppendLine(); sb.Append("[pagination:"); sb.Append(json); sb.Append("]");` stanza |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json for the footer | Hand-rolled string interpolation | Hand-rolled is fewer lines but fragile (e.g., embedded quotes in future paginated tools). Use `JsonSerializer.Serialize(new { total, returned, offset, truncated, nextOffset })` with a minimal options bag. |
| Reuse `SearchResults<T>` | Introduce `PagedResult<T>` | Both are acceptable per CONTEXT.md discretion. Reuse is zero risk and keeps the diff small; rename is a bigger payoff if Phases 10-12 will use the same type for non-search domains. **Recommendation:** reuse for now, add a `Returned` property, defer the rename to Phase 10 or when the second non-search consumer lands — keeps Phase 9 small. |

**Installation:**
```
(none — Phase 9 adds no new packages)
```

## Architecture Patterns

### Recommended Project Structure

No structural changes. The existing layered layout is preserved exactly:

```
Transport/Mcp/Tools/           # [McpServerTool] thin handlers
  ListNamespaceTypesTool.cs    # NEW (renamed from DecompileNamespaceTool.cs)
  (AnalyzeReferencesTool.cs)   # DELETED
  SearchStringsTool.cs         # unchanged (precedent for maxResults/offset params)
  SearchConstantsTool.cs       # unchanged (same precedent)
  ...

Application/UseCases/          # use-case orchestration
  ListNamespaceTypesUseCase.cs # NEW (renamed from DecompileNamespaceUseCase.cs)
                               #   body rewritten: no more .Take(maxTypes);
                               #   instead .Skip(offset).Take(maxResults) +
                               #   SearchResults<TypeSummaryEntry> wrap + footer
  SearchStringsUseCase.cs      # unchanged (source of FormatResults pattern)
  ...

Domain/
  Models/
    SearchResult.cs            # unchanged OR +Returned property (discretion)
    NamespaceTypeSummary.cs    # unchanged (internal shape still useful)
  Services/
    IDecompilerService.cs      # UNCHANGED — ListTypesAsync already returns
                               #   all types; no signature change needed
  Errors/                      # unchanged

Tests/
  Tools/
    ListNamespaceTypesToolTests.cs  # NEW (renamed + expanded)
    (AnalyzeReferencesToolTests.cs) # DELETED
  Fixtures/
    ToolTestFixture.cs              # UPDATED DI lines (71, 87, 100)

docs/
  banner.png                        # unchanged
  PAGINATION.md                     # NEW (clean-slate contract doc)

.claude/skills/mcp-tool-design/
  SKILL.md                          # UPDATED Principle 4 cross-reference
```

### Pattern 1: Attribute-based MCP tool registration + explicit DI

**What:** Each tool class is decorated with `[McpServerToolType]` on the class and `[McpServerTool(Name = "...")]` on `ExecuteAsync`. The MCP SDK's `WithToolsFromAssembly()` auto-discovers these at startup. **In addition**, every tool class is explicitly `AddScoped<>()`-registered in `Program.cs` RegisterServices (line 174+) AND in `Tests/Fixtures/ToolTestFixture.cs` (line 73+), because both code paths need DI to resolve the tool class.

**When to use:** Every MCP tool in the project. CLEAN-01 and CLEAN-02 both need to update **both** registration sites.

**Example:**
```csharp
// Source: Transport/Mcp/Tools/SearchStringsTool.cs (verified 2026-04-09)
[McpServerToolType]
public sealed class SearchStringsTool
{
    private readonly SearchStringsUseCase _useCase;
    private readonly ILogger<SearchStringsTool> _logger;

    public SearchStringsTool(SearchStringsUseCase useCase, ILogger<SearchStringsTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "search_strings")]
    [Description("Search for string literals in assembly IL bytecode matching a regex pattern. ...")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Regex pattern to match against string literals ...")] string pattern,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, pattern, maxResults, offset, cancellationToken);
        }
        catch (ArgumentException ex) { /* ... */ }
        // ... catch chain identical across tools
    }
}
```

**Use verbatim for `ListNamespaceTypesTool`:** the two `[Description]` strings on `maxResults` and `offset` parameters — `"Maximum number of results to return (default: 100)"` and `"Number of results to skip for pagination (default: 0)"` — match this precedent exactly. Do not invent new wording.

### Pattern 2: "Header + body" formatter in the use case layer

**What:** The use case's `FormatResults(...)` static method builds a `StringBuilder`, appends a natural-language header that summarizes counts, appends the body (one result per line), and returns the string. The tool just returns what the use case returns.

**When to use:** Every paginated tool in Phases 9-12. Phase 9 extends this pattern by adding a trailing footer stanza.

**Example (the pattern to extend):**
```csharp
// Source: Application/UseCases/SearchStringsUseCase.cs:84-111 (verified 2026-04-09)
private static string FormatResults(string pattern, SearchResults<StringSearchResult> results)
{
    var sb = new System.Text.StringBuilder();

    int rangeEnd = Math.Min(results.Offset + results.Results.Count, results.TotalCount);
    if (results.TotalCount == 0)
    {
        sb.AppendLine($"String search for '{pattern}': 0 total matches");
    }
    else
    {
        sb.AppendLine($"String search for '{pattern}': {results.TotalCount} total matches (showing {results.Offset + 1}-{rangeEnd})");
    }
    sb.AppendLine();

    if (results.Results.Count == 0)
    {
        sb.AppendLine("No matching strings found in the assembly.");
        return sb.ToString();
    }

    foreach (var result in results.Results)
    {
        sb.AppendLine($"  \"{result.MatchedValue}\" in {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
    }

    return sb.ToString();
}
```

**Phase 9 extension:** after the foreach, append a blank line and the footer:
```csharp
sb.AppendLine();  // separator line
sb.Append("[pagination:");
sb.Append(JsonSerializer.Serialize(new
{
    total = results.TotalCount,
    returned = results.Results.Count,
    offset = results.Offset,
    truncated = results.Offset + results.Results.Count < results.TotalCount,
    nextOffset = (results.Offset + results.Results.Count < results.TotalCount)
        ? (int?)(results.Offset + results.Results.Count)
        : null,
}));
sb.Append(']');
```

Note: Phase 9 applies this footer ONLY inside `ListNamespaceTypesUseCase.FormatResults` / `FormatOutput`. `SearchStringsUseCase` and `SearchConstantsUseCase` do **not** receive the footer in this phase (per CONTEXT.md "What NOT to touch" list — defer to Phase 12 which owns OUTPUT-06/07).

### Pattern 3: Use-case pagination slicing

**What:** The use case fetches the full result set from the domain port, computes `totalCount` from the unsliced collection, then slices via `.Skip(offset).Take(maxResults)` before building the output-model entries. `SearchResults<T>` or `PagedResult<T>` carries `{ Results, TotalCount, Offset, Limit }` out to the formatter.

**When to use:** Every paginated tool. Phase 9 applies it once (`ListNamespaceTypesUseCase`).

**Example (the target rewrite for DecompileNamespaceUseCase.cs:60-121):**
```csharp
// Current (before rename):
var sorted = topLevelTypes
    .OrderBy(t => KindOrder.GetValueOrDefault(t.Kind, 5))
    .ThenBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
    .Take(maxTypes)   // <-- clamps, discards total visibility
    .ToList();

// After (CLEAN-02):
var allSorted = topLevelTypes
    .OrderBy(t => KindOrder.GetValueOrDefault(t.Kind, 5))
    .ThenBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
    .ToList();
var totalCount = allSorted.Count;
var page = allSorted
    .Skip(offset)
    .Take(maxResults)
    .ToList();
var pagedResult = new SearchResults<TypeInfo>
{
    Results = page,
    TotalCount = totalCount,
    Offset = offset,
    Limit = maxResults,
};
// ... build entries from pagedResult.Results, then format with envelope
```

**Important subtlety:** nested types are currently grouped under their declaring type via `nestedByParent`. Pagination must apply to **top-level types only** (the unit of pagination is the top-level type), not to nested types. The `nestedByParent` lookup stays as-is and each selected top-level entry still carries its full nested-type tree. This matches user intuition ("I asked for 100 types, I got 100 top-level entries, their nested children are informational").

### Anti-Patterns to Avoid

- **Silently clamping at the domain layer.** `IDecompilerService.ListTypesAsync` currently has no `limit` parameter (verified in `Domain/Services/IDecompilerService.cs:26-29`). Do not add one. The pagination slicing MUST live at the use-case layer so the `total` is known before slicing.
- **Throwing `McpToolException` from the use case.** `McpToolException` lives in `Transport/Mcp/Errors/McpToolException.cs` and is **Transport-layer**. The Phase 8 DEBT-02 fix (see `08-01-PLAN.md`) specifically removed a Transport-layer import from the Application layer. Phase 9 must honor that: the ceiling-check rejection either happens in the tool class (`ListNamespaceTypesTool.ExecuteAsync`) **before** calling the use case, or the use case throws a domain-level `ArgumentException` / a new narrow domain exception that the tool maps. Recommendation: keep the ceiling check in the **tool** class for simplicity and parallelism with how `SearchStringsTool` handles `ArgumentException` from regex failures — the tool already has a `try`/`catch` chain that maps to `McpToolException`.
- **Duplicating the pagination contract in README.** CLEAN-03 adds a one-paragraph link, not a copy. The source of truth is `docs/PAGINATION.md`. Copy-paste drift is the exact pitfall that made CONTEXT.md insist on a dedicated file.
- **Using `WithToolsFromAssembly()` as the sole registration.** It discovers `[McpServerToolType]` classes for MCP SDK tool dispatch, but DI still needs the explicit `AddScoped<ToolClass>()` for constructor-injected services to resolve. `Program.cs` shows both patterns in use. Deleting ONLY `AddScoped<AnalyzeReferencesTool>()` while leaving the file would orphan the class but not remove it from MCP discovery — so CLEAN-01 must delete both the class file AND the registration line.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Minified JSON footer | Hand-rolled `$"{{\"total\":{n},..."` string interpolation | `System.Text.Json.JsonSerializer.Serialize(new { total, returned, offset, truncated, nextOffset })` | Future tools may have field values that need JSON escaping (null, unicode, etc.). BCL is zero cost and always correct. |
| Pagination slicing | Custom extension methods, index math | Plain LINQ `.Skip(offset).Take(maxResults)` on the `.ToList()`-materialized full set | Matches the in-tree `SearchStringsUseCase` / `SearchConstantsUseCase` precedent. No new abstractions. |
| Paged result envelope type | New record per use case | Reuse `Domain/Models/SearchResult.cs:6` `SearchResults<T>` | Already has `Results`, `TotalCount`, `Offset`, `Limit`. Either rename to `PagedResult<T>` (discretionary) or add a `Returned` property (discretionary). |
| Tool-count verification | Automated runtime assertion that `tools/list` returns 27 | Manual verification via `dotnet run --transport stdio` one-off OR grep on tool source files | Marked as a "nice-to-have" deferred in CONTEXT.md. A grep-based verification in the plan's "Verification" task is sufficient for Phase 9. |
| Test fixture explicit DI duplication | Refactor `ToolTestFixture` to share registration with `Program.cs` via a helper | Update both lists in parallel | Refactoring the fixture to share code with `Program.cs` is tempting but out of scope — it is tech debt not owned by Phase 9. Just update both. |

**Key insight:** Phase 9 is **subtractive and surgical**, not architectural. Every "do not build" above is "resist the urge to generalize in this phase." The `PaginationEnvelope` helper extraction, the `PagedResult<T>` rename, the shared-DI refactor, and the tool-count regression test are all fine ideas — they belong in Phase 10 (first evidence of copy-paste), Phase 10-11 (second paginated use case), backlog tech debt, and backlog tech debt respectively.

## Common Pitfalls

### Pitfall 1: Forgetting the second DI registration list
**What goes wrong:** Update `Program.cs` DI lines but not `Tests/Fixtures/ToolTestFixture.cs` — test build breaks with "unable to resolve `DecompileNamespaceTool`" when the old class is renamed, OR a `Cannot add service of type X to collection twice` error if the old and new both register.
**Why it happens:** CONTEXT.md mentions `Program.cs` under "Integration Points" but does not explicitly name the test fixture. Both files are mechanical copies of each other (verified: lines 174-201 in `Program.cs` match lines 73-100 in `ToolTestFixture.cs` almost line-for-line).
**How to avoid:** Treat the two files as a single logical "DI registration list" and edit them in the same task. Grep `grep -n "AddScoped<.*Tool>" Program.cs Tests/Fixtures/ToolTestFixture.cs` before closing the plan and confirm symmetric changes.
**Warning signs:** Tests fail to resolve at `_fixture.CreateScope().ServiceProvider.GetRequiredService<...>()` calls in `[Fact]` methods.

### Pitfall 2: Nested-type pagination mental model drift
**What goes wrong:** Pagination slicing is applied to the full type list (including nested types flattened) instead of top-level types, breaking the existing `BuildEntry` recursion that expects top-level as the unit of work.
**Why it happens:** Current code at `DecompileNamespaceUseCase.cs:77-95` splits `exactMatches` into `nestedByParent` and `topLevelTypes`. The `.Take(maxTypes)` at line 101 operates only on `topLevelTypes`. If someone rewrites the method to slice earlier (on `exactMatches`), they will break nested-type grouping and the nested tests (`NestedTypesIndentedUnderParent` at DecompileNamespaceToolTests.cs:58-72).
**How to avoid:** Keep the nested/top-level split exactly as-is. Apply `.Skip(offset).Take(maxResults)` to the **sorted `topLevelTypes` list**. `totalCount` is `topLevelTypes.Count` (NOT `exactMatches.Count`, which includes nested types — that is the current reported count and is misleading per the existing `NamespaceTypeSummary.TotalTypeCount` semantic).
**Warning signs:** The existing `NestedTypesIndentedUnderParent` test fails after refactor, or `total` in the pagination footer is unexpectedly larger than the visible type list.
**Decision for planner:** Document in the plan whether `total` reports "top-level types in namespace" (new, cleaner) or "all types including nested" (matches current `TotalTypeCount` but is weird for pagination). Recommend the former because agents paging a namespace care about the unit they can ask for next. The existing `NamespaceTypeSummary.TotalTypeCount = exactMatches.Count` at line 111 should be **changed** to `= topLevelTypes.Count` for consistency.

### Pitfall 3: Header `(showing X-Y)` range math off-by-one on empty pages
**What goes wrong:** `SearchStringsUseCase.FormatResults` prints `(showing 1-100)` when `Offset=0, Results.Count=100`. If the agent passes `offset=1000` and there are only 5 total, the current code would print `(showing 1001-5)` — nonsense.
**Why it happens:** The existing code at line 88 (`rangeEnd = Math.Min(results.Offset + results.Results.Count, results.TotalCount)`) works when `results.Results.Count > 0`, but `offset + 1` in the header becomes `1001` with no results — the `if (results.TotalCount == 0)` branch at line 89 covers the "no matches at all" case but not the "matches exist but offset >= total" case.
**How to avoid:** Before using `SearchStringsUseCase.FormatResults` as the header template for `ListNamespaceTypesUseCase`, add a third branch: `if (results.Results.Count == 0 && results.TotalCount > 0)` prints `"Namespace: X ({total} types total, offset {offset} beyond last page)"` or similar. The pagination footer is still correct (`returned=0, truncated=false, nextOffset=null`) regardless of header.
**Warning signs:** Integration test "offset beyond total" produces a confusing header. Catch it with the test case listed under Validation Architecture.

### Pitfall 4: Ceiling-check placement violates layering
**What goes wrong:** The `maxResults > 500` check is placed in the use case, which then throws `McpToolException`. This re-introduces the exact Transport-leak-into-Application violation that Phase 8 DEBT-02 fixed.
**Why it happens:** It is the most natural place to put validation — right next to the parameter it validates.
**How to avoid:** Put the ceiling check in the **tool** class (`ListNamespaceTypesTool.ExecuteAsync`) at the very top of the `try` block, before calling the use case. The tool already imports `ILSpy.Mcp.Transport.Mcp.Errors` and throws `McpToolException` in its catch chain; adding an up-front `if (maxResults > 500) throw new McpToolException("INVALID_PARAMETER", "...")` is zero layer-violation cost.
**Warning signs:** Grep `Application/UseCases/ListNamespaceTypesUseCase.cs` for `McpToolException` — should be zero hits. Grep `using ILSpy.Mcp.Transport` in that file — should be zero hits.

### Pitfall 5: `NamespaceNotFoundException` pre-empts the zero-page case
**What goes wrong:** Current code at `DecompileNamespaceUseCase.cs:71-74` throws `NamespaceNotFoundException` when `exactMatches.Count == 0`. The pagination contract says "`offset >= total` → empty results body, `total` populated, `truncated=false`, `nextOffset=null`. Tool does not error." If the namespace doesn't exist, these two rules collide.
**Why it happens:** "Zero types in the namespace because the namespace doesn't exist" and "zero types on this page because offset is past end" are semantically different, but both look like "zero results" to the formatter.
**How to avoid:** Keep the `NamespaceNotFoundException` throw when `exactMatches.Count == 0`. It is correct behavior — the agent asked about a namespace that isn't there, and the error code `NAMESPACE_NOT_FOUND` tells it exactly that. The "zero page" contract rule applies when the namespace **does** exist but the `offset` skips past all top-level types in it. That is a different code path: `topLevelTypes.Count > 0 && offset >= topLevelTypes.Count` → return a formatted response with `total=topLevelTypes.Count, returned=0, offset=offset, truncated=false, nextOffset=null`.
**Warning signs:** Integration test "offset beyond total" throws instead of returning an envelope. Test "invalid namespace" stops throwing `NamespaceNotFoundException`.

## Code Examples

### Ceiling rejection in the tool class (Pattern location: `ListNamespaceTypesTool.ExecuteAsync`)

```csharp
// Location: Transport/Mcp/Tools/ListNamespaceTypesTool.cs (new file)
[McpServerTool(Name = "list_namespace_types")]
[Description("Lists all types in a namespace with full signatures, member counts, and public method signatures. Returns a summary -- use decompile_type to get full source for individual types.")]  // unchanged — Phase 13 owns description sweep
public async Task<string> ExecuteAsync(
    [Description("Path to the .NET assembly file")] string assemblyPath,
    [Description("Full namespace name (e.g., 'System.Collections.Generic')")] string namespaceName,
    [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
    [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Phase 9 pagination contract: hard ceiling + positive minimum.
        // Validation at the Transport boundary (not the use case) to preserve
        // the layering Phase 8 DEBT-02 restored.
        if (maxResults > 500)
        {
            throw new McpToolException("INVALID_PARAMETER",
                "maxResults cannot exceed 500. Use offset to paginate.");
        }
        if (maxResults <= 0)
        {
            throw new McpToolException("INVALID_PARAMETER",
                "maxResults must be >= 1.");
        }

        return await _useCase.ExecuteAsync(assemblyPath, namespaceName, maxResults, offset, cancellationToken);
    }
    // ... existing catch chain (NamespaceNotFoundException, AssemblyLoadException, TimeoutException,
    //     OperationCanceledException, Exception) preserved verbatim from DecompileNamespaceTool.cs:39-63
}
```

### Pagination footer composition in the use case

```csharp
// Location: Application/UseCases/ListNamespaceTypesUseCase.cs (renamed file, rewritten FormatOutput)
using System.Text.Json;

// Inside ListNamespaceTypesUseCase:
private static string FormatOutput(
    NamespaceTypeSummary summary,
    int totalTopLevelTypes,
    int offset,
    int maxResults)
{
    var sb = new StringBuilder();

    // Header — tool-specific prose per contract
    var rangeStart = summary.Types.Count == 0 ? 0 : offset + 1;
    var rangeEnd = offset + summary.Types.Count;
    if (totalTopLevelTypes == 0)
    {
        sb.AppendLine($"Namespace: {summary.Namespace} (0 types)");
    }
    else if (summary.Types.Count == 0)
    {
        sb.AppendLine($"Namespace: {summary.Namespace} ({totalTopLevelTypes} types total, offset {offset} is beyond last page)");
    }
    else
    {
        sb.AppendLine($"Namespace: {summary.Namespace} ({totalTopLevelTypes} top-level types, showing {rangeStart}-{rangeEnd})");
    }

    // Body — existing grouping preserved
    var groups = summary.Types
        .GroupBy(t => t.Kind)
        .OrderBy(g => KindOrder.GetValueOrDefault(g.Key, 5));
    foreach (var group in groups)
    {
        sb.AppendLine();
        sb.AppendLine($"{GetKindGroupName(group.Key)}:");
        foreach (var entry in group)
        {
            WriteEntry(sb, entry, indent: "  ");
        }
    }

    // Footer — the parseable contract
    var returned = summary.Types.Count;
    var truncated = offset + returned < totalTopLevelTypes;
    int? nextOffset = truncated ? offset + returned : null;

    var footerPayload = JsonSerializer.Serialize(new
    {
        total = totalTopLevelTypes,
        returned,
        offset,
        truncated,
        nextOffset,
    });

    sb.AppendLine();
    sb.Append("[pagination:");
    sb.Append(footerPayload);
    sb.Append(']');

    return sb.ToString();
}
```

### PAGINATION.md skeleton

```markdown
# Pagination Contract

This document defines the single, uniform pagination contract that every list-returning MCP tool in this project obeys. It is the canonical reference; the skill at `.claude/skills/mcp-tool-design/SKILL.md` Principle 4 and `README.md` both link here rather than duplicate the rules.

## Parameters

Every paginable tool accepts two parameters with the following defaults:

- `maxResults` (int, default **100**) — number of items returned per page.
- `offset` (int, default **0**) — number of items to skip before returning `maxResults`.

## Hard Ceiling

`maxResults` has a hard ceiling of **500**. A request with `maxResults > 500` is rejected with:

```
McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.")
```

A request with `maxResults <= 0` is rejected with:

```
McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.")
```

The ceiling is enforced by rejection, not silent clamping, so agents discover the limit on first violation and can switch to `offset`-based pagination immediately.

## Response Envelope

Every paginable tool returns a `Task<string>` with three sections in order:

1. **Header** — tool-specific natural-language prose summarizing counts and the visible range. Each tool writes its header in its own voice.
2. **Body** — the tool's existing result format, one item per logical row. Unchanged by this contract.
3. **Footer** — a single line wrapped in `[pagination:...]` containing a minified JSON object. Always present. Never omitted.

### Footer Shape

```
[pagination:{"total":N,"returned":N,"offset":N,"truncated":bool,"nextOffset":N|null}]
```

Fields, in order:

| Field | Type | Meaning |
|-------|------|---------|
| `total` | int | Total number of items across all pages (the unsliced result size). |
| `returned` | int | Items actually present in this response's body. |
| `offset` | int | The offset this response was computed at (echoes the request parameter). |
| `truncated` | bool | `true` if more items exist beyond this page, `false` otherwise. |
| `nextOffset` | int or `null` | When `truncated=true`: the offset to request for the next page. When `truncated=false`: explicit `null`. Never omitted. |

### Edge Cases

- **Zero matches** (`total=0`): the body is empty or a short "no matches" message, and the footer still appears with `total=0, returned=0, truncated=false, nextOffset=null`.
- **Final page** (`offset + returned == total`): `truncated=false, nextOffset=null`.
- **Offset beyond total** (`offset >= total`, but `total > 0`): the body is empty, `returned=0, truncated=false, nextOffset=null`. The tool does **not** throw.

## Worked Examples

```
// Zero matches
(no matching types found)

[pagination:{"total":0,"returned":0,"offset":0,"truncated":false,"nextOffset":null}]

// Mid-page
...body rows 1-100...

[pagination:{"total":523,"returned":100,"offset":0,"truncated":true,"nextOffset":100}]

// Final page
...body rows 501-523...

[pagination:{"total":523,"returned":23,"offset":500,"truncated":false,"nextOffset":null}]
```

## Scope

As of Phase 9 (v1.2.0), the following tool implements this contract:

- `list_namespace_types` — the canonical reference implementation.

Phases 10-12 will extend the contract to:

- All `find_*` tools (Phase 10 / PAGE-02).
- `list_assembly_types`, `list_embedded_resources`, `get_type_members`, `search_members_by_name` (Phase 11 / PAGE-03, PAGE-04, PAGE-05).
- `search_strings`, `search_constants` (Phase 12 / OUTPUT-06, OUTPUT-07) — note: these already expose `maxResults` and `offset` parameters; Phase 12 adds the trailing footer.

Source-returning tools (`decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method`) and bounded-output tools (`export_project`, `analyze_assembly`) use a related but simpler `(truncated, total_lines)` pattern defined separately (PAGE-07, PAGE-08) because their pagination unit is lines of source, not list items.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `maxTypes = 200` silent clamp | `maxResults / offset` + footer envelope | Phase 9 (this phase) | First tool (`list_namespace_types`) to drop silent truncation in favor of visibility. Sets the precedent for Phases 10-12. |
| `analyze_references` dispatcher | Four direct `find_*` tools | Phase 9 (this phase) | Principle 7 (One Tool, One Job) enforced. Audit's called-out antipattern removed. Tool count 28 → 27. |
| Mixed use-case / transport exception flow | `McpToolException` lives only in Transport | Phase 8 (DEBT-02) | Relevant constraint for Phase 9: ceiling rejection must stay in the tool class, not the use case. |

**Deprecated/outdated:**
- `DecompileNamespaceTool` / `DecompileNamespaceUseCase` class names — replaced by `ListNamespaceTypesTool` / `ListNamespaceTypesUseCase`.
- `maxTypes` parameter everywhere in the `list_namespace_types` code path — replaced by `maxResults` + `offset`.
- The `AnalyzeReferencesTool` dispatcher pattern — gone entirely. No alias.

## Open Questions

1. **Should `NamespaceTypeSummary.TotalTypeCount` report `topLevelTypes.Count` or `exactMatches.Count`?**
   - What we know: current code at `DecompileNamespaceUseCase.cs:111` assigns `TotalTypeCount = exactMatches.Count` (all types including nested).
   - What's unclear: the pagination `total` in the footer must report the unit being paginated, which is top-level types. If `TotalTypeCount` and pagination `total` disagree, the header text and footer contradict each other.
   - Recommendation: Change `TotalTypeCount` to `topLevelTypes.Count` as part of CLEAN-02. Update existing `ListsTypesInNamespace` test assertion (`result.Should().Contain("types)")` at line 30) to tolerate either shape — it doesn't pin the number. Impact is contained to one file.

2. **Should `SearchResults<T>` gain a `Returned` property, rename to `PagedResult<T>`, or stay as-is?**
   - What we know: CONTEXT.md leaves all three options to Claude's discretion.
   - Recommendation: **Leave as-is for Phase 9.** Adding `Returned` is harmless but unnecessary (the formatter can compute `Results.Count`). Renaming is bigger than the phase needs. Revisit in Phase 10 when the second consumer (`find_*` pagination) lands.

3. **Does `ExportProjectTool`'s `maxTypes=500` parameter collide with the name choice?**
   - What we know: `ExportProjectTool.cs:33` has `int maxTypes = 500`, and `README.md:1334` documents it. `ExportProjectUseCase.cs:35` mirrors it.
   - What's unclear: after Phase 9, the only `maxTypes` parameter left in the whole tool surface is in `export_project`. That's a minor wart but not a Phase 9 concern — `export_project` is pagination-PAGE-08 in Phase 12 and the name will be revisited then.
   - Recommendation: Leave `export_project`'s `maxTypes` untouched in Phase 9. Note in the plan that the inconsistency is intentional and time-bound to Phase 12.

4. **Does the test fixture expansion need a dedicated C# file per test namespace, or can one file host multiple test-namespace declarations?**
   - What we know: C# allows multiple `namespace` declarations in a single file. `TestTargets/Types/*.cs` currently uses one namespace per file, but not dogmatically.
   - Recommendation: Create a single new file `TestTargets/Types/PaginationTestTargets.cs` with `namespace ILSpy.Mcp.TestTargets.Pagination;` containing 105 empty `public class TypeN {}` declarations. 105 is the smallest number that proves "default page=100 → page 1 truncated=true, page 2 truncated=false". Cheaper than 200.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.x + FluentAssertions 8.x |
| Config file | `Tests/ILSpy.Mcp.Tests.csproj` (standard xUnit package references; no special config) |
| Test collection | `[Collection("ToolTests")]` via `Tests/Fixtures/ToolTestCollection.cs` + `Tests/Fixtures/ToolTestFixture.cs` |
| Quick run command | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypes"` |
| Full suite command | `dotnet test ILSpy.Mcp.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PAGE-01 | `docs/PAGINATION.md` exists and documents the contract | doc check | `test -f docs/PAGINATION.md && grep -q "\\[pagination:" docs/PAGINATION.md && grep -q "maxResults" docs/PAGINATION.md && grep -q "nextOffset" docs/PAGINATION.md` | ❌ Wave 0 (manual grep in plan verification task; no code test) |
| PAGE-01 | Skill Principle 4 cross-references `docs/PAGINATION.md` | doc check | `grep -q "docs/PAGINATION.md" .claude/skills/mcp-tool-design/SKILL.md` | ❌ Wave 0 (manual grep) |
| CLEAN-01 | `AnalyzeReferencesTool.cs` file removed | build / grep | `! test -f Transport/Mcp/Tools/AnalyzeReferencesTool.cs` | ❌ Wave 0 (grep) |
| CLEAN-01 | `AnalyzeReferencesToolTests.cs` file removed | build / grep | `! test -f Tests/Tools/AnalyzeReferencesToolTests.cs` | ❌ Wave 0 (grep) |
| CLEAN-01 | No `AnalyzeReferencesTool` references remain in source or tests | grep | `! grep -rq "AnalyzeReferencesTool\\|analyze_references" Transport/ Application/ Tests/ Domain/ Infrastructure/ Program.cs` | ❌ Wave 0 (grep) |
| CLEAN-01 | Solution still builds after delete | build | `dotnet build ILSpy.Mcp.sln` | ✅ existing |
| CLEAN-01 | All remaining tests still pass (no collateral damage) | full suite | `dotnet test ILSpy.Mcp.sln` | ✅ existing (173/173 baseline from Phase 8) |
| CLEAN-02 | `ListNamespaceTypesTool` class exists with `[McpServerTool(Name = "list_namespace_types")]` | grep / unit | `grep -q "list_namespace_types" Transport/Mcp/Tools/ListNamespaceTypesTool.cs` | ❌ Wave 0 (new file) |
| CLEAN-02 | `ListNamespaceTypesUseCase` class exists, `DecompileNamespaceUseCase` does not | grep | `grep -q "ListNamespaceTypesUseCase" Application/UseCases/ListNamespaceTypesUseCase.cs && ! test -f Application/UseCases/DecompileNamespaceUseCase.cs` | ❌ Wave 0 (new file) |
| CLEAN-02 | DI registration updated in both `Program.cs` and `ToolTestFixture.cs` | grep | `grep -q "ListNamespaceTypesTool" Program.cs && grep -q "ListNamespaceTypesTool" Tests/Fixtures/ToolTestFixture.cs && ! grep -q "DecompileNamespaceTool" Program.cs Tests/Fixtures/ToolTestFixture.cs` | ❌ Wave 0 (grep) |
| CLEAN-02 | Existing `ListsTypesInNamespace` test still passes with renamed tool | integration (xUnit) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.ListsTypesInNamespace"` | ❌ Wave 0 (rename + content update) |
| CLEAN-02 | Existing `OrdersByKindThenAlphabetically` test still passes | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.OrdersByKindThenAlphabetically"` | ❌ Wave 0 (rename) |
| CLEAN-02 | Existing `NestedTypesIndentedUnderParent` test still passes | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.NestedTypesIndentedUnderParent"` | ❌ Wave 0 (rename) |
| CLEAN-02 | Existing `InvalidNamespace_ThrowsNamespaceNotFound` test still passes | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.InvalidNamespace_ThrowsNamespaceNotFound"` | ❌ Wave 0 (rename) |
| CLEAN-02 | `MaxTypesLimitsOutput` test renamed to `MaxResultsLimitsOutput` and updated for `maxResults` param | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.MaxResultsLimitsOutput"` | ❌ Wave 0 (rename + body update) |
| CLEAN-02 | Pagination footer present on normal response | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FooterPresent"` | ❌ Wave 0 (new test) |
| CLEAN-02 | Pagination footer shape matches regex `\[pagination:\{"total":\d+,"returned":\d+,"offset":\d+,"truncated":(true\|false),"nextOffset":(\d+\|null)\}\]` | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FooterShapeRegex"` | ❌ Wave 0 (new test) |
| CLEAN-02 (PAGE-06) | Namespace with >100 types: page 1 (default) returns `returned=100, truncated=true, nextOffset=100` | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FirstPageTruncated"` | ❌ Wave 0 (new test + new fixture file) |
| CLEAN-02 (PAGE-06) | Namespace with >100 types: page 2 with `offset=100, maxResults=100` returns `truncated=false, nextOffset=null` | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FinalPage"` | ❌ Wave 0 (new test) |
| CLEAN-02 (PAGE-06) | Offset beyond total returns empty body with `total=N, returned=0, truncated=false, nextOffset=null` and does not throw | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_OffsetBeyondTotal"` | ❌ Wave 0 (new test) |
| CLEAN-02 (PAGE-06) | `maxResults=501` rejected with `McpToolException("INVALID_PARAMETER", ...)` | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_CeilingRejected"` | ❌ Wave 0 (new test) |
| CLEAN-02 (PAGE-06) | `maxResults=0` rejected with `McpToolException("INVALID_PARAMETER", ...)` | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_ZeroMaxResultsRejected"` | ❌ Wave 0 (new test) |
| CLEAN-02 (PAGE-06) | Zero-match namespace (`ILSpy.Mcp.TestTargets.Empty`, created as empty marker namespace) — actually delegated to invalid-namespace path since NamespaceNotFoundException is correct; see Pitfall 5 | (covered by InvalidNamespace_ThrowsNamespaceNotFound above) | — | ✅ existing |
| CLEAN-03 | README tool count updated to 27 | grep | `grep -q "27 tools" README.md && ! grep -q "28 tools" README.md` | ❌ Wave 0 (grep) |
| CLEAN-03 | README `#### \`analyze_references\`` section removed | grep | `! grep -q "analyze_references" README.md` | ❌ Wave 0 (grep) |
| CLEAN-03 | README `#### \`decompile_namespace\`` section renamed and body updated | grep | `! grep -q "decompile_namespace" README.md && grep -q "#### \`list_namespace_types\`" README.md && grep -q "maxResults" README.md` | ❌ Wave 0 (grep) |
| CLEAN-03 | README links to `docs/PAGINATION.md` | grep | `grep -q "docs/PAGINATION.md" README.md` | ❌ Wave 0 (grep) |
| CLEAN-03 | `.claude/skills/mcp-tool-design/SKILL.md` Principle 4 cross-references `docs/PAGINATION.md` | grep | `grep -A 15 "### 4. Pagination" .claude/skills/mcp-tool-design/SKILL.md \| grep -q "docs/PAGINATION.md"` | ❌ Wave 0 (grep) |
| Roadmap ripple | REQUIREMENTS.md PAGE-06 row moved from Phase 11 to Phase 9 and marked landed | grep | `grep "PAGE-06" .planning/REQUIREMENTS.md \| grep -q "Phase 9"` | ❌ Wave 0 (grep) |
| Roadmap ripple | ROADMAP.md Phase 11 `**Requirements**:` line drops PAGE-06 | grep | `! sed -n '80p' .planning/ROADMAP.md \| grep -q "PAGE-06"` | ❌ Wave 0 (grep) |
| Roadmap ripple | ROADMAP.md Phase 11 success criterion #4 (line 85) updated — no longer mentions `maxTypes=200` | grep | `! sed -n '85p' .planning/ROADMAP.md \| grep -q "maxTypes"` | ❌ Wave 0 (grep) |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~ListNamespaceTypes"` (all renamed + new tests for the target tool, ~10 tests, <10s)
- **Per wave merge:** `dotnet test ILSpy.Mcp.sln` (full suite; baseline 173 tests from Phase 8 minus 5 deleted AnalyzeReferencesToolTests tests plus ~6 new pagination tests = ~174 expected)
- **Phase gate:** Full suite green (`dotnet test ILSpy.Mcp.sln` exit code 0) before `/gsd:verify-work` closes the phase. The phase-gate command is the same as the wave-merge command — the gate is a re-run on the final merged state.

### Wave 0 Gaps

The existing `DecompileNamespaceToolTests.cs` already covers the functional shape of the renamed tool (5 tests). Phase 9 adds:

- [ ] `TestTargets/Types/PaginationTestTargets.cs` — NEW fixture file containing `namespace ILSpy.Mcp.TestTargets.Pagination;` with 105 empty `public class TypeN {}` declarations. Enables all `Pagination_*` integration tests that need a >100-type namespace. (Without this, pagination tests either use an artificial namespace that doesn't exist in TestTargets or test trivially against the ~12-type root namespace, neither of which exercises the contract.)
- [ ] `Tests/Tools/ListNamespaceTypesToolTests.cs` (renamed from `DecompileNamespaceToolTests.cs`) — existing 5 tests updated for the `maxResults` parameter name + 6 new `Pagination_*` tests:
  - `Pagination_FooterPresent` — calls with defaults against the root namespace, asserts the last non-empty line starts with `[pagination:` and ends with `]`.
  - `Pagination_FooterShapeRegex` — asserts the footer JSON matches the exact contract regex.
  - `Pagination_FirstPageTruncated` — calls with defaults against `ILSpy.Mcp.TestTargets.Pagination` (105 types), asserts `total=105, returned=100, offset=0, truncated=true, nextOffset=100`.
  - `Pagination_FinalPage` — calls with `maxResults=100, offset=100` against the same namespace, asserts `total=105, returned=5, offset=100, truncated=false, nextOffset=null`.
  - `Pagination_OffsetBeyondTotal` — calls with `offset=1000` against the same namespace, asserts empty body + footer with `total=105, returned=0, truncated=false, nextOffset=null`, and that the call does NOT throw.
  - `Pagination_CeilingRejected` — calls with `maxResults=501`, asserts `McpToolException` with `ErrorCode == "INVALID_PARAMETER"`.
  - `Pagination_ZeroMaxResultsRejected` — calls with `maxResults=0`, asserts `McpToolException` with `ErrorCode == "INVALID_PARAMETER"`.
- [ ] `Tests/Tools/AnalyzeReferencesToolTests.cs` — DELETED (5 tests gone).
- [ ] Framework install: none required. xUnit 2.9.x + FluentAssertions 8.x + Microsoft.NET.Test.Sdk are already wired in `Tests/ILSpy.Mcp.Tests.csproj` as used by all 173 existing tests.

**Note on Wave 0 sequencing:** `TestTargets/Types/PaginationTestTargets.cs` must land BEFORE the `Pagination_*` tests are added (same task or earlier task in the same plan), otherwise the tests cannot compile-test-reference the namespace. Suggested ordering within the CLEAN-02 plan: (1) create `PaginationTestTargets.cs`, (2) rename + extend the use case and tool, (3) update DI in `Program.cs` + `ToolTestFixture.cs`, (4) rename + extend `ListNamespaceTypesToolTests.cs` with the new Pagination_* tests. All four sub-tasks commit as one atomic commit to avoid a broken intermediate state.

## Sources

### Primary (HIGH confidence — verified against working tree 2026-04-09)

- `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` (94 lines, current) — file to delete
- `Transport/Mcp/Tools/DecompileNamespaceTool.cs` (65 lines, current) — file to rename/replace; `[McpServerTool(Name = "decompile_namespace")]` at line 27; `maxTypes` parameter at line 32
- `Transport/Mcp/Tools/SearchStringsTool.cs` (66 lines) — precedent for `maxResults`/`offset` parameter descriptions and order; lines 32-33 are the verbatim wording source
- `Transport/Mcp/Tools/SearchConstantsTool.cs` (61 lines) — second precedent confirming the parameter description strings
- `Application/UseCases/DecompileNamespaceUseCase.cs` (248 lines) — rename target; `maxTypes` parameter at line 50; `.Take(maxTypes)` clamp at line 101; `NamespaceNotFoundException` throw at line 73-74; `NamespaceTypeSummary.TotalTypeCount = exactMatches.Count` at line 111; `FormatOutput` at lines 182-204
- `Application/UseCases/SearchStringsUseCase.cs` (112 lines) — `FormatResults` pattern at lines 84-111; header line at 95 (the "showing X-Y" precedent)
- `Domain/Models/SearchResult.cs` (65 lines) — `SearchResults<T>` record at lines 3-19 with `Results, TotalCount, Offset, Limit` fields
- `Domain/Services/IDecompilerService.cs` (62 lines) — `ListTypesAsync` signature at lines 26-29 confirmed to have NO limit parameter (clamping is use-case-only)
- `Domain/Models/NamespaceTypeSummary.cs` (28 lines) — `TotalTypeCount` property at line 11
- `Program.cs` (227 lines) — DI registrations at lines 171 (`DecompileNamespaceUseCase`), 188 (`AnalyzeReferencesTool`), 201 (`DecompileNamespaceTool`); uses BOTH `WithToolsFromAssembly()` at line 116 AND explicit `AddScoped<>` per tool
- `Tests/Fixtures/ToolTestFixture.cs` (115 lines) — second DI list mirroring `Program.cs`; `AnalyzeReferencesTool` at line 87; `DecompileNamespaceUseCase` at line 71; `DecompileNamespaceTool` at line 100
- `Tests/Tools/DecompileNamespaceToolTests.cs` (138 lines) — 5 existing tests: `ListsTypesInNamespace` (17), `OrdersByKindThenAlphabetically` (34), `NestedTypesIndentedUnderParent` (58), `InvalidNamespace_ThrowsNamespaceNotFound` (75), `MaxTypesLimitsOutput` (90) — last one uses `maxTypes: 2` at line 98
- `Tests/Tools/AnalyzeReferencesToolTests.cs` (102 lines) — 5 tests to delete, all dependent on the class being deleted
- `Transport/Mcp/Errors/McpToolException.cs` (22 lines) — exception shape: two-arg constructor `(errorCode, message)` at line 11; `INVALID_PARAMETER` error code confirmed as current pattern
- `README.md` (1566 lines) — verified line numbers: line 58 (`**28 tools** across 8 categories:`), line 376 (`#### \`decompile_namespace\``), line 384 (`maxTypes` parameter table row), line 884 (`#### \`analyze_references\``), line 1334 (unrelated `ExportProjectTool`'s `maxTypes=500` parameter — leave alone)
- `.claude/skills/mcp-tool-design/SKILL.md` (123 lines) — Principle 4 at lines 54-68; Principle 7 mention of `analyze_references` at line 93 (leave alone as historical audit reference)
- `docs/` directory — verified contents: `banner.png` only, clean slate for `docs/PAGINATION.md`
- `.planning/REQUIREMENTS.md` — PAGE-06 bullet at line 20; traceability row at line 114
- `.planning/ROADMAP.md` — Phase 11 `**Requirements**:` at line 80; success criterion #4 mentioning `maxTypes=200` at line 85
- `.planning/phases/08-tech-debt-cleanup/08-01-PLAN.md` — DEBT-02 context (Application-layer cannot import Transport-layer `McpToolException`)
- `TestTargets/Types/*.cs` (15 files) — fixture enumeration confirming no namespace has >100 types; root `ILSpy.Mcp.TestTargets` has ~12 top-level types
- `ARCHITECTURE.md`, `MCP.md`, `appsettings.json` — verified to contain ZERO references to `decompile_namespace` or `analyze_references` (no collateral edits required)

### Secondary (MEDIUM confidence)

None. Every finding in this research was verified by direct read of the file in question.

### Tertiary (LOW confidence)

None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages and patterns are in-tree and verified
- Architecture: HIGH — file paths, class names, line numbers, and patterns all verified against the working tree on 2026-04-09
- Pitfalls: HIGH — pitfalls 1-5 are concrete findings from direct code reads, not speculation
- Validation Architecture: HIGH — test commands mirror the existing `[Fact]` + `[Collection("ToolTests")]` pattern already used by 173 passing tests; the gap list is mechanical
- Roadmap ripple edits: HIGH — REQUIREMENTS.md and ROADMAP.md exact lines and wording verified

**Research date:** 2026-04-09
**Valid until:** 2026-05-09 (30 days; source files verified and unlikely to churn before Phase 9 plan creation, but RE-VERIFY if anything lands on `main` between now and plan creation — especially `Program.cs`, `ToolTestFixture.cs`, `DecompileNamespaceUseCase.cs`, and `README.md` which are the files with the most specific line-number dependencies)
