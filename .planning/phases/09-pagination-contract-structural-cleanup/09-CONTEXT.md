# Phase 9: Pagination Contract & Structural Cleanup - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Lock the final v1.2 tool surface and define exactly one pagination contract that every subsequent phase will apply. Four concrete deliverables:

1. **PAGE-01** — Define the pagination contract (parameters, defaults, ceiling, response envelope, edge cases) and document it in one place that Phases 10-12 can cite.
2. **CLEAN-01** — Hard-delete `AnalyzeReferencesTool` (the dispatcher) and its tests. The four `find_*` tools become the sole cross-reference entry points. Tool count: 28 → 27.
3. **CLEAN-02** — Hard-rename `decompile_namespace` → `list_namespace_types` (class, file, tool attribute, DI, tests). **Scope expansion decided in discussion:** the renamed tool also becomes the **first implementation of the pagination contract**, making it the canonical reference for Phases 10-12.
4. **CLEAN-03** — Update `README.md` to reflect the 27-tool surface, the rename, no stale `analyze_references` references, and link to the new pagination contract doc.

**Not in scope:**
- Applying the pagination contract to any tool other than `list_namespace_types` — that is Phases 10-12's job (PAGE-02..08).
- Rewriting mechanical tool descriptions — that is Phase 13's job (DESC-01, DESC-02).
- Enriching `find_*` / `list_*` match records with declaring type / IL offset / etc. — those are OUTPUT-01..07 in Phases 10-12.
- Any new tools or new capabilities.

**Roadmap ripple:** Phase 11's PAGE-06 (apply pagination to `list_namespace_types`) becomes redundant because Phase 9 now delivers it. Planner should note this and propose a REQUIREMENTS.md / ROADMAP.md update that moves PAGE-06 to "Completed in Phase 9" or removes it from Phase 11's plan scope.

</domain>

<decisions>
## Implementation Decisions

### Pagination contract — response envelope

**Format:** every paginable tool returns a `Task<string>` with three sections in order:

```
[tool-specific natural-language header]

[results body]

[pagination:{"total":N,"returned":N,"offset":N,"truncated":bool,"nextOffset":N|null}]
```

**Leading header** — tool-specific prose, not a canonical template. Precedent is `SearchStringsUseCase.FormatResults` line 95: `"String search for 'X': 523 total matches (showing 1-100)"`. Each tool writes its header in its own voice; the header is for human/LLM readability, not the parseable contract.

**Results body** — the existing output format for each tool. No change to body shape in Phase 9 (enrichment is Phases 10-12).

**Trailing footer — the parseable contract:**
- Single line
- Wrapped in `[pagination:...]`
- Single-line minified JSON object
- Fields (in this order): `total` (int), `returned` (int), `offset` (int), `truncated` (bool), `nextOffset` (int or `null`)
- **Always present** on every response from a paginable tool — including zero-match responses and final-page responses.
- `nextOffset` is a valid next offset when `truncated=true`, and **explicit `null`** when `truncated=false`. Never omitted.

**Worked examples** (these go into the doc):

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

### Pagination contract — parameters

- **`maxResults`** (int, default `100`) — number of items per page
- **`offset`** (int, default `0`) — number of items to skip
- **Hard ceiling: `maxResults <= 500`.** A request with `maxResults > 500` is rejected with `McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.")`. Reject, do not silently clamp — strict contract, agents learn the ceiling immediately, next-step hint points at `offset`.
- **Default is 100 uniformly** across every paginated tool in the milestone. Matches existing `search_strings` / `search_constants` precedent and lets agents memorize one number.
- **offset >= total** → empty results body, `total` populated, `truncated=false`, `nextOffset=null`. Tool does not error.
- **maxResults <= 0** → reject with `McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.")`. Same rejection class as the ceiling violation.

### CLEAN-01 — `analyze_references` hard delete

- **Delete:** `Transport/Mcp/Tools/AnalyzeReferencesTool.cs`, any `Tests/Tools/AnalyzeReferencesToolTests.cs` that exists, and any DI registration for the class.
- **Keep:** `FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase` — the individual `find_*` tools already consume these directly; removing the dispatcher does not affect them.
- **No deprecation alias.** The MCP server's only consumers are AI agents, who should see the clean 27-tool surface immediately. Deprecation stubs would advertise overlap that the audit explicitly flagged as an antipattern.
- **Verification:** `grep -r "analyze_references\|AnalyzeReferencesTool" Transport/ Application/ Tests/` returns zero hits after the phase.

### CLEAN-02 — `decompile_namespace` → `list_namespace_types`

