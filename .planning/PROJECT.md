# ILSpy MCP — .NET Static Analysis for AI Assistants

## What This Is

An MCP (Model Context Protocol) server that exposes ILSpy's .NET decompilation and static analysis capabilities as 28 tools for AI assistants. Covers decompilation, IL disassembly, cross-reference tracing, assembly inspection, string/constant search, resource extraction, cross-assembly analysis, and bulk operations — full reverse engineering feature parity with ILSpy GUI.

## Core Value

AI assistants can perform complete .NET static analysis workflows — not just read code, but trace execution, find usages, search strings, and navigate across types and assemblies.

## Requirements

### Validated

- ✓ Decompile individual types and methods — existing (pre-v1.0)
- ✓ List assembly types — existing (pre-v1.0)
- ✓ Analyze assembly structure — existing (pre-v1.0)
- ✓ Get type members — existing (pre-v1.0)
- ✓ Find type hierarchy — existing (pre-v1.0)
- ✓ Search members by name — existing (pre-v1.0)
- ✓ Find extension methods — existing (pre-v1.0)
- ✓ Upgrade MCP SDK from 0.4.0-preview.3 to stable 1.2.0 — v1.0 (Phase 2)
- ✓ Upgrade ICSharpCode.Decompiler from 9.1 to 10.0 — v1.0 (Phase 2)
- ✓ Fix MaxConcurrentOperations semaphore enforcement — v1.0 (Phase 2)
- ✓ Fix CancellationTokenSource leak in TimeoutService — v1.0 (Phase 2)
- ✓ Include constructors (.ctor/.cctor) in get_type_members and decompile_method — v1.0 (Phase 2)
- ✓ IL/CIL output for types and methods — v1.0 (Phase 3)
- ✓ Cross-reference analysis: find_usages, find_implementors, find_instantiations, find_dependencies, analyze_references — v1.0 (Phase 4)
- ✓ Assembly metadata (target framework, PE bitness, strong name, entry point) — v1.0 (Phase 5)
- ✓ Assembly-level and type-level custom attribute inspection — v1.0 (Phase 5)
- ✓ List and extract embedded resources — v1.0 (Phase 5)
- ✓ List nested types and find compiler-generated types — v1.0 (Phase 5)
- ✓ List assembly references (name, version, culture, public key token) — v1.0 (Phase 5)
- ✓ String search across assembly IL (ldstr operands) — v1.0 (Phase 6)
- ✓ Constant/enum search via ldc.*/FieldDefinition.HasDefault — v1.0 (Phase 6)
- ✓ Resolve type across assemblies in a directory — v1.0 (Phase 6)
- ✓ Load all assemblies from a folder for cross-assembly analysis — v1.0 (Phase 6)
- ✓ Bulk decompilation: decompile_namespace, export_project — v1.0 (Phase 7)
- ✓ Critical-path test coverage (148+ integration tests) — v1.0 (Phases 1-7)
- ✓ README.md with all 28 tools documented — v1.0 (Phase 7)

### Active

(None — next milestone requirements TBD via `/gsd:new-milestone`)

### Out of Scope

- Multi-assembly session management (P3) — architectural change, deferred to next milestone
- Dynamic/runtime analysis (debugging, memory inspection, hooking) — ILSpy is static analysis only
- VB.NET output — low value for reverse engineering workflows
- Mobile/desktop GUI — this is an MCP server for AI assistants
- Assembly editing/patching — not aligned with read-only static analysis scope
- PDB/source matching — source-level debugging is separate concern
- Cross-request caching — premature optimization, defer until performance data exists
- xUnit v3 migration — not worth churn during feature milestone, stay on v2.9.x

## Context

Shipped v1.0 with 12,180 LOC C# across 28 MCP tools, 148+ integration tests.
Tech stack: .NET 10, ICSharpCode.Decompiler 10.0, ModelContextProtocol 1.2.0, xUnit 2.9.x.
Architecture: Domain → Infrastructure → Application → Transport (layered, DI-wired).
IL scanning via System.Reflection.Metadata BlobReader for cross-refs, string search, constant search.

Known tech debt (4 items from v1.0):
- Error code inconsistency in FindDependenciesTool (METHOD_NOT_FOUND vs MEMBER_NOT_FOUND)
- ExportProjectUseCase imports McpToolException from Transport layer (architecture violation)
- SUMMARY.md frontmatter gaps for Phases 1-6
- Phase 7 tests not runtime-verified

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Upgrade SDKs before new features | Clean foundation, avoid rework on deprecated APIs | ✓ Good — zero regressions |
| Fix bugs before new features | Stable baseline for tests to validate against | ✓ Good — baseline caught issues |
| P0-P2 this milestone, P3 deferred | Session management is architectural change, separable | ✓ Good — clean scope boundary |
| Critical-path tests (not exhaustive) | P0 + bug fixes get thorough tests, lighter elsewhere | ✓ Good — 148+ tests, all green |
| Separate IDisassemblyService from IDecompilerService | Disassembly is a distinct concern with different output format | ✓ Good — clean separation |
| Duplicated IL helpers in Search vs CrossRef services | Avoid coupling between services for 2 scan patterns | ⚠️ Revisit — may extract shared helper if a 3rd scanner appears |
| WholeProjectDecompiler used directly (not via IDecompilerService) | Project export is a different workflow from type/method decompilation | ✓ Good — appropriate separation |
| net10.0 target framework | Only .NET 10 runtime available in dev environment | — Pending — verify CI compatibility |

## Constraints

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK — no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes
- **Compatibility**: 28 existing tools must remain stable during future changes

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-08 after v1.0 milestone*
