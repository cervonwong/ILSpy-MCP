# Requirements: ILSpy MCP — v1.2.0 Tool Polish

**Defined:** 2026-04-09
**Core Value:** AI assistants can perform complete .NET static analysis workflows — trace execution, find usages, search strings, and navigate across types and assemblies.
**Milestone Goal:** Make the 28 existing tools AI-effective. Close the lazy-agent gaps identified in the 260410 audit so agents succeed on first call without round-tripping.

## v1.2 Requirements

Requirements for v1.2.0 Tool Polish. Each maps to roadmap phases.

### Pagination

Uniform pagination contract across unbounded tools. Agents get sensible defaults (no forced pagination params), but `truncated`/`total` metadata is always returned so agents know when to page.

- [x] **PAGE-01**: Uniform pagination contract defined and documented — every list-returning tool accepts `(maxResults, offset)` with sensible defaults, returns `(truncated: bool, total: int)`; documented as a reusable pattern so agents don't need to specify pagination unless they intend to page
- [ ] **PAGE-02**: All `find_*` tools implement PAGE-01 contract (`find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations`, `find_extension_methods`, `find_compiler_generated_types`)
- [ ] **PAGE-03**: All `list_*` tools implement PAGE-01 contract (`list_assembly_types`, `list_embedded_resources`)
- [ ] **PAGE-04**: `get_type_members` implements PAGE-01 contract
- [ ] **PAGE-05**: `search_members_by_name` implements PAGE-01 contract
- [x] **PAGE-06**: `list_namespace_types` implements PAGE-01 contract (replaces the previous `maxTypes=200` hard cap) — landed in Phase 9 alongside CLEAN-02
- [ ] **PAGE-07**: Source-returning tools (`decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method`) report `(truncated, total_lines)` when output exceeds line cap — makes silent truncation visible
- [ ] **PAGE-08**: Bounded-output tools (`export_project`, `analyze_assembly`) report `truncated`/`total` metadata so silent truncation becomes visible

### Descriptions

Scenario-oriented, agent-intent framing. Every description answers "when is the agent reaching for this?" instead of "what does the tool produce mechanically?"

- [ ] **DESC-01**: Every mechanical tool description rewritten to scenario-oriented "Use this when..." format — starts with the 6 worst offenders (`find_usages`, `disassemble_method`, `search_strings`, `find_compiler_generated_types`, `search_constants`, `find_dependencies`) and extends to all 21 mechanical descriptions identified in the audit
- [ ] **DESC-02**: Overlapping tools cross-reference each other with cost/use-case guidance in their descriptions (`decompile_type` ↔ `get_type_members`; `list_assembly_types` ↔ `list_namespace_types`)

### IL Output Richness

Inline metadata token resolution in IL disassembly — the single biggest round-trip waste in the project per the audit.

- [ ] **IL-01**: `disassemble_method` resolves metadata token references inline (`call`/`callvirt`/`newobj`/`ldfld`/`ldstr`) so the agent sees fully-qualified names and defining assembly, not raw token IDs
- [ ] **IL-02**: `disassemble_type` resolves metadata token references inline
- [ ] **IL-03**: IL disassembly tools expose an opt-in `resolveDeep` flag for deeper resolution (full type signatures for parameters, generics expanded)

### Output Richness (Find + Search + Get)

Self-describing match records so the agent doesn't need follow-up calls to understand where a match lives or what it means.

- [ ] **OUTPUT-01**: `find_usages` matches include declaring type FQN, containing method signature, IL offset — each match is self-describing
- [ ] **OUTPUT-02**: `find_dependencies` matches grouped by kind (calls, field reads, type refs) with fully-qualified names and defining assembly
- [ ] **OUTPUT-03**: `find_implementors` matches include full type name, assembly, direct-vs-transitive relationship marker
- [ ] **OUTPUT-04**: `find_instantiations` matches include containing type FQN, containing method signature, IL offset
- [ ] **OUTPUT-05**: `get_type_members` distinguishes inherited vs declared, exposes virtual/abstract/sealed flags, includes attribute summary per member
- [ ] **OUTPUT-06**: `search_strings` matches include literal value, containing method FQN, IL offset, and a window of surrounding IL instructions
- [ ] **OUTPUT-07**: `search_constants` matches include constant value, containing method FQN, IL offset

