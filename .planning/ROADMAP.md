# Roadmap: ILSpy MCP — Feature Parity

## Overview

This milestone takes ILSpy MCP from ~25-30% GUI parity (8 basic tools) to full reverse engineering feature parity. We start by establishing a test safety net against the current codebase, then upgrade SDKs and fix bugs with confidence against that baseline. From there we build IL infrastructure for cross-reference analysis and search, add assembly inspection capabilities (metadata, resources, nested types), and finish with bulk operations and documentation. Every phase delivers independently verifiable tools that AI assistants can use for .NET static analysis.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Test Infrastructure & Baseline** - Set up integration test project and establish regression tests for all 8 existing tools as a safety net
- [ ] **Phase 2: SDK Upgrades & Bug Fixes** - Upgrade both SDKs to current stable and fix all known bugs, validated against baseline tests
- [ ] **Phase 3: IL Infrastructure & Disassembly** - Build IL scanning infrastructure and expose disassembly output for types and methods
- [ ] **Phase 4: Cross-Reference Analysis** - Enable tracing of usages, implementations, dependencies, and instantiations across assemblies
- [ ] **Phase 5: Assembly Inspection** - Expose assembly metadata, custom attributes, embedded resources, and nested/compiler-generated types
- [ ] **Phase 6: Search & Cross-Assembly** - Enable string/constant search across IL and type resolution across assembly directories
- [ ] **Phase 7: Bulk Operations & Documentation** - Namespace-level decompilation, full project export, and updated documentation

## Phase Details

### Phase 1: Test Infrastructure & Baseline
**Goal**: A comprehensive integration test suite exists that validates all 8 existing tools against real assemblies, providing a known-good baseline before any code changes
**Depends on**: Nothing (first phase)
**Requirements**: TEST-01
**Success Criteria** (what must be TRUE):
  1. An integration test project exists with test infrastructure (test assemblies, helpers, project references)
  2. Every existing tool (list_assembly_types, decompile_type, decompile_method, get_type_members, get_base_types, get_derived_types, get_type_hierarchy, list_namespaces) has at least one regression test
  3. All regression tests pass against the current (pre-upgrade) codebase, establishing the known-good baseline
  4. Tests run in CI (or can be run via dotnet test) with clear pass/fail reporting
**Plans:** 2 plans

Plans:
- [x] 01-01-PLAN.md — Create TestTargets class library and shared test fixture infrastructure
- [ ] 01-02-PLAN.md — Write regression tests for all 8 tools and remove old test files

### Phase 2: SDK Upgrades & Bug Fixes
**Goal**: Existing tools work identically on current SDK versions with all known bugs fixed, validated by the baseline test suite
**Depends on**: Phase 1
**Requirements**: SDK-01, SDK-02, SDK-03, SDK-04, SDK-05, TEST-04
**Success Criteria** (what must be TRUE):
  1. All baseline regression tests pass after MCP SDK upgrade to 1.2.0 stable
  2. All baseline regression tests pass after ICSharpCode.Decompiler upgrade to 10.x
  3. MaxConcurrentOperations semaphore actually limits concurrent operations (observable under load)
  4. Repeated rapid tool calls do not leak CancellationTokenSource objects
  5. Constructors (.ctor/.cctor) appear in get_type_members output and can be decompiled via decompile_method
**Plans:** 0/3 plans executed

Plans:
- [x] 02-01-PLAN.md — Upgrade ICSharpCode.Decompiler to 10.0.0.8330 and validate MCP SDK 1.2.0
- [x] 02-02-PLAN.md — Fix CancellationTokenSource leak and implement concurrency limiter
- [ ] 02-03-PLAN.md — Expose constructors in get_type_members and decompile_method with regression tests

### Phase 3: IL Infrastructure & Disassembly
**Goal**: Users can view raw CIL disassembly for any type or method, and the IL scanning foundation is in place for downstream features
**Depends on**: Phase 2
**Requirements**: IL-01, IL-02, TEST-03
**Success Criteria** (what must be TRUE):
  1. User can request CIL disassembly of a type and receive complete IL output including all methods
  2. User can request CIL disassembly of a specific method and receive its IL instruction listing
  3. IL disassembly output is structurally correct (valid IL mnemonics, proper metadata token references)
**Plans:** 1/2 plans executed

Plans:
- [x] 03-01-PLAN.md — Domain interface, infrastructure service, and use cases for IL disassembly
- [ ] 03-02-PLAN.md — MCP transport tools, DI wiring, and integration tests for disassemble_type and disassemble_method

