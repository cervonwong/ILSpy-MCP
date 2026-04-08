---
phase: 6
slug: search-cross-assembly
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-08
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x |
| **Config file** | `tests/ILSpyMCP.Tests/ILSpyMCP.Tests.csproj` |
| **Quick run command** | `dotnet test tests/ILSpyMCP.Tests --filter "Category=Phase06" --no-build` |
| **Full suite command** | `dotnet test tests/ILSpyMCP.Tests` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/ILSpyMCP.Tests --filter "Category=Phase06" --no-build`
- **After every plan wave:** Run `dotnet test tests/ILSpyMCP.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | SRCH-01 | integration | `dotnet test --filter "StringSearch"` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | SRCH-02 | integration | `dotnet test --filter "ConstantSearch"` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 2 | XASM-01 | integration | `dotnet test --filter "ResolveType"` | ❌ W0 | ⬜ pending |
| 06-02-02 | 02 | 2 | XASM-02 | integration | `dotnet test --filter "DirectoryLoad"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for SRCH-01, SRCH-02 string/constant search
- [ ] Test stubs for XASM-01, XASM-02 cross-assembly resolution
- [ ] Test fixture assemblies with known strings and constants for deterministic verification

*Existing xUnit infrastructure covers framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Large directory performance | XASM-02 | Requires real NuGet cache or large assembly directory | Time `search_strings` on a directory with 50+ assemblies; should complete in < 30s |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