### Structural Cleanup

- [x] **CLEAN-01**: `analyze_references` dispatcher tool removed; four `find_*` tools become the sole cross-reference entry points (tool count: 28 → 27)
- [x] **CLEAN-02**: `decompile_namespace` renamed to `list_namespace_types` — surface matches actual behavior (enumerates types with signatures; it is not a decompile operation)
- [x] **CLEAN-03**: README.md and any cross-references updated to match the new surface (27 tools, renamed namespace tool, no dispatcher)

### v1.0 Tech Debt

- [x] **DEBT-01**: `FindDependenciesTool` error codes normalized (resolve `METHOD_NOT_FOUND` vs `MEMBER_NOT_FOUND` inconsistency)
- [x] **DEBT-02**: `ExportProjectUseCase` no longer imports `McpToolException` from Transport layer (fix layered-architecture violation — Application layer must not depend on Transport)
- [x] **DEBT-03**: v1.0 Phase 1-6 SUMMARY.md frontmatter gaps filled (add missing `requirements_completed` fields)
- [x] **DEBT-04**: Phase 7 tool tests are runtime-verified (not just code inspection)

## v2 Requirements

Deferred to a future milestone. Tracked but not in v1.2.0 roadmap.

### Session Management

- **SESSION-01**: Multi-assembly session management — load an assembly set once, reuse across tool calls (P3 from v1.0, architectural change)

### Additional Tooling

- **MORE-01**: Any new tools beyond the current 27-after-cleanup surface

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| New tools / new capabilities | This milestone is polish of the existing 28, not expansion |
| xUnit v3 migration | Not worth churn during a polish milestone, defer separately |
| New runtime dependencies | Project constraint — ICSharpCode.Decompiler + S.R.Metadata + MCP SDK only |
| Dynamic/runtime analysis | ILSpy is static analysis only |
| VB.NET output | Low value for reverse engineering workflows |
| Assembly editing/patching | Not aligned with read-only static analysis scope |
| PDB/source matching | Source-level debugging is a separate concern |
| Cross-request caching | Premature optimization; defer until performance data exists |
| Multi-assembly session management | Architectural change, deferred to next milestone (v2) |

## Traceability

Which phases cover which requirements.

| Requirement | Phase | Status |
|-------------|-------|--------|
| DEBT-01 | Phase 8 | Complete |
| DEBT-02 | Phase 8 | Complete |
| DEBT-03 | Phase 8 | Complete |
| DEBT-04 | Phase 8 | Complete |
| PAGE-01 | Phase 9 | Complete |
| CLEAN-01 | Phase 9 | Complete |
| CLEAN-02 | Phase 9 | Complete |
| CLEAN-03 | Phase 9 | Complete |
| PAGE-02 | Phase 10 | Pending |
| OUTPUT-01 | Phase 10 | Pending |
| OUTPUT-02 | Phase 10 | Pending |
| OUTPUT-03 | Phase 10 | Pending |
| OUTPUT-04 | Phase 10 | Pending |
| PAGE-03 | Phase 11 | Pending |
| PAGE-04 | Phase 11 | Pending |
| PAGE-05 | Phase 11 | Pending |
| PAGE-06 | Phase 9 | Complete |
| OUTPUT-05 | Phase 11 | Pending |
| IL-01 | Phase 12 | Pending |
| IL-02 | Phase 12 | Pending |
| IL-03 | Phase 12 | Pending |
| OUTPUT-06 | Phase 12 | Pending |
| OUTPUT-07 | Phase 12 | Pending |
| PAGE-07 | Phase 12 | Pending |
| PAGE-08 | Phase 12 | Pending |
| DESC-01 | Phase 13 | Pending |
| DESC-02 | Phase 13 | Pending |

**Coverage:**
- v1.2 requirements: 27 total
- Mapped to phases: 27
- Unmapped: 0 ✓

---
*Requirements defined: 2026-04-09*
*Last updated: 2026-04-09 after roadmap creation (Phases 8-13 mapped)*
