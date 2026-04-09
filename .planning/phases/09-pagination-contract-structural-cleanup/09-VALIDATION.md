---
phase: 9
slug: pagination-contract-structural-cleanup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-09
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `09-RESEARCH.md` → `## Validation Architecture` section.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x + FluentAssertions 8.x |
| **Config file** | `Tests/ILSpy.Mcp.Tests.csproj` (standard package refs; no special config) |
| **Test collection** | `[Collection("ToolTests")]` via `Tests/Fixtures/ToolTestCollection.cs` + `Tests/Fixtures/ToolTestFixture.cs` |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypes"` |
| **Full suite command** | `dotnet test ILSpy.Mcp.sln` |
| **Estimated runtime** | Quick: ~10s (~11 filtered tests) · Full: ~30s (~174 tests expected) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~ListNamespaceTypes"`
- **After every plan wave:** Run `dotnet test ILSpy.Mcp.sln`
- **Before `/gsd:verify-work`:** Full suite must be green (exit code 0)
- **Max feedback latency:** ~10 seconds (filtered) / ~30 seconds (full)

Baseline: 173 passing tests from Phase 8. After Phase 9: 173 − 5 (deleted `AnalyzeReferencesToolTests`) + 6 new `Pagination_*` tests = **174 expected**.

---

## Per-Task Verification Map

Tasks will be numbered `09-NN-MM` after planner assigns plans. Requirements map to four IDs: `PAGE-01`, `CLEAN-01`, `CLEAN-02`, `CLEAN-03`.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 09-*-* | * | * | PAGE-01 | doc check | `test -f docs/PAGINATION.md && grep -q "\[pagination:" docs/PAGINATION.md && grep -q "maxResults" docs/PAGINATION.md && grep -q "nextOffset" docs/PAGINATION.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | PAGE-01 | doc check | `grep -q "docs/PAGINATION.md" .claude/skills/mcp-tool-design/SKILL.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-01 | grep | `! test -f Transport/Mcp/Tools/AnalyzeReferencesTool.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-01 | grep | `! test -f Tests/Tools/AnalyzeReferencesToolTests.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-01 | grep | `! grep -rq "AnalyzeReferencesTool\|analyze_references" Transport/ Application/ Tests/ Domain/ Infrastructure/ Program.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-01 | build | `dotnet build ILSpy.Mcp.sln` | ✅ existing | ⬜ pending |
| 09-*-* | * | * | CLEAN-01 | full suite | `dotnet test ILSpy.Mcp.sln` | ✅ existing | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | grep | `grep -q "list_namespace_types" Transport/Mcp/Tools/ListNamespaceTypesTool.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | grep | `grep -q "ListNamespaceTypesUseCase" Application/UseCases/ListNamespaceTypesUseCase.cs && ! test -f Application/UseCases/DecompileNamespaceUseCase.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | grep | `grep -q "ListNamespaceTypesTool" Program.cs && grep -q "ListNamespaceTypesTool" Tests/Fixtures/ToolTestFixture.cs && ! grep -q "DecompileNamespaceTool" Program.cs Tests/Fixtures/ToolTestFixture.cs` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.ListsTypesInNamespace"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.OrdersByKindThenAlphabetically"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.NestedTypesIndentedUnderParent"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.InvalidNamespace_ThrowsNamespaceNotFound"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.MaxResultsLimitsOutput"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FooterPresent"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FooterShapeRegex"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 (PAGE-06) | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FirstPageTruncated"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 (PAGE-06) | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_FinalPage"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 (PAGE-06) | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_OffsetBeyondTotal"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 (PAGE-06) | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_CeilingRejected"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-02 (PAGE-06) | integration (NEW) | `dotnet test --filter "FullyQualifiedName~ListNamespaceTypesToolTests.Pagination_ZeroMaxResultsRejected"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-03 | grep | `grep -q "27 tools" README.md && ! grep -q "28 tools" README.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-03 | grep | `! grep -q "analyze_references" README.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-03 | grep | `! grep -q "decompile_namespace" README.md && grep -q "#### \`list_namespace_types\`" README.md && grep -q "maxResults" README.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-03 | grep | `grep -q "docs/PAGINATION.md" README.md` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | CLEAN-03 | grep | `grep -A 15 "### 4. Pagination" .claude/skills/mcp-tool-design/SKILL.md \| grep -q "docs/PAGINATION.md"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | Roadmap ripple | grep | `grep "PAGE-06" .planning/REQUIREMENTS.md \| grep -q "Phase 9"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | Roadmap ripple | grep | `! sed -n '80p' .planning/ROADMAP.md \| grep -q "PAGE-06"` | ❌ W0 | ⬜ pending |
| 09-*-* | * | * | Roadmap ripple | grep | `! sed -n '85p' .planning/ROADMAP.md \| grep -q "maxTypes"` | ❌ W0 | ⬜ pending |

*Task IDs will be filled in by the planner. Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

The existing `DecompileNamespaceToolTests.cs` already covers the functional shape of the renamed tool (5 tests). Phase 9 adds:

- [ ] `TestTargets/Types/PaginationTestTargets.cs` — NEW fixture file with `namespace ILSpy.Mcp.TestTargets.Pagination;` containing 105 empty `public class TypeN {}` declarations. Required by all `Pagination_*` integration tests.
- [ ] `Tests/Tools/ListNamespaceTypesToolTests.cs` (renamed from `DecompileNamespaceToolTests.cs`) — existing 5 tests updated for `maxResults` parameter + 6 new `Pagination_*` tests:
  - `Pagination_FooterPresent`
  - `Pagination_FooterShapeRegex`
  - `Pagination_FirstPageTruncated`
  - `Pagination_FinalPage`
  - `Pagination_OffsetBeyondTotal`
  - `Pagination_CeilingRejected`
  - `Pagination_ZeroMaxResultsRejected`
- [ ] `Tests/Tools/AnalyzeReferencesToolTests.cs` — DELETED (5 tests gone).
- [ ] Framework install: **none required** — xUnit 2.9.x + FluentAssertions 8.x + Microsoft.NET.Test.Sdk already wired in `Tests/ILSpy.Mcp.Tests.csproj`.

**Wave 0 sequencing note:** `TestTargets/Types/PaginationTestTargets.cs` must land BEFORE the `Pagination_*` tests compile. Suggested atomic-commit ordering within the CLEAN-02 plan: (1) create fixture, (2) rename + rewrite use case / tool, (3) update DI in `Program.cs` + `ToolTestFixture.cs`, (4) rename + extend test file.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Human-readable header prose in footer-bearing responses is still sensible (not garbled by refactor) | CLEAN-02 | Subjective readability, not regex-able | Run `dotnet test --filter "Pagination_FooterPresent"` in verbose mode and eyeball the captured stdout line above the `[pagination:...]` footer. |
| `docs/PAGINATION.md` prose is coherent and matches the worked examples in CONTEXT.md | PAGE-01 | Doc quality is a reviewer call, not a grep | Read `docs/PAGINATION.md` end-to-end after plan completion; confirm the three worked examples (zero-match, mid-page, final-page) are verbatim from CONTEXT.md. |
| Tool count assertion is visually correct in README tool catalogue | CLEAN-03 | Counting by hand sanity check in addition to the `grep -q "27 tools"` guard | Open README.md, scroll the tool catalogue, count headings under `####` and confirm `27`. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (fixture file, renamed test file, deleted test file)
- [ ] No watch-mode flags in any test command
- [ ] Feedback latency < 30s (full suite)
- [ ] `nyquist_compliant: true` set in frontmatter (flip after planner assigns task IDs and sign-off checks pass)

**Approval:** pending
