---
phase: 04-cross-reference-analysis
plan: "02"
subsystem: transport
tags: [mcp-tools, cross-reference, integration-tests, di-registration]

# Dependency graph
requires:
  - phase: 04-cross-reference-analysis
    plan: "01"
    provides: ICrossReferenceService, use cases, domain models, infrastructure IL scanner
provides:
  - 5 MCP tool classes (find_usages, find_implementors, find_dependencies, find_instantiations, analyze_references)
  - DI wiring in Program.cs and ToolTestFixture.cs
  - 21 integration tests covering all cross-reference tools
affects: [tool-count, test-coverage]

# Tech tracking
tech-stack:
  added: []
  patterns: [McpServerToolType dispatcher pattern for analyze_references, MethodNotFoundException mapped to MEMBER_NOT_FOUND error code]

key-files:
  created:
    - Transport/Mcp/Tools/FindUsagesTool.cs
    - Transport/Mcp/Tools/FindImplementorsTool.cs
    - Transport/Mcp/Tools/FindDependenciesTool.cs
    - Transport/Mcp/Tools/FindInstantiationsTool.cs
    - Transport/Mcp/Tools/AnalyzeReferencesTool.cs
    - Tests/Tools/FindUsagesToolTests.cs
    - Tests/Tools/FindImplementorsToolTests.cs
    - Tests/Tools/FindDependenciesToolTests.cs
    - Tests/Tools/FindInstantiationsToolTests.cs
    - Tests/Tools/AnalyzeReferencesToolTests.cs
  modified:
    - Program.cs
    - Tests/Fixtures/ToolTestFixture.cs

key-decisions:
  - "MethodNotFoundException used for member-not-found errors (no separate MemberNotFoundException class) mapped to MEMBER_NOT_FOUND error code"
  - "AnalyzeReferencesTool uses switch expression on analysisType.ToLowerInvariant() for clean dispatch"
  - "Test assertions use actual output format from use cases (DeclaringType, Kind enum names, IL_XXXX offsets)"

patterns-established:
  - "Cross-reference tool pattern: inject use case + logger, map domain exceptions to McpToolException error codes"
  - "Dispatcher tool pattern: AnalyzeReferencesTool injects all 4 use cases, switches on analysisType string"

requirements-completed: [XREF-01, XREF-02, XREF-03, XREF-04, TEST-02]

# Metrics
duration: 5min
completed: 2026-04-08
---

# Phase 4 Plan 2: Cross-Reference MCP Tools and Integration Tests Summary

**5 MCP tools wiring cross-reference use cases to protocol surface with 21 integration tests validating IL scanning results against CrossRef test targets**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-08T02:57:59Z
- **Completed:** 2026-04-08T03:02:30Z
- **Tasks:** 2
- **Files modified:** 12

## Accomplishments
- 4 dedicated cross-reference MCP tools following established DecompileTypeTool pattern with full error mapping
- AnalyzeReferencesTool dispatcher that routes to all 4 use cases via switch on analysisType parameter
- DI registration for ICrossReferenceService, 4 use cases, and 5 tools in both Program.cs and ToolTestFixture
- 21 integration tests covering usages, implementors, dependencies, instantiations, and dispatcher routing
- Total tool count now 15 (10 existing + 5 new)
- Total test count now 79 (58 existing + 21 new), all passing

## Task Commits

Each task was committed atomically:

1. **Task 1: MCP tools and DI registration** - `5dcbb36` (feat)
2. **Task 2: Integration tests for all 5 tools** - `27f45c1` (test)

## Files Created/Modified
- `Transport/Mcp/Tools/FindUsagesTool.cs` - MCP tool for find_usages with member_name parameter
- `Transport/Mcp/Tools/FindImplementorsTool.cs` - MCP tool for find_implementors
- `Transport/Mcp/Tools/FindDependenciesTool.cs` - MCP tool for find_dependencies with optional methodName
- `Transport/Mcp/Tools/FindInstantiationsTool.cs` - MCP tool for find_instantiations
- `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` - Dispatcher tool routing to 4 use cases
- `Program.cs` - Added ICrossReferenceService, 4 use cases, 5 tools to DI
- `Tests/Fixtures/ToolTestFixture.cs` - Mirrored DI registrations for test infrastructure
- `Tests/Tools/FindUsagesToolTests.cs` - 5 tests: method calls, load, empty, member not found, invalid assembly
- `Tests/Tools/FindImplementorsToolTests.cs` - 4 tests: interface, base class, no implementors, type not found
- `Tests/Tools/FindDependenciesToolTests.cs` - 3 tests: specific method, type-level, type not found
- `Tests/Tools/FindInstantiationsToolTests.cs` - 4 tests: known type, database repo, never instantiated, type not found
- `Tests/Tools/AnalyzeReferencesToolTests.cs` - 5 tests: implementors, usages, dependencies routing, invalid type, missing member

## Decisions Made
- Used MethodNotFoundException (existing) for member-not-found errors mapped to MEMBER_NOT_FOUND code, rather than creating a separate MemberNotFoundException class
- AnalyzeReferencesTool validates memberName requirement for usages analysis before dispatching
- Test assertions match actual use case output format (UsageKind enum names like VirtualCall, MethodCall etc.)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test assertions for actual IL scanning behavior**
- **Found during:** Task 2
- **Issue:** Plan expected FileProcessor to appear in IRepository.Save usages, but IL scanning correctly shows FileProcessor calls Save on concrete FileRepository type, not IRepository interface
- **Fix:** Removed FileProcessor assertion from IRepository.Save usages test; changed zero-result test to use CachedFileRepository.Load (never called)
- **Files modified:** Tests/Tools/FindUsagesToolTests.cs
- **Commit:** 27f45c1

## Issues Encountered
None.

## User Setup Required
None.

## Known Stubs
None - all tools are fully wired to infrastructure implementations.

## Next Phase Readiness
- Phase 4 complete: domain models, infrastructure IL scanner, use cases, MCP tools, and integration tests all in place
- 15 MCP tools total, 79 tests passing
- Ready for next phase

## Self-Check: PASSED
