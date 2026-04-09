---
phase: 08-tech-debt-cleanup
plan: 02
subsystem: planning-metadata
tags: [frontmatter, audit, debt, doc-only]

# Dependency graph
requires:
  - phase: v1.0 milestone archive
    provides: 16 SUMMARY.md files covering Phases 1-7 (only 6 missing the key)
provides:
  - Uniform requirements-completed frontmatter coverage across all Phase 1-7 SUMMARY files (16/16)
  - Unblocks any audit tool that scans SUMMARY frontmatter for requirement traceability
affects: [Phase 8 doc-debt tracking, future audit tooling, DEBT-03 closure]

# Tech tracking
tech-stack:
  added: []
  patterns: [additive-frontmatter-backfill, no-churn-policy, kebab-case-coexists-with-snake_case]

key-files:
  created:
    - .planning/phases/08-tech-debt-cleanup/08-02-SUMMARY.md
  modified:
    - .planning/milestones/v1.0-phases/01-test-infrastructure-baseline/01-01-SUMMARY.md
    - .planning/milestones/v1.0-phases/01-test-infrastructure-baseline/01-02-SUMMARY.md
    - .planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-01-SUMMARY.md
    - .planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-02-SUMMARY.md
    - .planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-03-SUMMARY.md
    - .planning/milestones/v1.0-phases/06-search-cross-assembly/06-01-SUMMARY.md

key-decisions:
  - "01-01 assigned requirements-completed: [] (infrastructure-only); TEST-01 credited to 01-02 which built the actual regression tests"
  - "Additive-only edits — existing `requirements:` keys on 02-02 and 02-03 left untouched alongside the new `requirements-completed:` key"
  - "Kebab-case and snake_case frontmatter keys coexist in the same YAML document; no schema migration performed (deferred per CONTEXT.md no-churn policy)"

patterns-established:
  - "No-churn backfill: add the minimum key needed, leave all other frontmatter shape variations alone"
  - "Requirement credit rule: credit the plan that produced the user-observable effect, not the plan that built the enabling infrastructure"

requirements-completed: [DEBT-03]

# Metrics
duration: 2 min
completed: 2026-04-09
---

# Phase 08 Plan 02: DEBT-03 Frontmatter Backfill Summary

**Backfilled `requirements-completed:` frontmatter key in the six Phase 1-6 SUMMARY files that lacked it, closing the Phase 1-7 traceability gap at 16/16 total coverage without touching any code or renaming any existing frontmatter keys.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-09T10:36:32Z
- **Completed:** 2026-04-09T10:38:11Z
- **Tasks:** 1
- **Files modified:** 6

## Accomplishments

- Added `requirements-completed: []` to `01-01-SUMMARY.md` (infrastructure-only plan)
- Added `requirements-completed: [TEST-01]` to `01-02-SUMMARY.md` (31 regression tests for all 8 tools)
- Added `requirements-completed: [SDK-01, SDK-02]` to `02-01-SUMMARY.md` (MCP SDK + ICSharpCode.Decompiler upgrade)
- Added `requirements-completed: [SDK-03, SDK-04]` to `02-02-SUMMARY.md` (ConcurrencyLimiter + TimeoutToken disposable) — alongside existing `requirements:` key
- Added `requirements-completed: [SDK-05, TEST-04]` to `02-03-SUMMARY.md` (constructor exposure + regression tests) — alongside existing `requirements:` key
- Added `requirements-completed: [SRCH-01, SRCH-02]` to `06-01-SUMMARY.md` (search_strings + search_constants MCP tools)
- Total count of SUMMARY files with the key went from 10/16 to 16/16 across Phases 1-7
- Zero files outside the six-file target list were modified in these edits

## Task Commits

| Task | Description | Commit | Notes |
|------|-------------|--------|-------|
| 1 | Backfill requirements-completed key in 6 files | `854e045` | Absorbed into Plan 08-01's final commit — see Deviation 1 |
| — | Plan metadata (this SUMMARY + STATE + ROADMAP + REQUIREMENTS) | pending | Created by final metadata commit below |

_Note: Plans 08-01 and 08-02 ran in parallel (Wave 1). The six backfilled SUMMARY files were staged by this plan and the edits landed inside Plan 08-01's final `refactor(08-01)` commit (`854e045`) due to a parallel-execution staging race. The edits ARE committed and traceable in git history; only the commit message attribution is wrong. See Deviation 1 for details._

## Files Created/Modified

- `.planning/milestones/v1.0-phases/01-test-infrastructure-baseline/01-01-SUMMARY.md` — one line added before `metrics:` block
- `.planning/milestones/v1.0-phases/01-test-infrastructure-baseline/01-02-SUMMARY.md` — one line added before `metrics:` block
- `.planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-01-SUMMARY.md` — one line added before `metrics:` block
- `.planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-02-SUMMARY.md` — one line added before `metrics:` block (alongside pre-existing `requirements:` key, which was not touched)
- `.planning/milestones/v1.0-phases/02-sdk-upgrades-bug-fixes/02-03-SUMMARY.md` — one line added before `metrics:` block (alongside pre-existing `requirements:` key, which was not touched)
- `.planning/milestones/v1.0-phases/06-search-cross-assembly/06-01-SUMMARY.md` — one line added before `metrics:` block

Total git-level impact for the six files: 6 insertions, 0 deletions.

## Decisions Made

### 1. 01-01 credited `[]`, not `[TEST-01]`

