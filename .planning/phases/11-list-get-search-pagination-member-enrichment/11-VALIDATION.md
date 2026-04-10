---
phase: 11
slug: list-get-search-pagination-member-enrichment
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-10
---

# Phase 11 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x with FluentAssertions 8.9.0 |
| **Config file** | `Tests/ILSpy.Mcp.Tests.csproj` |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~ListAssemblyTypesToolTests\|FullyQualifiedName~ListEmbeddedResourcesToolTests\|FullyQualifiedName~SearchMembersByNameToolTests\|FullyQualifiedName~GetTypeMembersToolTests" --no-restore -v quiet` |
| **Full suite command** | `dotnet test --no-restore -v quiet` |
| **Estimated runtime** | ~2 seconds (phase tests), ~8 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run quick phase test command
- **After every plan wave:** Run full suite command
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 8 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 11-01-01 | 01 | 1 | PAGE-03 | T-11-01, T-11-02 | maxResults validated > 0 and <= 500 at Transport boundary | integration | `dotnet test --filter "FullyQualifiedName~ListAssemblyTypesToolTests\|FullyQualifiedName~ListEmbeddedResourcesToolTests"` | ✅ | ✅ green |
| 11-01-02 | 01 | 1 | PAGE-05 | T-11-01, T-11-02 | maxResults validated > 0 and <= 500 at Transport boundary | integration | `dotnet test --filter "FullyQualifiedName~SearchMembersByNameToolTests"` | ✅ | ✅ green |
| 11-02-01 | 02 | 1 | OUTPUT-05 | T-11-06 | Inherited member exposure is intentional (accepted risk) | integration | `dotnet build --no-restore -v quiet` | ✅ | ✅ green |
| 11-02-02 | 02 | 1 | PAGE-04, OUTPUT-05 | T-11-04, T-11-05 | maxResults validated > 0 and <= 500; DirectBaseTypes walk limited to one level | integration | `dotnet test --filter "FullyQualifiedName~GetTypeMembersToolTests"` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Detailed Test Coverage

### PAGE-03: list_assembly_types + list_embedded_resources pagination (14 tests)

**ListAssemblyTypesToolTests.cs** (12 tests total, 7 pagination):
| Test | Requirement Behavior | Status |
|------|---------------------|--------|
| `Pagination_DefaultReturnsFooter` | Default call emits [pagination:] footer | ✅ |
| `Pagination_MaxResultsCapsOutput` | maxResults:2 returns exactly 2 | ✅ |
| `Pagination_OffsetSkipsItems` | offset:1 skips first item | ✅ |
| `Pagination_TruncatedTrueWhenMoreExist` | maxResults:1 shows truncated:true | ✅ |
| `Pagination_ExceedingCapRejectsWithInvalidParameter` | maxResults:501 throws INVALID_PARAMETER | ✅ |
| `Pagination_ZeroMaxResultsRejects` | maxResults:0 throws INVALID_PARAMETER | ✅ |
| `Pagination_NegativeMaxResultsRejects` | maxResults:-1 throws INVALID_PARAMETER | ✅ |

**ListEmbeddedResourcesToolTests.cs** (12 tests total, 7 pagination):
| Test | Requirement Behavior | Status |
|------|---------------------|--------|
| `Pagination_DefaultReturnsFooter` | Default call emits [pagination:] footer | ✅ |
| `Pagination_MaxResultsCapsOutput` | maxResults:1 returns exactly 1 | ✅ |
| `Pagination_OffsetSkipsItems` | offset:1 skips first resource | ✅ |
| `Pagination_TruncatedTrueWhenMoreExist` | maxResults:1 shows truncated:true | ✅ |
| `Pagination_ExceedingCapRejectsWithInvalidParameter` | maxResults:501 throws INVALID_PARAMETER | ✅ |
| `Pagination_ZeroMaxResultsRejects` | maxResults:0 throws INVALID_PARAMETER | ✅ |
| `Pagination_NegativeMaxResultsRejects` | maxResults:-1 throws INVALID_PARAMETER | ✅ |

### PAGE-05: search_members_by_name pagination (7 tests)

**SearchMembersByNameToolTests.cs** (11 tests total, 7 pagination):
| Test | Requirement Behavior | Status |
|------|---------------------|--------|
| `Pagination_DefaultReturnsFooter` | Default call emits [pagination:] footer | ✅ |
| `Pagination_MaxResultsCapsOutput` | maxResults:1 returns exactly 1 | ✅ |
| `Pagination_OffsetSkipsItems` | offset:1 skips first member | ✅ |
| `Pagination_TruncatedTrueWhenMoreExist` | maxResults:1 shows truncated:true | ✅ |
| `Pagination_ExceedingCapRejectsWithInvalidParameter` | maxResults:501 throws INVALID_PARAMETER | ✅ |
| `Pagination_ZeroMaxResultsRejects` | maxResults:0 throws INVALID_PARAMETER | ✅ |
| `Pagination_NegativeMaxResultsRejects` | maxResults:-1 throws INVALID_PARAMETER | ✅ |

### PAGE-04 + OUTPUT-05: get_type_members pagination + enrichment (11 tests)

**GetTypeMembersToolTests.cs** (17 tests total, 7 pagination + 4 enrichment):
| Test | Requirement Behavior | Status |
|------|---------------------|--------|
| `Pagination_DefaultReturnsFooter` | Default call emits [pagination:] footer | ✅ |
| `Pagination_MaxResultsCapsOutput` | maxResults:2 returns exactly 2 | ✅ |
| `Pagination_OffsetSkipsItems` | offset:1 changes output | ✅ |
| `Pagination_TruncatedTrueWhenMoreExist` | maxResults:1 shows truncated:true | ✅ |
| `Pagination_ExceedingCapRejectsWithInvalidParameter` | maxResults:501 throws INVALID_PARAMETER | ✅ |
| `Pagination_ZeroMaxResultsRejects` | maxResults:0 throws INVALID_PARAMETER | ✅ |
| `Pagination_NegativeMaxResultsRejects` | maxResults:-1 throws INVALID_PARAMETER | ✅ |
| `Enrichment_InheritedMembersTagged` | Inherited members show [inherited] tag | ✅ |
| `Enrichment_VirtualModifierShown` | Virtual methods show "virtual" modifier | ✅ |
| `Enrichment_OverrideModifierShown` | Override methods show modifier | ✅ |
| `Enrichment_AttributesShownOnMembers` | Attributes shown as short names e.g. [Obsolete] | ✅ |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No new test framework, fixtures, or stubs needed.

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 8s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-04-10

---

## Validation Audit 2026-04-10

| Metric | Count |
|--------|-------|
| Requirements audited | 4 (PAGE-03, PAGE-04, PAGE-05, OUTPUT-05) |
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
| Total tests | 52 (across 4 test classes) |
| Phase-specific tests | 39 (28 pagination + 4 enrichment + 7 existing behavioral) |

_Audited: 2026-04-10_
_Auditor: Claude (validate-phase orchestrator)_