**Rename targets (hard rename, no alias):**
- `Transport/Mcp/Tools/DecompileNamespaceTool.cs` → `Transport/Mcp/Tools/ListNamespaceTypesTool.cs`
- Class: `DecompileNamespaceTool` → `ListNamespaceTypesTool`
- `[McpServerTool(Name = "decompile_namespace")]` → `[McpServerTool(Name = "list_namespace_types")]`
- `Application/UseCases/DecompileNamespaceUseCase.cs` → `Application/UseCases/ListNamespaceTypesUseCase.cs`
- Class: `DecompileNamespaceUseCase` → `ListNamespaceTypesUseCase`
- All DI registrations in `Program.cs`
- All test files (`DecompileNamespaceToolTests.cs`, any `DecompileNamespaceUseCaseTests.cs`) renamed and internals updated
- Error message strings that reference the old tool name

**Scope expansion — apply pagination contract during rename** (decided in discussion, diverges from naive roadmap reading):
- Replace the current `maxTypes = 200` parameter with the full contract: `maxResults = 100, offset = 0`.
- Parameter rename `maxTypes` → `maxResults` is part of the hard rename.
- Enforce the 500 ceiling as above.
- Emit the trailing JSON footer always.
- This tool becomes the **canonical reference implementation** of the contract. The planner should structure the plan so the contract doc (`docs/PAGINATION.md`) and this tool land in the same wave so reviewers can verify the doc against running code.

**Description rewrite is out of scope** — the current description ("Lists all types in a namespace...") is actually already list-shaped and only mildly mechanical. Phase 13 (DESC-01) owns the full description sweep. Updating the description here would mean rewriting it twice.

**Test updates:**
- Existing `DecompileNamespaceToolTests` assertions on `maxTypes` parameter must be updated to `maxResults`.
- Add at least one new test exercising the pagination contract end-to-end against a namespace with >100 types (TestTargets may need an expanded fixture if none exists) — covers: footer presence, footer shape, `truncated` transitions, `nextOffset=null` on final page, ceiling rejection.

### CLEAN-03 — README.md update

- **Tool count:** 28 → 27 (wherever the number appears).
- **Delete:** the `#### \`analyze_references\`` section (README.md line 884).
- **Rename:** the `#### \`decompile_namespace\`` section (README.md line 376) becomes `#### \`list_namespace_types\``; update the body to reflect the new parameter list (`maxResults`, `offset` replacing `maxTypes`).
- **Cross-link:** add a one-paragraph "Pagination" note near the top of the tool catalogue linking to `docs/PAGINATION.md`. Do not duplicate the contract in README — link only.
- **Do not rewrite other tool descriptions in README.** Phase 13 owns that sweep.
- **Verification:** `grep -n "analyze_references\|decompile_namespace\|28 tools" README.md` returns zero hits after the phase.

### PAGE-01 — documentation target

- **New file:** `docs/PAGINATION.md`. The `docs/` directory exists but is currently empty (only `banner.png`), so this is a clean slate.
- **Contents:** the parameters, defaults, ceiling, rejection behavior, response envelope (with worked examples verbatim from the "worked examples" block above), and a one-paragraph scope note saying which tools are expected to implement it (Phase 9 = `list_namespace_types`, Phases 10-12 = all others).
- **Cross-reference:** update `.claude/skills/mcp-tool-design/SKILL.md` Principle 4 to point at `docs/PAGINATION.md` for the canonical format, so the skill and the contract never drift.
- **Do not** put the contract in `ARCHITECTURE.md` or `MCP.md` — those cover layering and MCP transport; pagination is its own concern and deserves a dedicated file that Phases 10-12 can cite atomically.

### Claude's Discretion

