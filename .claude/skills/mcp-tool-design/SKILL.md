---
name: mcp-tool-design
description: Design principles for AI-effective MCP tools in this project. Apply when adding or modifying any tool in Transport/Mcp/Tools/.
---

# MCP Tool Design Principles

## When to use this skill

Apply this skill whenever you are: (a) adding a new MCP tool under `Transport/Mcp/Tools/`, (b) modifying an existing tool's signature, parameters, or `[Description]` attribute, (c) reviewing the output schema returned by a use case wired to an MCP tool, or (d) writing the v1.1 cleanup tasks derived from the 260410 audit. The audience for these rules is the developer; the audience for the *tools themselves* is an AI agent that has never seen this codebase before.

## Core mental model

AI agents are **lazy**. They will not chase round-trips. They will not infer hidden affordances. They will not read the tool source. They will read the tool name, the description, and the parameter list — once — and then either pick the tool or ignore it. Every design choice below flows from that fact.

## Principles

### 1. Scenario-oriented descriptions

**Rule:** Every `[Description]` starts by answering "**when** would a user reach for this tool?", not "what does it do mechanically?". Prefer "Use this when…" or "Use this to…" framings.

**Why:** An agent reading a catalogue of 28+ tools needs to filter to "is this relevant to my current intent?" in one pass. Mechanical descriptions force the agent to translate verbs into intents on the fly, which it does badly.

```csharp
// BAD — describes mechanics
[Description("Find all call sites, field accesses, and property usages of a member.")]

// GOOD — describes the user's situation
[Description("Use this when you're about to change a method/field and need to know what will break. Returns every call site so you can assess impact before refactoring.")]
```

### 2. Nested references by default

**Rule:** When a tool's output references something the agent will obviously want next (a metadata token, a type name, a member signature), resolve it inline. Do not return bare identifiers that force a follow-up call.

**Why:** IL disassembly that says `call 0x0A000023` is useless without a second tool call. IL disassembly that says `call instance string [System.Runtime]System.Object::ToString()` is self-sufficient. The cost of resolving the reference once on the server is paid back many times in saved round-trips and reduced agent confusion.

```text
BAD:  Match found in method 'Process' (token 0x06000042)
GOOD: Match found in method 'MyApp.Services.OrderProcessor.Process(Order)' at IL_0023
```

### 3. Assume the agent is lazy

**Rule:** Preemptively include the context the next call would have asked for. If the agent will obviously need the containing type when it sees a method name, include the containing type. If it will obviously need the assembly that defines a referenced type, include the assembly.

**Why:** A round-trip is not free for an agent — it wastes a tool call, expands the conversation, and increases the chance the agent gives up and hallucinates. You are not paying tokens to be polite; you are paying tokens to prevent failure.

```text
BAD:  { "type": "OrderProcessor" }
GOOD: { "type": "MyApp.Services.OrderProcessor", "assembly": "MyApp.dll", "kind": "class", "members": 12 }
```

### 4. Pagination is mandatory for unbounded output (HARD RULE)

**Rule:** Any tool whose output count depends on the input assembly's contents (lists of types, lists of usages, lists of matches, lists of attributes attached to a non-fixed entity, lists of dependencies, etc.) **must** expose pagination parameters. The output **must** report `truncated` and `total` so the agent knows whether more pages exist.

**Why:** Without pagination an agent gets either (a) silent truncation that corrupts its mental model of the assembly, or (b) a multi-megabyte blob that destroys its context window. Both are failures. There are no exceptions: if the count is data-dependent, paginate.

```csharp
// REQUIRED parameters whenever output is unbounded:
int maxResults = 100,        // sensible default
int offset = 0,              // for cursor-style continuation
// ...and the response must include `truncated: bool` and `total: int`.
```

> **Canonical format:** The exact response envelope (header, body, trailing
> `[pagination:...]` footer), the hard ceiling (500), and the rejection
> behavior are specified in **[`docs/PAGINATION.md`](../../../docs/PAGINATION.md)**.
> Every paginable tool in this project obeys that contract — do not invent a
> new shape.

