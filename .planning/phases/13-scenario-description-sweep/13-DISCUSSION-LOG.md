# Phase 13: Scenario Description Sweep - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-10
**Phase:** 13-scenario-description-sweep
**Areas discussed:** Description tone & structure, Cross-reference strategy, Scope of rewrites, Parameter descriptions

---

## Description Tone & Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Audit exemplars as-is | Adopt the 6 proposed rewrites from the audit as template. ~1-2 sentences. | |
| Scenario + output hint | Like audit exemplars but always end with output hint. Pattern: purpose → scenario → output hint. | ✓ |
| Minimal scenario lead-in | Shorter: just the scenario trigger, no elaboration. | |

**User's choice:** Scenario + output hint, with refinement: lead with brief purpose (what does this do), then RE scenario, then output hint. Framing must be reverse-engineering perspective, not NuGet-developer.
**Notes:** User specified: "Brief description of actual purpose. E.g. Find usages of WHAT? Then scenario, then input and output hint. The scenario needs to be in a reverse engineer point of view."

**Follow-up: Pattern confirmation**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, this pattern | Purpose → RE scenario → output hint, framed for reverse engineers | ✓ |
| Adjust further | Let me describe what I want differently | |

**User's choice:** Confirmed the refined pattern with previewed examples for find_usages and disassemble_method.

---

## Cross-reference Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Inline guidance | Weave cross-reference into description text naturally. No separate "See also" section. | ✓ |
| Trailing 'See also' line | Add a separate line at the end with the cross-reference. | |

**User's choice:** Inline guidance — woven into the description text with cost/capability hints.

---

## Scope of Rewrites

| Option | Description | Selected |
|--------|-------------|----------|
| Rewrite all 27 | Rewrite every tool description including the 7-8 existing good ones. NuGet-developer framing doesn't match RE use case. | ✓ |
| Only mechanical ones | Only rewrite the ~19 with mechanical descriptions. Leave existing good ones as-is. | |
| Mechanical + light touch | Full rewrite for ~19, light framing adjustment for 7-8 good ones. | |

**User's choice:** Rewrite all 27 for uniform voice and consistent RE framing.

---

## Parameter Descriptions

| Option | Description | Selected |
|--------|-------------|----------|
| Out of scope | Phase 13 requirements only mention tool-level descriptions. | |
| Light improvements only | Fix only misleading or incomplete params. Opportunistic. | |
| Full rewrite | Rewrite all parameter descriptions with RE framing. | ✓ |

**User's choice:** Full rewrite of all parameter descriptions.
**Notes:** User specified: "But do not overexplain." — keep parameter descriptions concise and direct.

---

## Claude's Discretion

- Exact wording of each description
- Whether to mention specific output field names or general terms
- Cross-reference placement for pairs beyond the required list

## Deferred Ideas

None — discussion stayed within phase scope