### Phase 4: Cross-Reference Analysis
**Goal**: Users can trace execution flow and find all usages, implementations, dependencies, and instantiations within an assembly
**Depends on**: Phase 3
**Requirements**: XREF-01, XREF-02, XREF-03, XREF-04, TEST-02
**Success Criteria** (what must be TRUE):
  1. User can find all call sites and field/property accesses for a given member across an assembly
  2. User can find all types implementing a given interface or extending a given base class
  3. User can find all outward dependencies (calls, field refs) of a method or type
  4. User can find all instantiation sites (newobj) of a given type
  5. Cross-reference results are validated by integration tests against known assemblies
**Plans:** 0/2 plans executed

Plans:
- [x] 04-01-PLAN.md — Domain contracts, ICrossReferenceService infrastructure, and TestTargets cross-reference types
- [ ] 04-02-PLAN.md — Use cases, MCP tools, DI wiring, and integration tests

### Phase 5: Assembly Inspection
**Goal**: Users can fully inspect assembly metadata, custom attributes, embedded resources, and type structure including nested and compiler-generated types
**Depends on**: Phase 2
**Requirements**: META-01, META-02, META-03, META-04, RES-01, RES-02, TYPE-01, TYPE-02
**Success Criteria** (what must be TRUE):
  1. User can retrieve target framework, runtime version, PE bitness, strong name, and entry point for an assembly
  2. User can list all referenced assemblies with name, version, culture, and public key token
  3. User can inspect custom attributes at assembly, type, and member level with their constructor arguments
  4. User can list embedded resources with type and size, and extract content (text inline, binary as base64)
  5. User can list nested types and find compiler-generated types (DisplayClass, async state machines, closures)
**Plans:** 1/2 plans executed

Plans:
- [x] 05-01-PLAN.md — Domain contracts, infrastructure service, and TestTargets extensions
- [ ] 05-02-PLAN.md — Use cases, MCP tools, DI wiring, and integration tests

### Phase 6: Search & Cross-Assembly
**Goal**: Users can search for strings and constants across assembly IL, and resolve types across multiple assemblies in a directory
**Depends on**: Phase 3
**Requirements**: SRCH-01, SRCH-02, XASM-01, XASM-02
**Success Criteria** (what must be TRUE):
  1. User can search for string literals matching a regex pattern and get results with containing method context
  2. User can search for numeric and enum constants across an assembly
  3. User can resolve which assembly in a directory defines a given type name
  4. User can load all assemblies from a directory for cross-assembly analysis operations
**Plans:** 1/2 plans executed

Plans:
- [x] 06-01-PLAN.md — ISearchService, IL scanning for ldstr/ldc.*, search_strings and search_constants tools with tests
- [ ] 06-02-PLAN.md — ICrossAssemblyService, directory scanning, resolve_type and load_assembly_directory tools with tests

### Phase 7: Bulk Operations & Documentation
**Goal**: Users can decompile at namespace and project scale, and all new tools are documented with usage examples
**Depends on**: Phase 4, Phase 5, Phase 6
**Requirements**: BULK-01, BULK-02, DOC-01
**Success Criteria** (what must be TRUE):
  1. User can decompile all types in a namespace with a single tool call
  2. User can export a complete .csproj with all decompiled source files to a target directory
  3. README.md documents all new tools with usage examples and the current feature list is accurate
**Plans:** 3 plans

Plans:
- [ ] 07-01-PLAN.md — Namespace decompilation tool (decompile_namespace) with summary listing, kind ordering, and tests
- [ ] 07-02-PLAN.md — Project export tool (export_project) with WholeProjectDecompiler integration and tests
- [ ] 07-03-PLAN.md — README rewrite documenting all 28 tools with categorized reference and examples

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7
Note: Phase 5 depends only on Phase 2 (not Phase 3/4), so it could run in parallel with Phases 3-4 if needed.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Test Infrastructure & Baseline | 0/2 | Planning complete | - |
| 2. SDK Upgrades & Bug Fixes | 0/3 | Planned    |  |
| 3. IL Infrastructure & Disassembly | 1/2 | In Progress|  |
| 4. Cross-Reference Analysis | 0/2 | Planned    |  |
| 5. Assembly Inspection | 1/2 | In Progress|  |
| 6. Search & Cross-Assembly | 1/2 | In Progress|  |
| 7. Bulk Operations & Documentation | 0/3 | Planned | - |
