# Roadmap: ILSpy MCP

## Milestones

- [x] **v1.0 Feature Parity** - Phases 1-7 (shipped 2026-04-08)
- [ ] **v1.2.0 Tool Polish** - Phases 8-13 (started 2026-04-09)

## Phases

<details>
<summary>v1.0 Feature Parity (Phases 1-7) - SHIPPED 2026-04-08</summary>

- [x] Phase 1: Test Infrastructure & Baseline (2/2 plans) - completed 2026-04-07
- [x] Phase 2: SDK Upgrades & Bug Fixes (3/3 plans) - completed 2026-04-07
- [x] Phase 3: IL Infrastructure & Disassembly (2/2 plans) - completed 2026-04-07
- [x] Phase 4: Cross-Reference Analysis (2/2 plans) - completed 2026-04-07
- [x] Phase 5: Assembly Inspection (2/2 plans) - completed 2026-04-08
- [x] Phase 6: Search & Cross-Assembly (2/2 plans) - completed 2026-04-08
- [x] Phase 7: Bulk Operations & Documentation (3/3 plans) - completed 2026-04-08

Full details: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)

</details>

### v1.2.0 Tool Polish (Phases 8-13)

- [ ] **Phase 8: Tech Debt Cleanup** - Normalize error codes, fix architecture violation, backfill SUMMARY frontmatter, runtime-verify Phase 7 tests
- [ ] **Phase 9: Pagination Contract & Structural Cleanup** - Define uniform pagination contract once, drop `analyze_references` dispatcher, rename `decompile_namespace` to `list_namespace_types`, update README
- [x] **Phase 10: Find-Tool Pagination & Match Enrichment** - Apply pagination contract to all `find_*` tools and make match records self-describing (declaring type FQN, method signature, IL offset, kind grouping) (completed 2026-04-10)
- [ ] **Phase 11: List/Get/Search Pagination & Member Enrichment** - Apply pagination contract to `list_*`, `get_type_members`, `search_members_by_name`, and `list_namespace_types`; enrich `get_type_members` with inherited/declared distinction and modifier flags
- [ ] **Phase 12: IL Token Resolution, Search Enrichment & Truncation Reporting** - Inline-resolve metadata tokens in IL disassembly, enrich search_strings/search_constants with context, report truncation on source-returning and bounded-output tools
- [ ] **Phase 13: Scenario Description Sweep** - Rewrite all mechanical tool descriptions to "Use this when..." format and cross-reference overlapping tools

## Phase Details

### Phase 8: Tech Debt Cleanup
**Milestone**: v1.2.0
**Goal**: A clean v1.0 baseline to build polish work on top of, with no architecture violations, consistent error codes, and verified tests
**Depends on**: Nothing (first phase of v1.2, builds on v1.0)
**Requirements**: DEBT-01, DEBT-02, DEBT-03, DEBT-04
**Success Criteria** (what must be TRUE):
  1. `FindDependenciesTool` returns a single consistent error code (either `METHOD_NOT_FOUND` or `MEMBER_NOT_FOUND` but never both for the same kind of failure) - verifiable by reading the tool source and matching test expectations
  2. `ExportProjectUseCase` compiles with zero references to `Transport` layer types - verifiable by grepping `using` directives in `Application/UseCases/ExportProjectUseCase.cs`
  3. Every Phase 1-6 `SUMMARY.md` file has a complete frontmatter block including `requirements_completed` - verifiable by reading each phase's summary file
  4. All Phase 7 tool tests pass when executed (`dotnet test` green), not just by code inspection - verifiable by running the test suite and checking the run produced actual Phase 7 test results
**Plans**: 3 plans
- [ ] 08-01-PLAN.md — DEBT-01 + DEBT-02: normalize FindDependenciesTool error code to MEMBER_NOT_FOUND and eliminate Application->Transport layer violation via new OutputDirectoryNotEmptyException (Wave 1)
- [ ] 08-02-PLAN.md — DEBT-03: backfill requirements-completed frontmatter in 6 Phase 1-6 SUMMARY files (Wave 1, parallel with 08-01)
- [ ] 08-03-PLAN.md — DEBT-04: run full dotnet test suite and record Runtime Verification evidence in Phase 7 SUMMARY files (Wave 2, depends on 08-01)

