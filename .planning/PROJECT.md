# ILSpy MCP — .NET Static Analysis for AI Assistants

## What This Is

An MCP (Model Context Protocol) server that exposes ILSpy's .NET decompilation and static analysis capabilities as 27 tools for AI assistants. Covers decompilation, IL disassembly, cross-reference tracing, assembly inspection, string/constant search, resource extraction, cross-assembly analysis, and bulk operations — full reverse engineering feature parity with ILSpy GUI, tuned so an AI agent can succeed on the first call without round-tripping.

## Core Value

AI assistants can perform complete .NET static analysis workflows — not just read code, but trace execution, find usages, search strings, and navigate across types and assemblies with self-describing, paginable, silent-truncation-free responses.

## Requirements

### Validated

#### v1.0 — Feature Parity (shipped 2026-04-08)

- ✓ Decompile individual types and methods — existing (pre-v1.0)
- ✓ List assembly types — existing (pre-v1.0)
- ✓ Analyze assembly structure — existing (pre-v1.0)
- ✓ Get type members — existing (pre-v1.0)
- ✓ Find type hierarchy — existing (pre-v1.0)
- ✓ Search members by name — existing (pre-v1.0)
- ✓ Find extension methods — existing (pre-v1.0)
- ✓ Upgrade MCP SDK from 0.4.0-preview.3 to stable 1.2.0 — v1.0 (Phase 2)
- ✓ Upgrade ICSharpCode.Decompiler from 9.1 to 10.0 — v1.0 (Phase 2)
- ✓ Fix MaxConcurrentOperations semaphore enforcement — v1.0 (Phase 2)
- ✓ Fix CancellationTokenSource leak in TimeoutService — v1.0 (Phase 2)
- ✓ Include constructors (.ctor/.cctor) in get_type_members and decompile_method — v1.0 (Phase 2)
- ✓ IL/CIL output for types and methods — v1.0 (Phase 3)
- ✓ Cross-reference analysis: find_usages, find_implementors, find_instantiations, find_dependencies — v1.0 (Phase 4)
- ✓ Assembly metadata (target framework, PE bitness, strong name, entry point) — v1.0 (Phase 5)
- ✓ Assembly-level and type-level custom attribute inspection — v1.0 (Phase 5)
- ✓ List and extract embedded resources — v1.0 (Phase 5)
- ✓ List nested types and find compiler-generated types — v1.0 (Phase 5)
- ✓ List assembly references (name, version, culture, public key token) — v1.0 (Phase 5)
- ✓ String search across assembly IL (ldstr operands) — v1.0 (Phase 6)
- ✓ Constant/enum search via ldc.*/FieldDefinition.HasDefault — v1.0 (Phase 6)
- ✓ Resolve type across assemblies in a directory — v1.0 (Phase 6)
- ✓ Load all assemblies from a folder for cross-assembly analysis — v1.0 (Phase 6)
- ✓ Bulk decompilation: list_namespace_types, export_project — v1.0 (Phase 7)
- ✓ Critical-path test coverage (148+ integration tests) — v1.0 (Phases 1-7)
- ✓ README.md with all tools documented — v1.0 (Phase 7)

#### v1.2.0 — Tool Polish (shipped 2026-04-12)

- ✓ Uniform pagination contract — `(maxResults, offset)` params + canonical `[pagination:{total,returned,truncated,offset}]` footer across 19 unbounded tools (PAGE-01..08, v1.2.0 Phases 9/14)
- ✓ Scenario-oriented tool descriptions — all 27 tools rewritten to "Use this when…" format with bidirectional cross-references between overlapping tools (DESC-01/02, v1.2.0 Phase 13)
- ✓ Inline IL metadata token resolution in `disassemble_type` / `disassemble_method` with opt-in `resolveDeep` for full parameter signatures and expanded generics (IL-01..03, v1.2.0 Phases 12/14)
- ✓ Self-describing find-tool match records — declaring type FQN, method signature, IL offset, kind grouping, direct-vs-transitive markers (OUTPUT-01..04, v1.2.0 Phase 10)
- ✓ Enriched `get_type_members` — inherited vs declared distinction, virtual/abstract/sealed flags, per-member attribute summary (OUTPUT-05, v1.2.0 Phase 14)
- ✓ Enriched `search_strings` / `search_constants` — method FQN + IL offset; `search_strings` additionally emits a surrounding IL window (N=3) per match (OUTPUT-06/07, v1.2.0 Phases 12/14/15)
- ✓ Structural cleanup — `analyze_references` dispatcher removed; `decompile_namespace` renamed to `list_namespace_types`; README aligned with 27-tool runtime surface (CLEAN-01..03, v1.2.0 Phases 9/14/15)
- ✓ v1.0 tech debt closed — FindDependenciesTool error-code normalization, Application→Transport layer violation fix, Phase 1-6 SUMMARY frontmatter backfill, Phase 7 test runtime verification (DEBT-01..04, v1.2.0 Phase 8)

