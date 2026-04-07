---
phase: 2
slug: sdk-upgrades-bug-fixes
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-07
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x with FluentAssertions 8.x |
| **Config file** | `Tests/ILSpy.Mcp.Tests.csproj` |
| **Quick run command** | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build` |
| **Full suite command** | `dotnet test Tests/ILSpy.Mcp.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Tests/ILSpy.Mcp.Tests.csproj --no-build`
- **After every plan wave:** Run `dotnet test Tests/ILSpy.Mcp.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-xx | 01 | 1 | SDK-02 | integration | `dotnet test --filter "Category=Baseline"` | ✅ | ⬜ pending |
| 02-02-xx | 02 | 2 | SDK-03 | unit | `dotnet test --filter "Semaphore"` | ❌ W0 | ⬜ pending |
| 02-02-xx | 02 | 2 | SDK-04 | unit | `dotnet test --filter "Timeout\|Disposal"` | ❌ W0 | ⬜ pending |
| 02-02-xx | 02 | 2 | SDK-05 | integration | `dotnet test --filter "Constructor"` | ❌ W0 | ⬜ pending |
| 02-02-xx | 02 | 2 | TEST-04 | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Regression test stubs for semaphore enforcement (SDK-03)
- [ ] Regression test stubs for CTS disposal (SDK-04)
- [ ] Regression test stubs for constructor exposure (SDK-05)
- [ ] Test target types with constructors in TestTargets assembly

*Existing Phase 1 baseline (31 tests) covers SDK-01 and SDK-02 regression detection.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| CTS memory leak under sustained load | SDK-04 | GC behavior non-deterministic in short test runs | Run 1000 rapid calls, check no CTS accumulation via debugger or finalizer count |

*All other behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
