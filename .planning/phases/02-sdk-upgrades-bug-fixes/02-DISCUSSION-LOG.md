# Phase 2: SDK Upgrades & Bug Fixes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-07
**Phase:** 02-sdk-upgrades-bug-fixes
**Areas discussed:** Decompiler upgrade, Semaphore enforcement, CTS disposal pattern, Constructor exposure

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Decompiler upgrade | ICSharpCode.Decompiler 9.1→10.x strategy | ✓ |
| Semaphore enforcement | Where to enforce MaxConcurrentOperations | ✓ |
| CTS disposal pattern | Fix CancellationTokenSource leak | ✓ |
| Constructor exposure | How .ctor/.cctor appear in output | ✓ |

**User's choice:** "Use your own discretion for all problems. Prioritise robustness."
**Notes:** User delegated all four areas to Claude's discretion with robustness as the guiding principle. No specific preferences or constraints beyond what's in PROJECT.md and REQUIREMENTS.md.

---

## Claude's Discretion

All four gray areas were delegated to Claude:
- **Decompiler upgrade:** Big bang upgrade, minimal breaking changes for this codebase
- **Semaphore enforcement:** Decorator/service pattern at use case layer, singleton semaphore
- **CTS disposal:** Disposable wrapper from TimeoutService, consolidated CTS creation
- **Constructor exposure:** New "Constructors" section in get_type_members, .ctor/.cctor naming for decompile_method

## Deferred Ideas

None — discussion stayed within phase scope.
