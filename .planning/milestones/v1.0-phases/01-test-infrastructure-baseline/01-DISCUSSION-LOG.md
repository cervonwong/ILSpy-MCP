# Phase 1: Test Infrastructure & Baseline - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-07
**Phase:** 01-test-infrastructure-baseline
**Areas discussed:** Test assembly strategy, Test structure, Coverage depth per tool, Existing test handling

---

## Test Assembly Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Custom test assembly | Build a small C# class library with known types. Deterministic assertions, survives .NET version changes. | ✓ |
| Runtime DLLs only | Use System.Collections.dll etc. Real-world but fragile across .NET versions. | |
| Both | Custom + runtime DLLs. Strongest but more code. | |

**User's choice:** Custom test assembly
**Notes:** None

### Follow-up: Project structure

| Option | Description | Selected |
|--------|-------------|----------|
| Separate project | Standalone class library (ILSpy.Mcp.TestTargets) in same repo/solution | ✓ |
| Embedded source compiled at test time | Compile via Roslyn during test setup | |

**User's choice:** Separate project in same repo
**Notes:** User confirmed "same repo" — not a separate repository

### Follow-up: Type coverage

| Option | Description | Selected |
|--------|-------------|----------|
| Comprehensive set | Classes, interfaces, abstract classes, structs, enums, generics, nested types, static classes, extension methods, inheritance hierarchies | ✓ |
| Minimal set | Just enough for current 8 tools | |
| You decide | Claude picks based on tool needs | |

**User's choice:** Comprehensive set
**Notes:** None

---

## Test Structure

### Shared setup

| Option | Description | Selected |
|--------|-------------|----------|
| Shared fixture class | xUnit IClassFixture/ICollectionFixture, builds DI once | ✓ |
| Base test class | Abstract base with setup, inheritance-based | |
| You decide | Claude picks best pattern | |

**User's choice:** Shared fixture class
**Notes:** None

### Test class organization

| Option | Description | Selected |
|--------|-------------|----------|
| One class per tool | DecompileTypeToolTests.cs, etc. Clear ownership, scales well. | ✓ |
| Grouped by concern | Keep current pattern (integration, truncation, errors) | |
| You decide | Claude picks | |

**User's choice:** One class per tool
**Notes:** None

---

## Coverage Depth per Tool

### Thoroughness level

| Option | Description | Selected |
|--------|-------------|----------|
| Thorough baseline | 3-5 tests per tool: happy path, edge cases, error cases | ✓ |
| Minimal smoke tests | 1-2 tests per tool | |
| Exhaustive | 8-12 tests per tool, every parameter combination | |

**User's choice:** Thorough baseline
**Notes:** None

### Assertion style

| Option | Description | Selected |
|--------|-------------|----------|
| Structural assertions | Verify sections, headers, names, counts in output | ✓ |
| Existence only | Just check non-empty, no throws | |
| Snapshot testing | Compare full output against saved snapshots | |

**User's choice:** Structural assertions
**Notes:** None

---

## Existing Test Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Replace with new tests | Delete existing files, write fresh against custom assembly | ✓ |
| Keep and supplement | Leave existing, add new alongside | |
| Refactor in place | Update existing to use new fixture/assembly | |

**User's choice:** Replace with new tests
**Notes:** None

---

## Claude's Discretion

- Specific type names and structure within test assembly
- Exact fixture implementation choice (IClassFixture vs ICollectionFixture)
- Test method naming convention

## Deferred Ideas

None — discussion stayed within phase scope.
