---
phase: 08-tech-debt-cleanup
plan: 01
subsystem: architecture
tags: [domain-exceptions, wire-contract, layer-boundary, refactor, dotnet]

# Dependency graph
requires:
  - phase: 07-cross-assembly
    provides: Existing Transport/Application tool surface (8 tools) and the exception hierarchy that this plan rewires
provides:
  - OutputDirectoryNotEmptyException domain type carrying DIRECTORY_NOT_EMPTY wire code
  - Clean Application->Transport layer boundary (zero Transport imports in ExportProjectUseCase)
  - Normalized MEMBER_NOT_FOUND wire code across FindDependenciesTool, FindUsagesTool, GetMemberAttributesTool
  - New test FindDependencies_NonExistentMember_ThrowsMemberNotFound pinning the wire-code contract
affects: [08-02, 08-03, phase 09 pagination contract work, phase 13 description sweep]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Domain exception carries wire code via DomainException base class"
    - "Tool layer catches domain exception and maps to McpToolException with identical wire code"
    - "No Application layer imports from Transport (strict layer boundary)"

key-files:
  created:
    - Domain/Errors/OutputDirectoryNotEmptyException.cs
  modified:
    - Application/UseCases/ExportProjectUseCase.cs
    - Transport/Mcp/Tools/ExportProjectTool.cs
    - Transport/Mcp/Tools/FindDependenciesTool.cs
    - Tests/Tools/FindDependenciesToolTests.cs

key-decisions:
  - "Preserved DIRECTORY_NOT_EMPTY wire code verbatim to protect the existing ExportProjectToolTests.FailsOnNonEmptyDirectory guardrail"
  - "Kept Domain/Errors/MethodNotFoundException unchanged (still carries METHOD_NOT_FOUND domain code); only the Transport->wire mapping changed"
  - "MEMBER_NOT_FOUND chosen as the unified wire code because FindUsagesTool and GetMemberAttributesTool already use it; FindDependenciesTool was the outlier"

patterns-established:
  - "Pattern 1: Domain exception with error code -> Tool layer catch -> McpToolException with same wire code (verbatim copy from FindUsagesTool)"
  - "Pattern 2: Specific-to-general catch ordering in every Tool: domain exceptions first, then AssemblyLoadException, then TimeoutException, then OperationCanceledException, finally Exception"

requirements-completed: [DEBT-01, DEBT-02]

# Metrics
duration: 3min
completed: 2026-04-09
---

# Phase 08 Plan 01: Tech Debt Cleanup (DEBT-01 + DEBT-02) Summary

**Closed the Application->Transport layer violation by moving DIRECTORY_NOT_EMPTY into a domain exception, and normalized FindDependenciesTool's wire code from METHOD_NOT_FOUND to MEMBER_NOT_FOUND so all cross-reference tools speak the same contract.**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-04-09T10:36:12Z
- **Completed:** 2026-04-09T10:39:10Z
- **Tasks:** 4
- **Files modified:** 4 (3 source + 1 test)
- **Files created:** 1

## Accomplishments

- Created new `Domain/Errors/OutputDirectoryNotEmptyException.cs` mirroring the `NamespaceNotFoundException` pattern, carrying the DIRECTORY_NOT_EMPTY wire code through the `DomainException` base class
- Removed the `using ILSpy.Mcp.Transport.Mcp.Errors;` layer violation from `ExportProjectUseCase.cs` (DEBT-02 primary fix)
- Replaced `throw new McpToolException("DIRECTORY_NOT_EMPTY", ...)` in the use case with `throw new OutputDirectoryNotEmptyException(outputDirectory)` — the existing `catch (DomainException) { throw; }` handler at the tail of the catch block carries the new exception through unchanged
- Removed the dead `catch (McpToolException) { throw; }` passthrough from both `ExportProjectUseCase.cs` and `ExportProjectTool.cs`
- Added a specific `catch (OutputDirectoryNotEmptyException ex)` in `ExportProjectTool.cs` (placed first in the specific-to-general ordering) that maps to the same `McpToolException("DIRECTORY_NOT_EMPTY", ...)` the client has always seen — **wire contract preserved**
- Changed `FindDependenciesTool.cs` `MethodNotFoundException` catch to throw `McpToolException("MEMBER_NOT_FOUND", ...)` matching the sibling `FindUsagesTool.cs` and `GetMemberAttributesTool.cs` (DEBT-01 primary fix)
- Added new test `FindDependencies_NonExistentMember_ThrowsMemberNotFound` directly pinning the DEBT-01 wire-code contract
- **Full suite: 173 passed / 0 failed / 0 skipped** (up from the previous ~172 — the new test is the single net addition)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create OutputDirectoryNotEmptyException domain type** — `130dfc8` (feat)
2. **Task 2: Rewire ExportProjectUseCase to throw domain exception and remove Transport import** — `854e045` (refactor)
3. **Task 3: Rewire ExportProjectTool catch block and change FindDependenciesTool wire code** — `7d9bf79` (refactor)
4. **Task 4: Add FindDependencies_NonExistentMember_ThrowsMemberNotFound test** — `fe48fab` (test)

**Plan metadata commit:** pending (SUMMARY.md + STATE.md + ROADMAP.md + REQUIREMENTS.md)

## Files Created/Modified

**Created:**
- `Domain/Errors/OutputDirectoryNotEmptyException.cs` — Sealed `DomainException` subclass; error code DIRECTORY_NOT_EMPTY; single `OutputDirectory` property; message text identical to the previous inline McpToolException message so `ErrorSanitizer.SanitizePath` output is byte-identical on the wire