### Phase 9: Pagination Contract & Structural Cleanup
**Milestone**: v1.2.0
**Goal**: The tool surface agents see is the final v1.2 shape (27 tools, renamed namespace tool, no dispatcher), and there is exactly one pagination contract every subsequent phase will apply
**Depends on**: Phase 8
**Requirements**: PAGE-01, CLEAN-01, CLEAN-02, CLEAN-03
**Success Criteria** (what must be TRUE):
  1. The pagination contract is documented in one place (`docs/` or `CONTEXT.md`) with the exact parameter names, defaults, return metadata shape (`truncated: bool`, `total: int`), and a worked example - verifiable by reading the doc
  2. An agent listing MCP tools sees exactly 27 tools; `analyze_references` is not in the list - verifiable by `tools/list` MCP request or tool registration inspection
  3. An agent listing MCP tools sees `list_namespace_types` and does not see `decompile_namespace` - verifiable by `tools/list` inspection
  4. `README.md` reflects the final 27-tool surface with no stale references to `analyze_references` or `decompile_namespace` - verifiable by grep
**Plans**: 4 plans
- [x] 09-01-PLAN.md — PAGE-01: Create docs/PAGINATION.md canonical contract spec + cross-reference from mcp-tool-design SKILL Principle 4 (Wave 1, parallel with 09-02)
- [x] 09-02-PLAN.md — CLEAN-01: Hard-delete AnalyzeReferencesTool + tests + DI registrations in Program.cs and ToolTestFixture.cs (Wave 1, parallel with 09-01)
- [x] 09-03-PLAN.md — CLEAN-02 (+ list_namespace_types pagination landed here): Rename decompile_namespace → list_namespace_types, implement pagination contract (first reference impl), create PaginationTestTargets fixture, add 7 new Pagination_* tests (Wave 2, depends on 09-02)
- [x] 09-04-PLAN.md — CLEAN-03: Update README.md to 27-tool surface with list_namespace_types section + Pagination intro; roadmap ripple recording that list_namespace_types pagination moved from Phase 11 to Phase 9 in REQUIREMENTS.md and ROADMAP.md (Wave 3, depends on 09-01+09-02+09-03)

### Phase 10: Find-Tool Pagination & Match Enrichment
**Milestone**: v1.2.0
**Goal**: Every `find_*` tool returns paginable, self-describing match records so the agent understands where each match lives without follow-up calls
**Depends on**: Phase 9 (needs pagination contract defined)
**Requirements**: PAGE-02, OUTPUT-01, OUTPUT-02, OUTPUT-03, OUTPUT-04
**Success Criteria** (what must be TRUE):
  1. An agent calling `find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations`, `find_extension_methods`, or `find_compiler_generated_types` can pass `(maxResults, offset)` and receive `(truncated, total)` metadata - verifiable by calling each tool with pagination arguments
  2. A `find_usages` match tells the agent which declaring type the match is in, which containing method signature it belongs to, and at what IL offset - verifiable by calling `find_usages` against a known assembly and inspecting match structure
  3. A `find_dependencies` result groups outward references by kind (calls, field reads, type refs), each with a fully-qualified name and defining assembly - verifiable by calling the tool and inspecting grouping
  4. A `find_implementors` match states whether each match is a direct or transitive implementer/subclass - verifiable by calling the tool against an interface with both direct and transitive implementers
  5. A `find_instantiations` match tells the agent the containing type FQN, containing method signature, and IL offset for each `newobj` site - verifiable by calling the tool against a type with known instantiation sites
**Plans**: TBD

### Phase 11: List/Get/Search Pagination & Member Enrichment
**Milestone**: v1.2.0
**Goal**: Every remaining list-returning or enumeration-returning tool obeys the pagination contract, and `get_type_members` surfaces the inherited/declared distinction and modifier context agents need to pick the right member
**Depends on**: Phase 9 (pagination contract); Phase 10 (pagination contract proven out on find_* tools first)
**Requirements**: PAGE-03, PAGE-04, PAGE-05, OUTPUT-05
**Success Criteria** (what must be TRUE):
  1. An agent calling `list_assembly_types` or `list_embedded_resources` can paginate via `(maxResults, offset)` and always receives `(truncated, total)` metadata - verifiable by calling both tools against a large assembly (e.g. mscorlib)
  2. An agent calling `get_type_members` can paginate and always receives `(truncated, total)` metadata - verifiable by calling it against `System.String`
  3. An agent calling `search_members_by_name` can paginate and always receives `(truncated, total)` metadata - verifiable by calling it with a common search term in a large assembly
  4. A `get_type_members` response distinguishes inherited vs declared members, exposes virtual/abstract/sealed flags, and summarizes attributes per member - verifiable by calling it against a type with inheritance, virtual members, and annotated members
