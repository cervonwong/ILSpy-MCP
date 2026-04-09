---
phase: 09-pagination-contract-structural-cleanup
plan: 02
subsystem: api
tags: [mcp-tools, cleanup, dispatcher-removal, analyze_references]

# Dependency graph
requires:
  - phase: 08-tech-debt-cleanup
    provides: Stable 173-test baseline and clean DI registration patterns
provides:
  - Hard deletion of analyze_references dispatcher tool from Transport, DI, and tests
  - Tool count reduced from 28 to 27
  - Clean baseline of 178 passing tests (183 minus 5 deleted dispatcher tests)
affects:
  - 09-03 (CLEAN-02 rename: next plan to edit Program.cs and ToolTestFixture.cs)
  - 09-04 (README tool catalog update)
  - Phase 10 onwards (find_* tools are now the sole cross-reference entry points)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dispatcher anti-pattern removal: delete entirely, no alias or deprecation shim"
    - "Atomic multi-file delete: source file, test file, and both DI registrations in single commit"

key-files:
  created: []
  modified:
    - Program.cs (removed AddScoped<AnalyzeReferencesTool>() at line 188)
    - Tests/Fixtures/ToolTestFixture.cs (removed AddScoped<AnalyzeReferencesTool>() at line 87)
  deleted:
    - Transport/Mcp/Tools/AnalyzeReferencesTool.cs
    - Tests/Tools/AnalyzeReferencesToolTests.cs

key-decisions:
  - "Hard delete with no deprecation alias — dispatcher anti-pattern gets no legacy shim per CONTEXT.md and Principle 7 of mcp-tool-design"
  - "Both DI registrations (Program.cs + ToolTestFixture.cs) deleted in same commit as source file to keep build green"
  - "Test file deletion pulled into Task 1 commit (blocking: build fails while test file references deleted type)"
  - "178 test baseline accepted (not 168): plan written before --host/--port quick task added 10 tests; 183 - 5 deleted = 178 is correct"

patterns-established:
  - "Dispatcher removal pattern: delete source + test file + both DI registrations atomically in one commit"

requirements-completed: [CLEAN-01]

# Metrics
duration: 2min
completed: 2026-04-09
---

# Phase 9 Plan 02: Delete analyze_references Dispatcher Tool Summary

**Hard-deleted the analyze_references dispatcher tool (CLEAN-01): removed source file, test file, and both DI registrations atomically; tool count drops from 28 to 27 with 178/178 tests passing**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-09T14:32:34Z
- **Completed:** 2026-04-09T14:34:15Z
- **Tasks:** 2 (combined into 1 commit due to blocking dependency)
- **Files modified:** 4 (2 deleted, 2 edited)

## Accomplishments
- Deleted `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` (95 lines, the dispatcher tool)
- Deleted `Tests/Tools/AnalyzeReferencesToolTests.cs` (5 xUnit facts testing dispatcher routing)
- Removed `AddScoped<AnalyzeReferencesTool>()` from `Program.cs` line 188
- Removed `AddScoped<AnalyzeReferencesTool>()` from `Tests/Fixtures/ToolTestFixture.cs` line 87
- Full test suite passes: 178 tests, 0 failures (was 183; minus 5 deleted = 178)
- Four underlying use cases (`FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase`) are untouched with 12 references each

## Task Commits

Each task was committed atomically:

1. **Task 1+2: Delete analyze_references dispatcher tool (CLEAN-01)** - `9a45dce` (feat)
   - Both tasks combined into one commit because the test file deletion was a blocking dependency for the Task 1 build verification

**Plan metadata:** _(to be added)_ (docs: complete plan)

## Files Created/Modified
- `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` - DELETED (95 lines, dispatcher to 4 use cases)
- `Tests/Tools/AnalyzeReferencesToolTests.cs` - DELETED (5 facts testing dispatcher routing)
- `Program.cs` - Removed line 188 `AddScoped<AnalyzeReferencesTool>()`
- `Tests/Fixtures/ToolTestFixture.cs` - Removed line 87 `AddScoped<AnalyzeReferencesTool>()`

## Decisions Made

1. **Hard delete, no alias** — CONTEXT.md explicitly locked this decision; Principle 7 of mcp-tool-design calls out `analyze_references` as the canonical dispatcher anti-pattern example. No deprecation shim.

2. **Both DI registrations deleted atomically** — `Program.cs` and `Tests/Fixtures/ToolTestFixture.cs` both register tools in parallel `AddScoped<>` lists. Both lines removed in the same commit to keep the build green (C# compiler catches unresolved type references immediately).

3. **Test file deletion pulled into Task 1 commit** — The Task 1 build verification gate (`dotnet build`) fails while `AnalyzeReferencesToolTests.cs` still references the deleted `AnalyzeReferencesTool` type. Pulling the test file deletion into the same commit is the correct resolution per the plan's intent: "atomic deletion across three files".

4. **178 test baseline accepted** — The plan expected 168 (173 baseline - 5 deleted). The quick task 260411 added 10 tests for `--host/--port` flags after the plan was written, raising the baseline to 183. 183 - 5 = 178. Correct.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test file deletion pulled forward from Task 2 into Task 1 commit**
- **Found during:** Task 1 (build verification step)
- **Issue:** `dotnet build ILSpy.Mcp.sln` fails after deleting `AnalyzeReferencesTool.cs` because `Tests/Tools/AnalyzeReferencesToolTests.cs` still references `AnalyzeReferencesTool` — C# reports CS0246 "type or namespace name could not be found". The plan's Task 1 done criteria requires a clean build, which is impossible without also deleting the test file.
- **Fix:** Deleted `Tests/Tools/AnalyzeReferencesToolTests.cs` during Task 1 execution and committed all four changes (source file, test file, Program.cs line, ToolTestFixture.cs line) in one commit.
- **Files modified:** Tests/Tools/AnalyzeReferencesToolTests.cs
- **Verification:** `dotnet build ILSpy.Mcp.sln` exits 0 with no errors after combined deletion. Task 2 verification (`dotnet test`, guardrail grep) then confirms the test suite and source tree are clean.
- **Committed in:** 9a45dce (combined Task 1+2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The blocking deviation merges Tasks 1 and 2 into a single commit rather than two separate commits. The end state is identical to the plan's intended end state. No scope creep.

## Issues Encountered
None beyond the blocking deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `analyze_references` is completely removed from all source, test, DI, and transport layers
- `Program.cs` and `Tests/Fixtures/ToolTestFixture.cs` are ready for Plan 09-03 (CLEAN-02: rename `decompile_namespace` to `list_namespace_types`)
- The four `find_*` tools (`find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations`) are sole cross-reference entry points
- 178-test baseline established for Phase 09 continued work

---
*Phase: 09-pagination-contract-structural-cleanup*
*Completed: 2026-04-09*

## Self-Check: PASSED

- AnalyzeReferencesTool.cs: DELETED (confirmed absent from disk)
- AnalyzeReferencesToolTests.cs: DELETED (confirmed absent from disk)
- SUMMARY.md: CREATED at .planning/phases/09-pagination-contract-structural-cleanup/09-02-SUMMARY.md
- Commit 9a45dce: FOUND in git log
- Commit 3ead0d5 (metadata): FOUND in git log