- Exact wording of the PAGINATION.md prose (the decisions above lock the shape; the prose is flexible).
- Exact wording of the rejection error message beyond the "maxResults cannot exceed 500. Use offset to paginate." guidance.
- Whether to bundle CLEAN-01 (dispatcher delete) and CLEAN-02 (rename + contract) into a single plan or split them. They touch different files and can run in parallel, but both must land before CLEAN-03 (README update). Small plan bias: split if it keeps each plan under ~6 files changed; combine if the rename is small enough to fit in one atomic commit with the delete.
- Whether to rename the existing `SearchResults<T>` record in `Domain/Models/SearchResult.cs:6` to something more generic like `PagedResult<T>` now that a second domain (namespace listing) uses it, or leave it as-is and reuse the same type. Either is fine; document the choice in the plan.
- Whether to add a `returned` property to the existing `SearchResults<T>` record (it currently has `Results`, `TotalCount`, `Offset`, `Limit` — missing `Returned = Results.Count` would be a trivial convenience but not strictly required since the footer serializer can compute it).
- Which Phase 11 artifact (REQUIREMENTS.md PAGE-06 row, ROADMAP.md Phase 11 Plans line) to update to reflect that `list_namespace_types` pagination is landed in Phase 9. Propose the update in the plan; execute it atomically with the rename.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Domain/Models/SearchResult.cs:6` — `SearchResults<T>` record.** Already carries `Results`, `TotalCount`, `Offset`, `Limit`. Reuse directly for the renamed `list_namespace_types` instead of introducing a parallel type. If desired, add a convenience `Returned` property or rename to `PagedResult<T>` — Claude's discretion.
- **`Application/UseCases/SearchStringsUseCase.cs:84-111` — `FormatResults` method.** Canonical example of the "tool-specific prose header + body" pattern. The Phase 9 work is to extend this pattern with the trailing JSON footer. Can be refactored into a shared helper (`PaginationEnvelope.Append(sb, results)` or similar) to avoid copy-paste when Phases 10-12 apply the contract.
- **`Transport/Mcp/Tools/SearchStringsTool.cs` and `SearchConstantsTool.cs`** — the two tools that already have `maxResults` + `offset` parameters. They are the precedent for the parameter names, defaults, and description strings. Use verbatim phrasings: `[Description("Maximum number of results to return (default: 100)")]` and `[Description("Number of results to skip for pagination (default: 0)")]`.
- **`Domain/Errors/` — `DomainException` base + narrow subclasses.** The rejection path for `maxResults > 500` does NOT need a new domain exception — `McpToolException("INVALID_PARAMETER", ...)` at the Transport boundary is sufficient. A domain exception would be overkill for a Transport-layer input validation concern.
- **`ErrorSanitizer.SanitizePath(...)`** — used by every Transport tool in error mapping. Not directly relevant to Phase 9 but the rename plan must preserve the existing error handling pipeline when renaming `DecompileNamespaceTool`.

### Established Patterns
- **MCP tools return `Task<string>`.** The entire tool surface produces plain-text MCP content. Phase 9 does NOT change this — the pagination contract is a text-format contract, not a return-type change.
- **Tool / UseCase / Domain separation.** `DecompileNamespaceTool` is a thin Transport handler; real work lives in `DecompileNamespaceUseCase` which delegates to `INamespaceService` (Domain). The rename must cascade through all three layers atomically.
- **DI wiring via `Program.cs`.** MCP SDK 1.2.0 uses `WithToolsFromAssembly()` which auto-registers tool classes decorated with `[McpServerToolType]`. Dropping `AnalyzeReferencesTool` removes it from the registration set automatically, but the use case services registered for it (all four `Find*UseCase`) are still consumed by the individual `find_*` tools — leave those alone.
- **Test style:** xUnit 2.9.x + FluentAssertions, integration-style against `TestTargets` assembly. New pagination tests should follow this style — no mocking framework.
- **SUMMARY.md frontmatter is kebab-case** (`requirements-completed:`, `key-decisions:`, etc.) — Phase 9's SUMMARY files must match.
- **Plans commit atomically per task** — Phase 8's 08-01 landed CLEAN-01-ish work in a single atomic commit; follow that precedent.

### Integration Points
- **`docs/PAGINATION.md`** — new file, clean slate.
- **`Transport/Mcp/Tools/AnalyzeReferencesTool.cs`** — DELETE.
- **`Tests/Tools/AnalyzeReferencesToolTests.cs`** (if exists) — DELETE.
- **`Transport/Mcp/Tools/DecompileNamespaceTool.cs`** → RENAME to `ListNamespaceTypesTool.cs`, update attribute, update parameters, update error mapping.
- **`Application/UseCases/DecompileNamespaceUseCase.cs`** → RENAME to `ListNamespaceTypesUseCase.cs`, update signature (`maxTypes` → `maxResults` + `offset`), wrap result in `SearchResults<T>`, format with pagination envelope helper.
- **`Domain/Services/INamespaceService.cs`** (or equivalent) — may need a signature change if the current `DecompileNamespace` domain call hard-bakes the `maxTypes` limit. Verify during research.
- **`Domain/Models/SearchResult.cs`** — possibly extend with a `Returned` property; possibly rename `SearchResults<T>` → `PagedResult<T>` for broader applicability.
- **`Tests/Tools/DecompileNamespaceToolTests.cs`** → RENAME + update for new parameter names + add pagination test cases.
- **`Program.cs`** — DI registrations for the renamed tool/usecase; verify `AnalyzeReferencesTool` registration was removed (if it was explicit) or confirm `WithToolsFromAssembly()` picks up the deletion automatically.
- **`README.md`** — lines ~376 (`decompile_namespace` section), ~884 (`analyze_references` section), tool count references.
- **`.claude/skills/mcp-tool-design/SKILL.md`** — Principle 4, add cross-reference to `docs/PAGINATION.md`.
- **`.planning/REQUIREMENTS.md`** — mark PAGE-06 as "landed in Phase 9" or move it, and update the traceability table.
- **`.planning/ROADMAP.md`** — Phase 11 Plans line referring to PAGE-06.

### What NOT to touch
- **`find_*` tool files** — their pagination application is Phase 10 (PAGE-02 + OUTPUT-01..04). Do not add pagination parameters to them in Phase 9 even if the plan touches adjacent code.
- **`list_assembly_types`, `list_embedded_resources`, `get_type_members`, `search_members_by_name`** — all are Phase 11's PAGE-03/04/05. Hands off.
- **`disassemble_*` / `decompile_*` / `analyze_assembly` / `export_project`** — Phase 12's PAGE-07/08. Hands off.
- **Tool descriptions** — Phase 13's DESC-01/02. Do not rewrite any descriptions in Phase 9 beyond the literal rename of `decompile_namespace` → `list_namespace_types` in the `[McpServerTool(Name = ...)]` attribute.
- **`SearchStringsTool` / `SearchConstantsTool`** — already have `maxResults`/`offset`; they only need the trailing footer envelope added if Phase 9 decides to retrofit them as part of the contract proof-out. **Recommendation: do NOT retrofit in Phase 9** — Phase 12 (which owns search enrichment) is the natural home for bringing them into the footer contract. Phase 9 only has to prove the contract works on ONE tool (`list_namespace_types`); retrofitting others expands scope for no goal-backward gain.
- **`FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase`** — stay, the individual tools still consume them.
- **`MethodNotFoundException`, `TypeNotFoundException`, `NamespaceNotFoundException`** — no changes.

</code_context>

<specifics>
## Specific Ideas

- **The 260410 audit** (`.planning/quick/260410-audit-mcp-tools-for-ai-effectiveness-and/260410-AUDIT.md`) is the authoritative source for the "19 unbounded tools" list and the Principle-4 hard rule. Section 5 (Pagination) and Section 2 (Scoping — `analyze_references`) are the most relevant sub-sections for Phase 9.
- **The mcp-tool-design skill** (`.claude/skills/mcp-tool-design/SKILL.md`) Principle 4 already says `maxResults + offset` + `truncated + total`. Phase 9's PAGE-01 doc must be consistent with the skill, OR the skill must be updated to match. Treat them as one specification in two places; the `docs/PAGINATION.md` is the source of truth and the skill cross-references it.
- **Precedent velocity:** Phase 8 plans landed in 2-3 minutes each with 3-6 files touched. Phase 9 should stay in that envelope: the rename is ~6-10 files, the dispatcher delete is ~3 files, the PAGINATION.md doc is 1 file, README.md is 1 file. A three-plan split (cleanup, contract+rename, docs) or two-plan split (cleanup+contract+rename wave1, docs+README wave2) both fit.
- **The discussion produced one scope expansion vs the roadmap:** Phase 9 now delivers PAGE-06 (pagination on `list_namespace_types`), which was originally scoped to Phase 11. The planner must explicitly surface this scope shift in the plan's deviation section and propose the REQUIREMENTS.md / ROADMAP.md adjustments as atomic edits in the same plan.

</specifics>

<deferred>
## Deferred Ideas

- **Retrofit `search_strings` / `search_constants` with the trailing JSON footer.** These tools already have `maxResults` / `offset` but emit only the natural-language header today. Bringing them into the footer contract is natural but expands Phase 9 scope. Defer to Phase 12 (which owns OUTPUT-06/07 on the same tools) so the footer and the richer match record land together.
- **Rename `SearchResults<T>` → `PagedResult<T>`.** Broader applicability as multiple domains adopt the contract. Not required for Phase 9 to close its goal; decide in the plan.
- **`PaginationEnvelope` helper extraction.** A shared `PaginationEnvelope.Append(sb, results)` or similar to avoid copy-paste when Phases 10-12 apply the contract to 18+ tools. Phase 9 only applies it to one tool; extract the helper when the second tool in Phase 10 shows the copy-paste pain.
- **Description rewrites.** The audit identified 21 mechanical descriptions including `list_namespace_types`'s current wording. Phase 13 owns the sweep; Phase 9 leaves descriptions alone except where the tool name literally changes.
- **Automated tool count assertion (e.g., a test that fails if `tools/list` count != 27).** Nice-to-have regression guard but not required by Phase 9's success criteria. Add as a new tech-debt item if the milestone cycle wants it.
- **Expand `TestTargets` fixture** if no existing fixture has a namespace with >100 types to exercise the pagination end-to-end test. Scope this in the plan if needed; defer if an existing fixture works.

</deferred>

---

*Phase: 09-pagination-contract-structural-cleanup*
*Context gathered: 2026-04-09*