**Rationale:** TEST-01 says "All existing tools have regression tests." 01-01 built the foundational TestTargets assembly and the `ToolTestFixture` + `ToolTestCollection` DI plumbing, but did NOT write any regression tests. 01-02 wrote the 31 regression tests. The CONTEXT.md ambiguity policy interprets requirement credit as "credit the plan that produced the user-observable effect." A user reading TEST-01 and asking "is this done?" needs to land on 01-02, where the tests actually live. 01-01 being credited would dilute the traceability signal.

### 2. Additive-only — no key renames

**Rationale:** Files 02-02 and 02-03 already have a singular `requirements:` key carrying the same values. The plan explicitly forbids renaming or deleting those pre-existing keys. Adding `requirements-completed:` alongside leaves both shapes readable by any existing tooling while introducing the uniform audit key. The broader kebab-case vs snake_case normalization (e.g., `tech_stack:` → `tech-stack:`) is explicitly deferred per CONTEXT.md no-churn policy.

### 3. Kebab-case and snake_case coexistence accepted

**Rationale:** Phase 1/2 files use snake_case nested frontmatter (`dependency_graph:`, `tech_stack:`, `key_files:`), Phase 3+ use flat kebab-case. Both are legal YAML and no tool in this repo enforces a strict frontmatter schema. Adding one kebab-case key into a snake_case document is safe and was verified by the post-edit grep loop returning OK across all six files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Parallel-execution staging race with Plan 08-01**

- **Found during:** Task 1 commit step
- **Issue:** Plans 08-01 and 08-02 ran in parallel in Wave 1. When this plan staged the six backfilled SUMMARY files and attempted `git commit -m "docs(08-02): ..."`, the working tree had already been partially consumed by Plan 08-01's final commit (`854e045 refactor(08-01): Rewire ExportProjectUseCase to throw domain exception`). That commit absorbed the six staged SUMMARY files into its own changeset under an 08-01-tagged message. By the time this plan's `git commit` call ran, the index was empty (except for unrelated `.planning/config.json` and `ExportProjectUseCase.cs` changes that belong to other work and are out of scope). The 08-02 intended commit message never landed.
- **Fix:** Accepted the state as-is. The six frontmatter edits ARE committed and present on disk — they are reachable via `git log --oneline .planning/milestones/v1.0-phases/01-test-infrastructure-baseline/01-01-SUMMARY.md` which shows `854e045`. The only cost is that the git history attributes the six doc edits to Plan 08-01 instead of Plan 08-02. Rewriting history via `git commit --amend` or interactive rebase was NOT attempted — it would be destructive and was not authorized. Instead, this SUMMARY file documents the race explicitly so any future audit can trace `854e045` back to DEBT-03 work.
- **Files affected:** All six backfilled SUMMARY files
- **Verification:** `grep -rc "^requirements-completed:" .planning/milestones/v1.0-phases/` returns 16 total matches (10 pre-existing + 6 newly backfilled). The verification grep loop from the plan body (`for f in ...; do grep -q ...; done`) prints `OK`. `git show --stat 854e045` shows all six SUMMARY files listed alongside the ExportProjectUseCase.cs change.
- **Committed in:** `854e045` (under Plan 08-01 message due to the race)

**Lesson for Wave 1 parallelism:** Running two plans in parallel on the same repository works for disjoint file sets, but any plan that commits doc files AND runs concurrently with another plan that also commits needs either (a) a git worktree per plan, or (b) a serializing lock on the staging area. The plan's "parallel safety" note was based on file-path disjointness, which holds — but file-path disjointness does NOT prevent staging-area races when both plans call `git add` + `git commit` against the same index.

---

**Total deviations:** 1 auto-fixed (1 blocking — parallel-execution staging race)
**Impact on plan:** The intended doc-only frontmatter backfill landed correctly on all six target files. The only impact is cosmetic: the commit that carries the six edits is labeled with Plan 08-01's message instead of Plan 08-02's. The DEBT-03 audit check (`grep -c "^requirements-completed:"` across Phase 1-7 SUMMARY files) returns 16/16, which is the acceptance criterion. No scope creep.

## Issues Encountered

None beyond the parallel-execution race documented above. The frontmatter edits themselves were straightforward additive single-line inserts and all six landed on the first attempt.

## User Setup Required

None — no external service configuration required. This is a doc-only plan.

## Next Phase Readiness

- **DEBT-03 status:** CLOSED. All Phase 1-7 SUMMARY files now carry the `requirements-completed:` frontmatter key (16/16).
- **DEBT-04 (Runtime Verification blocks in Phase 7 SUMMARIES):** Not this plan's concern. Plan 08-03 will handle it.
- **Phase 8 overall:** After this plan, Phase 8 has Plan 01 (DEBT-01/DEBT-02 architecture-violation fixes) and Plan 02 (DEBT-03 frontmatter backfill) complete. Plan 03 (DEBT-04) remains to close out the phase.
- **Follow-up note:** The parallel-execution staging race described in Deviation 1 is worth flagging for future phase executions. Consider either serializing Wave 1 plans when both touch git staging, or running parallel plans in separate git worktrees to isolate the index.

---
*Phase: 08-tech-debt-cleanup*
*Completed: 2026-04-09*

## Self-Check: PASSED

- [x] All 7 referenced files exist on disk (08-02-SUMMARY.md + 6 backfilled SUMMARY files)
- [x] Commit `854e045` found in git log (carries the six frontmatter edits despite wrong attribution)
- [x] Verification grep loop prints `OK` — all six files have `requirements-completed:` key
- [x] Total count of SUMMARY files with the key is 16/16 across Phases 1-7
- [x] `git diff --stat` for the six target files shows exactly +6 insertions, 0 deletions (verified before commit was absorbed into 854e045)
- [x] Zero files outside the six-file target list were modified by this plan's task