**Modified:**
- `Application/UseCases/ExportProjectUseCase.cs` — Removed layer violation `using ILSpy.Mcp.Transport.Mcp.Errors;`; replaced inline `McpToolException` throw with `throw new OutputDirectoryNotEmptyException(outputDirectory)`; removed dead `catch (McpToolException) { throw; }` passthrough. Zero `McpToolException` references remain in the file.
- `Transport/Mcp/Tools/ExportProjectTool.cs` — Replaced dead `catch (McpToolException) { throw; }` with specific `catch (OutputDirectoryNotEmptyException ex)` placed as the first catch (specific-to-general ordering); maps to `McpToolException("DIRECTORY_NOT_EMPTY", ...)` preserving the wire contract
- `Transport/Mcp/Tools/FindDependenciesTool.cs` — Changed `MethodNotFoundException` catch to throw `McpToolException("MEMBER_NOT_FOUND", ...)` (was `"METHOD_NOT_FOUND"`); log template updated from "Method not found" to "Member not found" matching sibling tools
- `Tests/Tools/FindDependenciesToolTests.cs` — Appended `FindDependencies_NonExistentMember_ThrowsMemberNotFound` asserting the MEMBER_NOT_FOUND wire code on a non-existent method of an existing type

## Decisions Made

- **Preserved DIRECTORY_NOT_EMPTY wire code verbatim:** The `ExportProjectToolTests.FailsOnNonEmptyDirectory` test (Tests/Tools/ExportProjectToolTests.cs:89-97) pins this exact string. Any change would cascade into client-facing breakage. The new domain exception and the new tool-layer catch both use the identical string, so the assertion passes unchanged.
- **Domain MethodNotFoundException stays as METHOD_NOT_FOUND:** Only the Transport->wire mapping changed. The domain layer's concept of "method not found" is semantically correct as METHOD_NOT_FOUND; the wire layer's concept is "member not found" as MEMBER_NOT_FOUND. The two are allowed to differ — the tool layer is the translation point. This matches CONTEXT.md's explicit guidance: "Domain exception stays."
- **Catch ordering: specific-to-general.** `OutputDirectoryNotEmptyException` placed FIRST in `ExportProjectTool.cs` (before `AssemblyLoadException`). This matches the sibling tools and ensures the specific DIRECTORY_NOT_EMPTY path is reached before any generic fallback.
- **Message text preserved byte-for-byte:** The domain exception's constructor message is identical to the previous `McpToolException` message literal. After `ErrorSanitizer.SanitizePath`, the string reaching the MCP client is identical. No wire-level observable change.

## Deviations from Plan

None — plan executed exactly as written.

All four tasks executed in the order specified. All edits matched the exact BEFORE/AFTER snippets in the plan. All verification steps passed on the first attempt. The auto-fix rules were not triggered.

## Issues Encountered

- **Git CRLF normalization noise:** When committing Task 2, `git add` on a single file produced a commit that also included 6 pre-existing SUMMARY.md files with CRLF-to-LF normalization. This is a `.gitattributes`-driven auto-normalization that was pending from earlier sessions and got picked up incidentally. The normalization is harmless (only affects line endings), does not affect any in-flight change, and the Task 2 code change is correctly captured in the commit. Noted for awareness; no action needed.

## User Setup Required

None — this is an internal refactor with no external service configuration.

## Next Phase Readiness

**Ready for Plan 08-02 and 08-03:**
- Layer boundary is clean: Application layer has zero Transport imports (grep-verified)
- Wire contract is normalized: all three cross-reference tools (FindUsagesTool, FindDependenciesTool, GetMemberAttributesTool) throw MEMBER_NOT_FOUND for member-not-found errors
- Test baseline: **173/173 green** — this is the evidence artifact Plan 08-03 (DEBT-04) will reference for runtime verification
- Domain exception pattern is now demonstrated for a directory-validation case; future validation errors can follow this shape

**No blockers for downstream plans in this phase or for Phase 09.**

## Verification Results

Phase-level checks from the plan's `<verification>` block:

1. **Layer boundary clean (DEBT-02):** `grep -c "using ILSpy.Mcp.Transport" Application/UseCases/ExportProjectUseCase.cs` → **0** PASS
2. **Error code normalized (DEBT-01):**
   - `grep -c "METHOD_NOT_FOUND" Transport/Mcp/Tools/FindDependenciesTool.cs` → **0** PASS
   - `grep -c "MEMBER_NOT_FOUND" Transport/Mcp/Tools/FindDependenciesTool.cs` → **1** PASS
3. **Wire contract preserved — full suite green:** `dotnet test ILSpy.Mcp.sln` → **173 passed / 0 failed / 0 skipped** PASS
4. **Specific-to-general catch ordering in ExportProjectTool.cs:** `OutputDirectoryNotEmptyException` → `AssemblyLoadException` → `TimeoutException` → `OperationCanceledException` → `Exception` PASS

All four plan-level success criteria satisfied.

## Self-Check: PASSED

**Files verified present:**
- Domain/Errors/OutputDirectoryNotEmptyException.cs (created)
- Application/UseCases/ExportProjectUseCase.cs (modified)
- Transport/Mcp/Tools/ExportProjectTool.cs (modified)
- Transport/Mcp/Tools/FindDependenciesTool.cs (modified)
- Tests/Tools/FindDependenciesToolTests.cs (modified)
- .planning/phases/08-tech-debt-cleanup/08-01-SUMMARY.md (this file)

**Commits verified present:**
- 130dfc8 (Task 1)
- 854e045 (Task 2)
- 7d9bf79 (Task 3)
- fe48fab (Task 4)

---
*Phase: 08-tech-debt-cleanup*
*Completed: 2026-04-09*
