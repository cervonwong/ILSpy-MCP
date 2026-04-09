---
phase: quick-260411
plan: 01
subsystem: infra
tags: [cli, configuration, aspnetcore, kestrel, switch-mappings]

requires:
  - phase: quick-260407-hz7
    provides: HTTP transport mode selectable via --transport http
provides:
  - --host CLI flag mapped to Transport:Http:Host (HTTP mode only)
  - --port CLI flag mapped to Transport:Http:Port (HTTP mode only)
  - CLI > env > appsettings.json > default precedence chain for HTTP binding
  - Fail-fast reject of --host/--port when running in stdio mode (exit code 2)
  - HttpBindingResolver static helper as unit-testable seam for Program.cs
affects: [http-transport, deployment, systemd, docker]

tech-stack:
  added: []
  patterns:
    - "Use IConfigurationBuilder.AddCommandLine(args, switchMappings) registered AFTER WebApplication.CreateBuilder(args) to make CLI flags the last-wins layer of the config chain"
    - "Extract static public helpers in Program.cs (top-level-statements) for unit-testable seams, avoiding InternalsVisibleTo"
    - "Top-level entry point returns int (exit code 2 for bad usage, 0 for success) via explicit return statements in both branches"

key-files:
  created:
    - Tests/Program/HttpBindingResolutionTests.cs
  modified:
    - Program.cs
    - README.md

key-decisions:
  - "Use switch-mapped AddCommandLine provider instead of hand-rolled parsing — framework handles missing-value errors for free and participates in the standard precedence chain"
  - "Stdio mode rejects --host/--port fail-fast with exit code 2 before building the host, so misconfiguration is loud instead of silent"
  - "HttpBindingResolver is declared public (not internal with InternalsVisibleTo) because top-level-statements Program defaults to internal and the helper contains nothing sensitive"
  - "Both HTTP and stdio branches now return explicit int exit codes because adding `return 2;` in the stdio reject path promotes the entry point to Task<int>"

patterns-established:
  - "CLI flag seam: public static helper class in Program.cs called from both production code path and unit tests, synthetic IConfiguration built via AddInMemoryCollection for test isolation (no env vars, no disk, no process launches)"

requirements-completed: [QUICK-260411]

duration: 2min
completed: 2026-04-09
---

# Quick Task 260411: Add --host CLI Option for HTTP Transport Summary

**First-class `--host` and `--port` CLI flags for HTTP transport with CLI > env > appsettings.json > default precedence, fail-fast stdio-mode rejection, and 10 unit tests against an extracted HttpBindingResolver seam.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-04-09T11:50:17Z
- **Completed:** 2026-04-09T11:52:22Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments

- `--host <value>` and `--port <value>` are now first-class CLI flags on the HTTP transport path, matching the UX already established by `--transport`
- Precedence chain is CLI > env (`Transport__Http__Host/Port`) > appsettings.json > defaults (`0.0.0.0` / `3001`), verified by a dedicated last-provider-wins test
- Stdio mode fails fast with a clear stderr message and exit code `2` when `--host` or `--port` is supplied, preventing silent misconfiguration when transport is flipped via env var
- New `HttpBindingResolver` public static helper class in Program.cs provides the unit-test seam without requiring `InternalsVisibleTo`
- 10 new xUnit tests in `Tests/Program/HttpBindingResolutionTests.cs` (6 for `Resolve`, 4 for `StdioHasBindingFlags`); full suite now reports **183 passed / 0 failed / 0 skipped** (173 baseline + 10 new) — zero regression
- README HTTP Server Reference updated: CLI column populated, precedence sentence expanded with stdio-mode restriction, new usage examples for both "Pre-built binary" and "From source" sections

## Task Commits

All three tasks landed in a single atomic commit per CONTEXT.md "Commit scope: one atomic commit" lock:

1. **Task 1 (HttpBindingResolver + switch mappings + stdio reject) + Task 2 (10 unit tests) + Task 3 (README updates)** — `28acfcd` (feat)

## Files Created/Modified

- `Program.cs` — Added switch-mapped `AddCommandLine` provider after `WebApplication.CreateBuilder(args)`, added stdio reject pre-check with `return 2;`, replaced two `GetValue` calls with `HttpBindingResolver.Resolve(builder.Configuration)`, added `public static class HttpBindingResolver` at file bottom, added explicit `return 0;` at end of both transport branches
- `Tests/Program/HttpBindingResolutionTests.cs` — New test file with 10 `[Fact]` tests: 6 covering `Resolve` (both keys, host-only, port-only, neither, whitespace host, last-provider-wins) and 4 covering `StdioHasBindingFlags` (with host, with port, without flags, empty args)
- `README.md` — Updated "Changing port and host" table CLI column, rewrote precedence sentence to include stdio restriction and default tail, added 5 new CLI-flag usage examples across Pre-built-binary and From-source blocks

