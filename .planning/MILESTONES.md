# Milestones

## v1.2.0 Tool Polish (Shipped: 2026-04-12)

**Phases completed:** 8 phases, 29 plans
**Requirements:** 27 / 27 satisfied (iteration-3 audit PASS)

**Delivered:** Made the existing tool surface AI-effective — uniform pagination, scenario-oriented descriptions, inline IL token resolution, self-describing match records, silent-truncation visibility, and a cleaner 27-tool surface.

**Key accomplishments:**

- **Uniform pagination contract** — Defined once in `docs/PAGINATION.md` and applied across 19 unbounded tools with a canonical `[pagination:{total,returned,truncated,offset}]` footer (PAGE-01..08).
- **Scenario-oriented descriptions** — Rewrote all 27 tool descriptions from mechanical "Lists all X" to "Use this when…" agent-intent framing; added bidirectional cross-references between overlapping tools (DESC-01/02).
- **IL output richness** — Inlined metadata token resolution in `disassemble_type` / `disassemble_method` (call/callvirt/newobj/ldfld/ldstr) and added an opt-in `resolveDeep` flag for full parameter signatures and expanded generics (IL-01..03).
- **Self-describing match records** — Every `find_*` match now carries declaring type FQN, method signature, and IL offset; `find_dependencies` groups by kind; `find_implementors` marks direct vs transitive; `find_instantiations` pinpoints each `newobj` site (OUTPUT-01..04).
- **Enriched search + get** — `search_strings` emits method FQN, IL offset, and a surrounding IL window (N=3 instructions); `search_constants` emits method FQN + IL offset; `get_type_members` distinguishes inherited vs declared, exposes virtual/abstract/sealed flags, and summarises per-member attributes (OUTPUT-05/06/07).
- **Silent-truncation visibility** — `decompile_*`, `disassemble_*`, `export_project`, `analyze_assembly` all report truncation via the canonical footer instead of free-form "[Output truncated...]" strings (PAGE-07/08).
- **Structural cleanup** — Hard-deleted the `analyze_references` dispatcher (28 → 27 tools), renamed `decompile_namespace` → `list_namespace_types`, and aligned README with the final surface (CLEAN-01..03).
- **v1.0 tech debt closed** — Normalised FindDependenciesTool error code (MEMBER_NOT_FOUND), removed Application→Transport layer violation via a new domain exception, backfilled Phase 1-6 SUMMARY frontmatter, and runtime-verified Phase 7 tests (DEBT-01..04).

**Stats:**

- Timeline: 4 days (2026-04-09 → 2026-04-12)
- Phases: 8 (Phases 8-15, including two audit-driven gap-closure phases 14 and 15)
- Tests: 230 passing (from 173 at v1.0)
- Audit iterations: 3 (shipped on iteration-3 PASS)

**Tech debt carried forward (non-blocking):**

- Nyquist VALIDATION.md files missing or draft for all seven v1.2.0 phases — optional retrospective pass; VERIFICATION.md green for all phases
- Phase 14 verification template grepped for wiring only and missed a content assertion (root cause of the iteration-2 reopen); addressed in Phase 15 but the template pattern is worth fixing project-wide
- 15-01-SUMMARY.md miscounted the test suite (reports 235; actual 230) — cosmetic, no functional impact

---

## v1.0 Feature Parity (Shipped: 2026-04-08)

**Phases completed:** 7 phases, 16 plans, 8 tasks

**Delivered:** Full .NET static analysis capability for AI assistants — from 8 basic tools to 28 tools covering decompilation, IL disassembly, cross-references, assembly inspection, search, and bulk operations.

**Key accomplishments:**

- Test infrastructure with 148+ integration tests across all 28 tools
- SDK upgrades: MCP SDK 0.4→1.2.0, ICSharpCode.Decompiler 9.1→10.0 (zero regressions)
- IL disassembly tools (disassemble_type, disassemble_method) with full CIL output
- Cross-reference analysis: find_usages, find_implementors, find_dependencies, find_instantiations, analyze_references
- Assembly inspection: metadata, attributes, embedded resources, compiler-generated types (7 tools)
- Search & cross-assembly: string/constant search across IL, multi-assembly directory loading
- Bulk operations: namespace decompilation and full project export (.csproj)

**Stats:**

- Lines of code: 12,180 C#
- Files modified: 246
- Timeline: 2 days (2026-04-07 → 2026-04-08)
- Git range: feat(01-01) → docs(phase-07)

**Tech Debt (4 items):**

- Minor error code inconsistency in FindDependenciesTool (METHOD_NOT_FOUND vs MEMBER_NOT_FOUND)
- ExportProjectUseCase imports McpToolException from Transport layer (architecture violation)
- SUMMARY.md frontmatter missing requirements_completed for Phases 1-6
- Phase 7 tests not runtime-verified (code inspection only)

---
