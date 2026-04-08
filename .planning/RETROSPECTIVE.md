# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — Feature Parity

**Shipped:** 2026-04-08
**Phases:** 7 | **Plans:** 16

### What Was Built
- 28 MCP tools (up from 8) covering full .NET static analysis: decompilation, IL disassembly, cross-references, assembly inspection, search, cross-assembly, and bulk operations
- 148+ integration tests providing comprehensive regression coverage
- SDK upgrades: MCP SDK 0.4→1.2.0, ICSharpCode.Decompiler 9.1→10.0
- Bug fixes: concurrency limiter, CTS disposal, constructor visibility

### What Worked
- Test-first approach (Phase 1 baseline before SDK upgrades) caught zero regressions — validated the strategy
- Layered architecture (Domain→Infrastructure→Application→Transport) scaled cleanly from 8 to 28 tools with no structural changes
- IL scanning via System.Reflection.Metadata BlobReader proved sufficient for cross-refs, string search, and constant search — no extra dependencies needed
- Phase execution was fast (~71 minutes total across 16 plans) due to well-scoped plans with clear success criteria
- Audit before completion caught architecture violation (ExportProjectUseCase→Transport import) and error code inconsistency

### What Was Inefficient
- ROADMAP.md plan checkboxes fell out of sync with actual execution (plans marked `[ ]` after completion)
- SUMMARY.md `requirements_completed` frontmatter not populated for Phases 1-6 (only Phase 7 had it)
- Phase 7 tests couldn't be runtime-verified due to missing dotnet CLI during execution
- Nyquist validation was never completed for any phase (all 7 phases have draft VALIDATION.md)

### Patterns Established
- Domain interface → Infrastructure service → Use case → MCP tool pattern for every feature
- TestTargets class library with purpose-built types for each phase's tests
- ToolTestFixture with full DI container mirroring Program.cs registration
- IL scanning pattern: MetadataReader → MethodBodyBlock → BlobReader → ILOpCode dispatch

### Key Lessons
1. SDK upgrades with a test baseline is the right order — test-first gives confidence for breaking changes
2. Duplicating small helpers between services (IL scanning) is preferable to coupling unrelated services
3. WholeProjectDecompiler should be used directly, not wrapped in the decompiler service — different workflow, different abstraction
4. PEFile is needed for PE header access; MetadataFile alone doesn't expose PEReader
5. Assembly-level attributes require disabling auto-generated attributes in test projects

### Cost Observations
- Sessions: Multiple across 2 days
- Notable: 16 plans executed in ~71 minutes total — averaging ~4.4 minutes per plan

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 | 7 | 16 | Established test-first, layered architecture, IL scanning patterns |

### Cumulative Quality

| Milestone | Tests | Zero-Dep Additions |
|-----------|-------|--------------------|
| v1.0 | 148+ | 20 new tools using only existing dependencies |

### Top Lessons (Verified Across Milestones)

1. Test baseline before SDK upgrades prevents silent regressions
2. Layered architecture scales without structural changes when domain interfaces are clean