## Decisions Made

- **Switch mappings over hand-rolled parsing:** `IConfigurationBuilder.AddCommandLine(args, switchMappings)` after `WebApplication.CreateBuilder(args)` was chosen because (a) it participates in the framework's precedence chain for free, (b) the framework throws a clear error on missing values, and (c) adding `--port` parity cost only one dictionary entry. This matches CONTEXT.md's locked decision.
- **Public helper class, no InternalsVisibleTo:** `HttpBindingResolver` is declared `public` at the bottom of Program.cs. This is the smallest surface change that lets the test project call in without adding `InternalsVisibleTo` metadata. The helper contains only two pure methods — nothing sensitive to expose.
- **Exit code 2 for bad usage:** The stdio reject path uses `return 2;` to distinguish "bad CLI usage" from the `1` that exception-driven startup failures produce.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added explicit `return 0;` at end of both HTTP and stdio branches**

- **Found during:** Task 1 (Program.cs edits)
- **Issue:** Adding `return 2;` to the stdio reject path promoted the top-level-statements entry point to `Task<int>`, but the HTTP branch ended with `app.Run(...)` (void) and the stdio branch ended with `await builder.Build().RunAsync();` — neither branch had an explicit return value, producing `CS0161: '<top-level-statements-entry-point>': not all code paths return a value`.
- **Fix:** Added `return 0;` after `app.Run(...)` in the HTTP branch and after `await builder.Build().RunAsync();` in the stdio branch. Both calls block until the server shuts down, so `return 0;` is reached only on clean shutdown — matching POSIX convention (success = 0).
- **Files modified:** Program.cs
- **Verification:** `dotnet build ILSpy.Mcp.sln -c Debug` — 0 warnings, 0 errors; full test suite 183/183 green.
- **Committed in:** 28acfcd (atomic commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was a direct consequence of the planned `return 2;` addition — the plan's action step 2 implicitly required it. No scope creep; the two new `return 0;` lines are the minimum change needed to restore compilation.

## Issues Encountered

None — the only unplanned work was the `CS0161` fix above, which was a mechanical consequence of the planned stdio reject path.

## User Setup Required

None — no external service configuration required. Users who previously used `Transport__Http__Host` / `Transport__Http__Port` env vars continue to work unchanged; the new CLI flags are a purely additive override layer.

## Verification Evidence

- `dotnet build ILSpy.Mcp.sln -c Debug` — **Build succeeded. 0 Warning(s), 0 Error(s)** (no new warnings introduced; the 2 pre-existing TestTargets warnings are out of scope for this task)
- `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~HttpBindingResolutionTests"` — **Passed! Failed: 0, Passed: 10, Skipped: 0, Total: 10**
- `dotnet test ILSpy.Mcp.sln` (full regression) — **Passed! Failed: 0, Passed: 183, Skipped: 0, Total: 183** — exact match with plan target (173 baseline + 10 new)

## Goal-Backward Check (from must_haves.truths)

- [x] Running with `--transport http --host 127.0.0.1 --port 8080` binds Kestrel via switch-mapped provider (HTTP branch now reads through `HttpBindingResolver.Resolve`, which sees the switch-mapped provider's values as the last provider)
- [x] CLI `--host`/`--port` overrides env vars (last-provider-wins test 6 proves the semantic)
- [x] Env var overrides appsettings.json, appsettings.json overrides defaults (standard `IConfigurationBuilder` precedence, unchanged)
- [x] Defaults resolve to `0.0.0.0` and `3001` (test 4 proves)
- [x] Stdio + `--host` writes stderr error and exits non-zero (test 7 proves detection; `return 2;` wired into stdio branch)
- [x] Stdio + `--port` writes stderr error and exits non-zero (test 8 proves detection)
- [x] Existing 8 tools unchanged over both transports (183/183 full suite passing, zero regression from 173 baseline)
- [x] All existing tests still pass (173 baseline unchanged, 10 new added, 183 total)

## Next Phase Readiness

- HTTP transport CLI UX now matches the pattern users expect for self-contained server binaries (systemd units, containers, tmux sessions)
- `HttpBindingResolver` seam is available if future tasks need to add more HTTP binding fields (scheme, path base, etc.)
- Phase 08 tech-debt cleanup remains complete; this quick task did not touch any Phase 07/08 artifacts

## Self-Check: PASSED

- FOUND: `Tests/Program/HttpBindingResolutionTests.cs`
- FOUND: `.planning/quick/260411-add-host-cli-option-for-http-transport/260411-SUMMARY.md`
- FOUND: commit `28acfcd` (touching Program.cs, README.md, Tests/Program/HttpBindingResolutionTests.cs)
- Build: 0 warnings, 0 errors
- Tests: 183/183 passing (173 baseline + 10 new)

---
*Quick task: 260411-add-host-cli-option-for-http-transport*
*Completed: 2026-04-09*