A tool whose output is structurally bounded (e.g., the inheritance chain of a single type, the fixed-shape metadata record of a single assembly) is exempt — and you must justify the exemption in code review.

### 5. Rich but not flooding

**Rule:** Default to a *summary* shape. Offer a `verbose` (or equivalent) flag to opt into deeper detail. Cap inline detail-per-item to a small constant; the agent can always request more for a specific item.

**Why:** Principles 2 and 3 push toward rich output. Principle 4 caps the *count*. This principle caps the *per-item depth*. The goal is enough context to pick the next call correctly, not a flood that wastes the context window. If your default response for one match is more than ~20 lines of text, reconsider.

```csharp
// Heuristic split:
//   default     -> name + containing type + a one-line label
//   verbose=true -> + IL offset, + nearby instructions, + resolved tokens
```

### 6. Naming follows verb-noun convention

**Rule:** Tool names are `verb_noun`, lowercase, snake_case. Use the established verb set: `analyze_*`, `decompile_*`, `disassemble_*`, `find_*`, `get_*`, `list_*`, `search_*`, `load_*`, `resolve_*`, `extract_*`, `export_*`. Pick the verb that matches what the agent would naturally type if it had no documentation.

**Why:** Verb consistency lets an agent guess tool names. `find_*` means "structural locator," `search_*` means "pattern match against IL bytecode," `get_*` means "single-record metadata read," `list_*` means "enumerate everything of a kind." Mixing verbs forces the agent to memorize the catalogue instead of reasoning about it.

If you cannot pick between two verbs, the tool's responsibility is probably wrong — split it.

### 7. One tool, one job

**Rule:** Avoid dispatcher tools that route to N other tools based on a string parameter. If two tools have overlapping responsibility, either merge them or sharpen the boundary in their descriptions. The agent should always know which of two adjacent tools to call.

**Why:** Dispatchers ("pass `analysis_type='usages'` and I'll call the right thing") are the worst of both worlds: they hide the option set behind a parameter the agent has to guess, *and* they advertise overlap with the underlying tools. The 260410 audit identified `analyze_references` as exactly this antipattern.

### 8. Errors carry next-step hints

**Rule:** Every error message should tell the agent what to try next. "Type not found" is useless; "Type 'Foo' not found in assembly 'X.dll'. Use `list_assembly_types` to discover available types or `resolve_type` if the type may live in a referenced assembly" is actionable.

**Why:** Errors are conversational dead ends unless they suggest a recovery path. The lazy-agent rule applies to the failure path too: if you don't tell the agent what to do, it gives up.

## Checklist for new tools

Before opening a PR for any new MCP tool, verify all of the following:

- [ ] Description starts with "Use this when…" or "Use this to…" (Principle 1)
- [ ] Description identifies the user's *situation*, not the tool's mechanics (Principle 1)
- [ ] Output resolves metadata-token / type / member references inline (Principle 2)
- [ ] Output includes the obvious next-call context (containing type, declaring assembly, member kind) (Principle 3)
- [ ] If output count depends on assembly contents: `maxResults` + `offset` parameters present, `truncated`/`total` in response (Principle 4 — HARD)
- [ ] If output is structurally bounded: justification documented in PR description (Principle 4)
- [ ] Default response shape is summary; verbose mode is opt-in via flag (Principle 5)
- [ ] Tool name follows established verb-noun convention from the verb set in Principle 6
- [ ] Tool's responsibility does not duplicate a sibling tool (Principle 7)
- [ ] No new dispatcher-style tools that route via a string `type` parameter (Principle 7)
- [ ] Error messages name the next tool to call when recovery is possible (Principle 8)
- [ ] Tested against a real assembly that triggers pagination (not just a 5-item fixture)

## Reference

- **Audit report:** `.planning/quick/260410-audit-mcp-tools-for-ai-effectiveness-and/260410-AUDIT.md` — full per-tool scorecard and v1.1 priorities
- **User-facing version:** see the "Design Philosophy" section in the project `README.md`
- **Tool source:** `Transport/Mcp/Tools/*.cs`
