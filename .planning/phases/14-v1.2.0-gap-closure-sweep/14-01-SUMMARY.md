---
phase: 14-v1.2.0-gap-closure-sweep
plan: 01
subsystem: transport/docs
tags: [cleanup, docs, pagination, structural]
requirements-completed: [CLEAN-01, PAGE-01, CLEAN-03]
gap_closure: true
dependency-graph:
  requires: []
  provides:
    - "27-tool MCP runtime surface (AnalyzeReferencesTool removed)"
    - "docs/PAGINATION.md canonical contract spec"
  affects:
    - "Program.cs DI registration"
    - "Tests/Fixtures/ToolTestFixture.cs DI registration"
    - "README.md link target (now resolves)"
tech-stack:
  added: []
  patterns:
    - "hard-delete of dispatcher tools (no deprecation shim) — reinforces Phase 9 decision"
key-files:
  created:
    - docs/PAGINATION.md
  modified:
    - Program.cs
    - Tests/Fixtures/ToolTestFixture.cs
  deleted:
    - Transport/Mcp/Tools/AnalyzeReferencesTool.cs
    - Tests/Tools/AnalyzeReferencesToolTests.cs
decisions:
  - "Removed AnalyzeReferencesTool DI registration from Tests/Fixtures/ToolTestFixture.cs as well — plan only listed Program.cs, but the test fixture's parallel DI graph also held the registration (would have broken build). Fixed inline under Rule 3."
metrics:
  duration: "~2m"
  completed: 2026-04-12
  tasks: 2
  files_touched: 5
  tests_passing: 229
---

# Phase 14 Plan 01: v1.2.0 Gap Closure — AnalyzeReferencesTool Removal + PAGINATION.md Recreation Summary

Closes CLEAN-01 by hard-deleting the `AnalyzeReferencesTool` dispatcher (restoring the 27-tool MCP surface) and closes PAGE-01/CLEAN-03 by recreating the missing `docs/PAGINATION.md` canonical contract so the README link resolves and downstream gap-closure plans (14-02..05) have an authoritative spec to reference.

## What Landed

### Task 1 — AnalyzeReferencesTool Hard Delete (commit `76e9050`)

Deleted both the dispatcher tool and its dedicated test class:

- `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` — removed
- `Tests/Tools/AnalyzeReferencesToolTests.cs` — removed

Removed DI registrations:

- `Program.cs` line 195 — `services.AddScoped<AnalyzeReferencesTool>();` removed
- `Tests/Fixtures/ToolTestFixture.cs` line 87 — parallel DI graph also had the registration (not called out in plan, see Deviations)

Verification:
- `grep -rn "AnalyzeReferencesTool\|analyze_references" Transport/ Application/ Tests/ Program.cs` → no matches
- `[McpServerToolType]` count across `Transport/Mcp/Tools/*.cs` = **27 files, 27 occurrences**
- `dotnet build ILSpy.Mcp.sln` → 0 errors, 2 pre-existing TestTargets warnings
- `dotnet test ILSpy.Mcp.sln` → **229 passed, 0 failed, 0 skipped**

### Task 2 — docs/PAGINATION.md Canonical Contract (commit `cdedcd6`)

Created `docs/PAGINATION.md` with the required section scaffold:

- `## Parameters` — `maxResults` (default 100, cap 500) and `offset` (default 0)
- `## Footer Format` — field order LOCKED to `total, returned, offset, truncated, nextOffset`
- `## Worked Example` — `find_usages` against 250-match member, correct `truncated`/`nextOffset` math
- `## Truncation Footer` — source/bounded-output reuse of the same envelope
- `## Implementation` — points to `Application.Pagination.PaginationEnvelope.AppendFooter`
- `## Reference Implementation` — points to `FindUsagesUseCase.FormatResults`

Verification:
- `[ -f docs/PAGINATION.md ]` → true
- `grep "docs/PAGINATION.md" README.md` → 1 match (link at line ~287 resolves)
- Field-order string `total.*returned.*offset.*truncated.*nextOffset` appears 3 times in the doc, matching `PaginationEnvelope.cs`

## Contract Alignment

`docs/PAGINATION.md` was authored against `Application/Pagination/PaginationEnvelope.cs` verbatim:

| Aspect | PaginationEnvelope.cs | docs/PAGINATION.md |
|--------|-----------------------|--------------------|
| Field order | `total, returned, offset, truncated, nextOffset` | Same, marked LOCKED |
| `truncated` formula | `offset + returned < total` | Same |
| `nextOffset` formula | `truncated ? offset + returned : null` | Same |
| Footer prefix | `[pagination:` | Same |

No drift — the doc is a readable projection of the code contract.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Removed lingering DI registration in Tests/Fixtures/ToolTestFixture.cs**
- **Found during:** Task 1 grep verification
- **Issue:** Plan only listed `Program.cs` as the DI-registration site, but `Tests/Fixtures/ToolTestFixture.cs:87` held a parallel `services.AddScoped<AnalyzeReferencesTool>();` in the test-scope DI graph. Leaving it would cause test build failure (`AnalyzeReferencesTool` type no longer exists).
- **Fix:** Removed the line.
- **Files modified:** `Tests/Fixtures/ToolTestFixture.cs`
- **Commit:** `76e9050` (bundled with Task 1)

## Self-Check: PASSED

Files verified present on disk:
- `docs/PAGINATION.md` → FOUND
- `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` → MISSING (expected, deleted)
- `Tests/Tools/AnalyzeReferencesToolTests.cs` → MISSING (expected, deleted)

Commits verified in `git log`:
- `76e9050` feat(14-01): Delete AnalyzeReferencesTool dispatcher (CLEAN-01) → FOUND
- `cdedcd6` docs(14-01): Add canonical pagination contract spec (PAGE-01, CLEAN-03) → FOUND

Acceptance criteria:
- 27 `[McpServerToolType]` occurrences across 27 tool files → FOUND
- `dotnet build` → 0 errors
- `dotnet test` → 229/229 passing
- README link `docs/PAGINATION.md` resolves
- All required section headings present in docs/PAGINATION.md
