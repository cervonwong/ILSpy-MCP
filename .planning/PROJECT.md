# ILSpy MCP — Feature Parity

## What This Is

An MCP (Model Context Protocol) server that exposes ILSpy's .NET decompilation and static analysis capabilities as tools for AI assistants. Currently has 8 tools covering basic type inspection (~25-30% of ILSpy GUI functionality). This milestone extends it to full reverse engineering feature parity — cross-reference tracing, IL output, assembly metadata, string search, resource extraction, and bulk decompilation.

## Core Value

AI assistants can perform complete .NET static analysis workflows — not just read code, but trace execution, find usages, search strings, and navigate across types and assemblies.

## Requirements

### Validated

- ✓ Decompile individual types and methods — existing
- ✓ List assembly types — existing
- ✓ Analyze assembly structure — existing
- ✓ Get type members — existing
- ✓ Find type hierarchy — existing
- ✓ Search members by name — existing
- ✓ Find extension methods — existing

### Active

- [ ] Upgrade MCP SDK from 0.4.0-preview.3 to stable 1.x
- [ ] Upgrade ICSharpCode.Decompiler from 9.1 to 10.x
- [ ] Fix MaxConcurrentOperations semaphore enforcement
- [ ] Fix CancellationTokenSource leak in TimeoutService
- [ ] Include constructors (.ctor/.cctor) in get_type_members and decompile_method
- [ ] Cross-reference analysis: find_usages, find_implementors, find_instantiations, find_dependencies
- [ ] IL/CIL output for types and methods
- [ ] List assembly references (name, version, culture, public key token)
- [ ] Resolve type across assemblies in a directory
- [ ] Load all assemblies from a folder for cross-assembly analysis
- [ ] String search across assembly IL (ldstr operands)
- [ ] Constant/enum search via ldc.*/FieldDefinition.HasDefault
- [ ] Assembly metadata (target framework, PE bitness, strong name, entry point)
- [ ] Assembly-level and type-level custom attribute inspection
- [ ] List and extract embedded resources
- [ ] List nested types and find compiler-generated types
- [ ] Bulk decompilation: decompile_namespace, export_project
- [~] Critical-path test coverage for P0 features and bug fixes — Phase 1 established regression baseline (31 tests for all 8 tools)

### Out of Scope

- Multi-assembly session management (P3) — architectural change, deferred to next milestone
- Dynamic/runtime analysis (debugging, memory inspection, hooking) — ILSpy is static analysis only
- VB.NET output — low value for reverse engineering workflows
- Mobile/desktop GUI — this is an MCP server for AI assistants

## Context

- Built on `ICSharpCode.Decompiler` and `System.Reflection.Metadata` — no new dependencies needed for any feature
- Architecture follows Domain interface → Infrastructure implementation → Application use case → Transport MCP tool
- IL scanning (cross-refs, string search, constant search) shares common infrastructure — build a reusable ILScanner service
- Each tool is independently shippable
- MCP SDK upgrade (0.4 → 1.x) may have breaking API changes in tool registration and transport
- ILSpy Decompiler upgrade (9.1 → 10.x) may have API changes in decompiler/disassembler surface

## Constraints

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK — no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes to ensure nothing breaks
- **Compatibility**: Must not break existing 8 tools during upgrades

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Upgrade SDKs before new features | Clean foundation, avoid rework on deprecated APIs | — Pending |
| Fix bugs before new features | Stable baseline for tests to validate against | — Pending |
| P0-P2 this milestone, P3 deferred | Session management is architectural change, separable | — Pending |
| Critical-path tests (not exhaustive) | P0 + bug fixes get thorough tests, lighter elsewhere | — Pending |
| Reusable ILScanner for IL-based features | Cross-refs, string search, constant search share scanning logic | — Pending |

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
*Last updated: 2026-04-07 after Phase 1 completion — test infrastructure and regression baseline established*
