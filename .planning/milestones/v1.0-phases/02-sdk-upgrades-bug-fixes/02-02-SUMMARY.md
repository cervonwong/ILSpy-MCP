---
phase: 02-sdk-upgrades-bug-fixes
plan: 02
subsystem: application-services
tags: [bug-fix, concurrency, resource-leak, timeout, semaphore]
dependency_graph:
  requires: [02-01]
  provides: [TimeoutToken-disposable, IConcurrencyLimiter-service]
  affects: [all-8-use-cases, DI-registration, test-fixture]
tech_stack:
  added: []
  patterns: [disposable-wrapper, semaphore-throttling, constructor-injection]
key_files:
  created:
    - Application/Services/ConcurrencyLimiter.cs
    - Tests/Services/TimeoutServiceTests.cs
    - Tests/Services/ConcurrencyLimiterTests.cs
  modified:
    - Application/Services/TimeoutService.cs
    - Application/UseCases/DecompileTypeUseCase.cs
    - Application/UseCases/DecompileMethodUseCase.cs
    - Application/UseCases/ListAssemblyTypesUseCase.cs
    - Application/UseCases/AnalyzeAssemblyUseCase.cs
    - Application/UseCases/GetTypeMembersUseCase.cs
    - Application/UseCases/FindTypeHierarchyUseCase.cs
    - Application/UseCases/SearchMembersByNameUseCase.cs
    - Application/UseCases/FindExtensionMethodsUseCase.cs
    - Program.cs
    - Tests/Fixtures/ToolTestFixture.cs
decisions:
  - TimeoutToken always creates linked CTS (even with CancellationToken.None) for correctness over micro-optimization
  - ConcurrencyLimiter registered as singleton to share semaphore across scoped use cases
  - Concurrency limiter wraps the entire decompiler call including timeout token creation
requirements-completed: [SDK-03, SDK-04]
metrics:
  duration: 5m
  completed: "2026-04-07T07:59:58Z"
  tasks: 2
  files: 14
  tests_added: 8
  tests_total: 39
requirements:
  - SDK-03
  - SDK-04
---

# Phase 02 Plan 02: CTS Leak Fix and Concurrency Limiter Summary

**One-liner:** Disposable TimeoutToken wrapper eliminates CancellationTokenSource leak; SemaphoreSlim-based ConcurrencyLimiter enforces MaxConcurrentOperations across all 8 use cases.

## What Was Done

### Task 1: TimeoutToken wrapper and ConcurrencyLimiter service with tests (TDD)

- Refactored `ITimeoutService.CreateTimeoutToken` to return `TimeoutToken` (sealed disposable) instead of raw `CancellationToken`
- `TimeoutToken` owns both the timeout CTS and the linked CTS, disposing both in `Dispose()`
- Created `IConcurrencyLimiter` interface and `ConcurrencyLimiter` implementation using `SemaphoreSlim`
- `ConcurrencyLimiter.ExecuteAsync` passes cancellation token to `WaitAsync` so blocked requests unblock on client disconnect
- Semaphore released in `finally` block ensuring release even on exceptions
- 4 TimeoutService regression tests: non-cancelled token, dispose safety, no shared state leak, external cancellation linking
- 4 ConcurrencyLimiter regression tests: below-limit execution, at-limit blocking, cancellation while waiting, exception semaphore release
- **Commit:** `708b436` (RED), `1c21109` (GREEN)

### Task 2: Update all 8 use cases, DI registration, and test fixture

- Replaced broken `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeout.CreateTimeoutToken())` pattern in all 8 use cases with `using var timeout = _timeout.CreateTimeoutToken(cancellationToken)`
- Added `IConcurrencyLimiter _limiter` field and constructor injection to all 8 use cases
- Wrapped each use case's `ExecuteAsync` body with `_limiter.ExecuteAsync(async () => { ... }, cancellationToken)`
- Registered `IConcurrencyLimiter` as singleton in `Program.cs` and `Tests/Fixtures/ToolTestFixture.cs`
- Full test suite: 39 tests pass (31 baseline + 8 new)
- **Commit:** `a319abd`

## Verification Results

- `dotnet test` exits 0 with 39 passing tests
- 0 occurrences of `CancellationTokenSource.CreateLinkedTokenSource` in `Application/UseCases/` (old pattern eliminated)
- 8 occurrences of `_limiter.ExecuteAsync` in `Application/UseCases/` (one per use case)
- 8 occurrences of `using var timeout = _timeout.CreateTimeoutToken` in `Application/UseCases/` (one per use case)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Use case compilation failure after TimeoutToken return type change**
- **Found during:** Task 1 GREEN phase
- **Issue:** Changing `ITimeoutService.CreateTimeoutToken` return type from `CancellationToken` to `TimeoutToken` broke compilation of all 8 use cases (they passed the return value to `CancellationTokenSource.CreateLinkedTokenSource`)
- **Fix:** Applied the timeout pattern fix (Task 2's Change 1) during Task 1 to unblock compilation and test execution
- **Files modified:** All 8 use case files
- **Commit:** `1c21109` (included in Task 1 GREEN commit)

## Decisions Made

1. **Always create linked CTS**: Even when `externalToken` is `CancellationToken.None`, the `TimeoutToken` creates a linked CTS. Linking with `CancellationToken.None` is cheap and avoids the broken `cancellationToken != default` check from the original code.
2. **Singleton ConcurrencyLimiter**: Registered as singleton (not scoped) so the semaphore is shared across all concurrent requests, matching the intent of `MaxConcurrentOperations`.
3. **Timeout inside limiter**: The `using var timeout = _timeout.CreateTimeoutToken(cancellationToken)` is placed inside the `_limiter.ExecuteAsync` lambda, so the timeout clock starts only after acquiring the semaphore slot (not while waiting).