**Plans**: TBD

### Phase 12: IL Token Resolution, Search Enrichment & Truncation Reporting
**Milestone**: v1.2.0
**Goal**: IL disassembly, IL-backed search, and all source/bounded-output tools give agents enough context on the first call to avoid round-tripping through other tools for interpretation or to detect silent truncation
**Depends on**: Phase 9 (final tool names and pagination contract)
**Requirements**: IL-01, IL-02, IL-03, OUTPUT-06, OUTPUT-07, PAGE-07, PAGE-08
**Success Criteria** (what must be TRUE):
  1. An agent reading `disassemble_method` or `disassemble_type` output sees fully-qualified names and defining assembly inline for `call`, `callvirt`, `newobj`, `ldfld`, and `ldstr` operands instead of raw metadata token IDs - verifiable by calling a disassemble tool against a method with cross-assembly calls
  2. An agent can opt into deeper resolution (full parameter signatures, expanded generics) via a `resolveDeep` flag on the disassemble tools - verifiable by calling with and without the flag and comparing output
  3. A `search_strings` match tells the agent the literal value, the containing method FQN, the IL offset, and a window of surrounding IL instructions - verifiable by calling the tool against an assembly with known string literals
  4. A `search_constants` match tells the agent the constant value, the containing method FQN, and the IL offset - verifiable by calling against an assembly with known numeric constants
  5. `decompile_type`, `decompile_method`, `disassemble_type`, and `disassemble_method` report `(truncated, total_lines)` when output exceeds their line cap, making silent truncation visible - verifiable by calling against a type/method that exceeds the cap
  6. `export_project` and `analyze_assembly` report `truncated`/`total` metadata so silent cap truncation becomes observable - verifiable by calling `export_project` on an assembly exceeding the type cap and inspecting the result
**Plans**: TBD

### Phase 13: Scenario Description Sweep
**Milestone**: v1.2.0
**Goal**: Every tool description tells the agent "when is the agent reaching for this?" rather than "what does this tool produce?", and overlapping tools cross-reference each other with cost guidance
**Depends on**: Phases 9-12 (all tool shape changes must be final before descriptions are rewritten, to avoid rewriting twice)
**Requirements**: DESC-01, DESC-02
**Success Criteria** (what must be TRUE):
  1. Every mechanical "Lists all X..." or "Get raw Y..." description identified in the audit (21 tools) has been rewritten to a scenario-oriented "Use this when..." description - verifiable by reading the `[Description]` attribute of every tool file and matching against the audit's worst-offender list
  2. `decompile_type` and `get_type_members` descriptions cross-reference each other with guidance on when to pick each (cost difference, structural vs. decompiled output) - verifiable by reading both descriptions
  3. `list_assembly_types` and `list_namespace_types` descriptions cross-reference each other with guidance on scope difference (whole assembly vs. single namespace) - verifiable by reading both descriptions
**Plans**: TBD

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Test Infrastructure & Baseline | v1.0 | 2/2 | Complete | 2026-04-07 |
| 2. SDK Upgrades & Bug Fixes | v1.0 | 3/3 | Complete | 2026-04-07 |
| 3. IL Infrastructure & Disassembly | v1.0 | 2/2 | Complete | 2026-04-07 |
| 4. Cross-Reference Analysis | v1.0 | 2/2 | Complete | 2026-04-07 |
| 5. Assembly Inspection | v1.0 | 2/2 | Complete | 2026-04-08 |
| 6. Search & Cross-Assembly | v1.0 | 2/2 | Complete | 2026-04-08 |
| 7. Bulk Operations & Documentation | v1.0 | 3/3 | Complete | 2026-04-08 |
| 8. Tech Debt Cleanup | v1.2.0 | 0/? | Not started | - |
| 9. Pagination Contract & Structural Cleanup | v1.2.0 | 0/? | Not started | - |
| 10. Find-Tool Pagination & Match Enrichment | v1.2.0 | 5/5 | Complete    | 2026-04-10 |
| 11. List/Get/Search Pagination & Member Enrichment | v1.2.0 | 0/? | Not started | - |
| 12. IL Token Resolution, Search Enrichment & Truncation Reporting | v1.2.0 | 0/? | Not started | - |
| 13. Scenario Description Sweep | v1.2.0 | 0/? | Not started | - |