### Active

No active milestone. Run `/gsd-new-milestone` to scope the next cycle.

Candidate themes for the next milestone (not committed):

- Multi-assembly session management (SESSION-01, deferred from v1.0)
- New tools beyond the 27-tool surface (MORE-01)
- Performance / caching (only if observed data supports it)
- Nyquist VALIDATION.md backfill for v1.2.0 Phases 8/10/11/12/13/14/15 (optional retrospective pass)

### Out of Scope

- Dynamic/runtime analysis (debugging, memory inspection, hooking) — ILSpy is static analysis only
- VB.NET output — low value for reverse engineering workflows
- Mobile/desktop GUI — this is an MCP server for AI assistants
- Assembly editing/patching — not aligned with read-only static analysis scope
- PDB/source matching — source-level debugging is a separate concern
- Cross-request caching — premature optimization, defer until performance data exists
- xUnit v3 migration — not worth churn during feature milestones, stay on v2.9.x

## Context

Shipped v1.0 (2026-04-08) and v1.2.0 (2026-04-12). Runtime surface: 27 MCP tools across decompilation, IL disassembly, type analysis, cross-references, assembly inspection, search, cross-assembly, and bulk operations. 230 tests passing.

Tech stack: .NET 10, ICSharpCode.Decompiler 10.0, ModelContextProtocol 1.2.0, xUnit 2.9.x, FluentAssertions 8.9.
Architecture: Domain → Infrastructure → Application → Transport (layered, DI-wired).
IL scanning via System.Reflection.Metadata BlobReader for cross-refs, string search, constant search.
Pagination contract documented in `docs/PAGINATION.md`; every unbounded tool emits the canonical footer.

Known tech debt carried forward from v1.2.0 (all non-blocking):

- Nyquist VALIDATION.md files missing or draft for Phases 8, 10, 11, 12, 13, 14, 15 — optional retrospective pass, VERIFICATION.md is green for all phases
- Phase 14 verification template grepped for `AppendFooter` only and missed the OUTPUT-06 "surrounding IL window" content assertion (fixed in Phase 15); future verification templates for output-shape requirements should include a positive content assertion, not just a wiring grep
- 15-01-SUMMARY.md reports 235 tests; actual suite is 230 passing — summary miscount, no functional impact

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Upgrade SDKs before new features | Clean foundation, avoid rework on deprecated APIs | ✓ Good — zero regressions |
| Fix bugs before new features | Stable baseline for tests to validate against | ✓ Good — baseline caught issues |
| P0-P2 this milestone, P3 deferred | Session management is architectural change, separable | ✓ Good — clean scope boundary |
| Critical-path tests (not exhaustive) | P0 + bug fixes get thorough tests, lighter elsewhere | ✓ Good — 230 tests, all green |
| Separate IDisassemblyService from IDecompilerService | Disassembly is a distinct concern with different output format | ✓ Good — clean separation |
| Duplicated IL helpers in Search vs CrossRef services | Avoid coupling between services for 2 scan patterns | ⚠️ Revisit — may extract shared helper if a 3rd scanner appears |
| WholeProjectDecompiler used directly (not via IDecompilerService) | Project export is a different workflow from type/method decompilation | ✓ Good — appropriate separation |
| net10.0 target framework | Only .NET 10 runtime available in dev environment | ✓ Good — CI compatibility verified |
| Pagination contract defined once, applied across phases | Avoids per-tool re-litigation of the contract | ✓ Good — 19 tools emit uniform footer, docs/PAGINATION.md is single source of truth |
| Hard-delete `analyze_references` dispatcher with no deprecation alias | Dispatchers hide the option set; shims would re-advertise the overlap | ✓ Good — clean 27-tool surface |
| Apply `list_namespace_types` pagination during the rename (Phase 9), not later in Phase 11 | Avoids two edits to the same tool across two phases | ✓ Good — single touch, roadmap ripple pattern established |
| `resolveDeep` as opt-in boolean flag rather than separate tool | Agent pays deeper-resolution cost only when asked | ✓ Good — default output stays compact |
| `search_strings` surrounding IL window via two-phase scan with N=3 | Avoid reopening the IL reader per match; window size matches typical agent context needs | ✓ Good — closed OUTPUT-06 iteration-2 gap |
| Retroactive gap-closure phases (14, 15) accepted over reopening earlier phases | Cleaner git history, clear ship timeline, audit-driven scope | ✓ Good — milestone shipped on iteration-3 audit pass |
| Scenario-oriented descriptions applied last (Phase 13) | Prevents rewriting descriptions twice as tool shapes change | ✓ Good — 27 tools rewritten once, no churn |

## Constraints

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK — no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes
- **Compatibility**: The 27-tool surface is the supported contract; additions are fine, renames and shape changes require a milestone

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-12 after v1.2.0 milestone*
