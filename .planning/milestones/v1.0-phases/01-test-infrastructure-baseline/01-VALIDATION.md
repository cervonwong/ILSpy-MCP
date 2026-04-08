---
phase: 1
slug: test-infrastructure-baseline
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-07
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x with FluentAssertions 8.8.0 |
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
| 01-01-01 | 01 | 1 | TEST-01 | integration | `dotnet build Tests/TestTargets/ILSpy.Mcp.TestTargets.csproj` | ❌ W0 | ⬜ pending |
| 01-01-02 | 01 | 1 | TEST-01 | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "ClassName~Fixture"` | ❌ W0 | ⬜ pending |
| 01-01-03 | 01 | 2 | TEST-01 | integration | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "ClassName~Tool"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/TestTargets/ILSpy.Mcp.TestTargets.csproj` — custom test assembly project
- [ ] `Tests/Fixtures/` — shared xUnit fixtures for DI container and assembly paths
- [ ] Test infrastructure builds and existing tests pass

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
