---
phase: 04-cross-reference-analysis
plan: "01"
subsystem: analysis
tags: [il-scanning, cross-reference, system-reflection-metadata, opcode-parsing]

# Dependency graph
requires:
  - phase: 03-il-infrastructure-disassembly
    provides: IL disassembly infrastructure and decompiler service patterns
provides:
  - ICrossReferenceService domain interface with 4 cross-reference operations
  - ILSpyCrossReferenceService IL scanning implementation using System.Reflection.Metadata
  - FindUsages, FindImplementors, FindDependencies, FindInstantiations use cases
  - CrossReferenceTargets test assembly with known call/impl/instantiation patterns
affects: [04-02-transport-tools, test-coverage]

# Tech tracking
tech-stack:
  added: []
  patterns: [IL bytecode scanning with BlobReader, ILOpCode operand size table, MemberReference/MethodDefinition token resolution]

key-files:
  created:
    - Domain/Models/CrossReferenceResult.cs
    - Domain/Services/ICrossReferenceService.cs
    - Infrastructure/Decompiler/ILSpyCrossReferenceService.cs
    - Application/UseCases/FindUsagesUseCase.cs
    - Application/UseCases/FindImplementorsUseCase.cs
    - Application/UseCases/FindDependenciesUseCase.cs
    - Application/UseCases/FindInstantiationsUseCase.cs
    - TestTargets/Types/CrossReferenceTargets.cs
  modified: []

key-decisions:
  - "IL scanning via System.Reflection.Metadata BlobReader — no new dependencies needed"
  - "Complete ILOpCode operand size table for correct IL reader advancement across all opcodes"
  - "FindImplementors uses two-pass approach: direct implementors first, then indirect via base type check"
  - "FindDependencies deduplicates results using HashSet on target member name"

patterns-established:
  - "ICrossReferenceService: separate domain interface for IL analysis (follows IDisassemblyService pattern)"
  - "IL scanning pattern: CSharpDecompiler for type system + MetadataFile.GetMethodBody + BlobReader for raw IL"
  - "Token resolution: MemberReference parent -> TypeReference/TypeDefinition for declaring type name"

requirements-completed: [XREF-01, XREF-02, XREF-03, XREF-04]

# Metrics
duration: 8min
completed: 2026-04-08
---

# Phase 4 Plan 1: Cross-Reference Analysis Domain, Infrastructure, and Use Cases Summary

**IL scanning cross-reference engine using System.Reflection.Metadata BlobReader for usage tracking, implementor discovery, dependency analysis, and instantiation finding**

## Performance

- **Duration:** 8 min
- **Started:** 2026-04-08T02:42:31Z
- **Completed:** 2026-04-08T02:50:21Z
- **Tasks:** 4
- **Files modified:** 8

## Accomplishments
- ICrossReferenceService domain interface with 4 operations following existing layered architecture patterns
- Full IL bytecode scanner using System.Reflection.Metadata that parses call/callvirt/ldfld/stfld/newobj opcodes
- Complete ILOpCode operand size table covering all .NET IL instructions for correct reader advancement
- Cross-reference test targets with known IRepository/FileRepository/DataService call patterns for validation

## Task Commits

Each task was committed atomically:

1. **Task 1: Domain models and interface** - `7757ec5` (feat)
2. **Task 2: Infrastructure IL scanner** - `b4990fa` (feat)
3. **Task 3: Application use cases** - `0cf89ea` (feat)
4. **Task 4: Test targets and build verification** - `86d71ff` (feat)

## Files Created/Modified
- `Domain/Models/CrossReferenceResult.cs` - UsageResult, ImplementorResult, DependencyResult, InstantiationResult records with UsageKind/DependencyKind enums
- `Domain/Services/ICrossReferenceService.cs` - Port interface with FindUsages, FindImplementors, FindDependencies, FindInstantiations
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` - IL scanning adapter (778 lines) using MetadataReader + BlobReader
- `Application/UseCases/FindUsagesUseCase.cs` - Formats usage sites with IL offsets
- `Application/UseCases/FindImplementorsUseCase.cs` - Groups direct/indirect implementors
- `Application/UseCases/FindDependenciesUseCase.cs` - Groups dependencies by kind
- `Application/UseCases/FindInstantiationsUseCase.cs` - Lists newobj sites with method context
- `TestTargets/Types/CrossReferenceTargets.cs` - IRepository, FileRepository, DatabaseRepository, CachedFileRepository, DataService, FileProcessor

## Decisions Made
- Used System.Reflection.Metadata BlobReader for IL scanning instead of higher-level ICSharpCode APIs — provides direct opcode-level access needed for cross-reference analysis
- Built complete ILOpCode operand size lookup table — ensures correct IL reader advancement for all ~220 opcodes
- Two-pass FindImplementors (direct then indirect) — simpler than recursive traversal and sufficient for single-assembly scope
- FindDependencies deduplicates via HashSet — avoids duplicate entries when same member referenced multiple times

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Domain/infrastructure/use case layer complete and building
- Ready for Phase 4 Plan 2: MCP transport tools (find_usages, find_implementors, find_dependencies, find_instantiations), DI wiring in Program.cs, and integration tests
- All 58 existing tests pass — no regressions

## Self-Check: PASSED

All 8 created files verified present. All 4 task commits verified in git log.

---
*Phase: 04-cross-reference-analysis*
*Completed: 2026-04-08*
