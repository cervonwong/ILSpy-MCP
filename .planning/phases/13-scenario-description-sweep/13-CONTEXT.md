# Phase 13: Scenario Description Sweep - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Rewrite every tool-level and parameter-level description across all 27 MCP tools to a consistent reverse-engineering-oriented format, and cross-reference overlapping tools with inline cost/use-case guidance. Requirements: DESC-01, DESC-02.

**In scope:**
- All 27 tool `[Description]` attributes rewritten (both the ~19 mechanical and ~8 existing scenario-oriented)
- All parameter `[Description]` attributes rewritten (concise, no over-explaining)
- Inline cross-references between overlapping tool pairs (DESC-02)

**Not in scope:**
- Tool names, parameter names, or parameter defaults (those are finalized by Phase 9-12)
- Tool behavior or output format changes (Phase 10-12 handled that)
- New tools or capabilities
- README.md updates (CLEAN-03 already covered this in Phase 9)

</domain>

<decisions>
## Implementation Decisions

### Description pattern (DESC-01)

- **D-01:** Every tool description follows a three-part pattern: **purpose** → **reverse-engineering scenario** → **output hint**.
  - **Purpose:** Brief statement of what the tool does (e.g., "Finds all call sites of a method or field").
  - **Scenario:** "Use this when..." framed from a reverse engineer's perspective analyzing compiled .NET assemblies — NOT from a NuGet-library-consumer perspective.
  - **Output hint:** Brief mention of key fields returned so the agent knows what to expect without calling the tool.
  - Length: 1-3 sentences. Compact but complete.

- **D-02:** The framing is reverse-engineering-centric throughout. The existing 7-8 "good" descriptions (analyze_assembly, decompile_type, decompile_method, find_type_hierarchy, find_extension_methods, get_type_members, list_assembly_types, search_members_by_name) must also be rewritten because they use NuGet-developer language ("Use this when you just installed a NuGet library...") which doesn't match the project's actual use case.

- **D-03:** The audit's 6 proposed rewrites (find_usages, disassemble_method, search_strings, find_compiler_generated_types, search_constants, find_dependencies) serve as starting exemplars but must be adapted to the three-part pattern with reverse-engineering framing.

### Cross-reference strategy (DESC-02)

- **D-04:** Cross-references are woven inline into the description text, not as a separate "See also" line. The cross-reference includes a cost/capability hint so the agent can pick the right tool without trying both.

- **D-05:** Required cross-reference pairs:
  - `decompile_type` ↔ `get_type_members` (full source vs. structural API surface)
  - `list_assembly_types` ↔ `list_namespace_types` (assembly-wide listing vs. namespace-scoped with signatures)
  - `load_assembly_directory` ↔ `resolve_type` (workflow pair: load first, resolve second)
  - `disassemble_type` ↔ `disassemble_method` (type-level headers vs. method-level IL bodies)
  - `decompile_type` ↔ `disassemble_type` (C# source vs. IL representation)
  - `decompile_method` ↔ `disassemble_method` (C# source vs. IL representation)

### Scope of rewrites

- **D-06:** All 27 tools get new descriptions — no tool is left untouched. This ensures uniform voice across the entire tool surface.

### Parameter descriptions

- **D-07:** All parameter `[Description]` attributes are rewritten. Keep them concise and direct — no over-explaining. Functional clarity without verbosity. Example: `"Path to the .NET assembly (.dll/.exe)"` not `"The full filesystem path to the target .NET assembly file that you want to analyze. Accepts both .dll and .exe files."`.

### Claude's Discretion

- Exact wording of each description — the pattern and framing are locked, but specific phrasing per tool is Claude's call
- Whether to mention specific output field names or use general terms in the output hint
- Order of the three parts when natural flow suggests a different sequence for a specific tool
- Whether a particular tool's cross-reference belongs in its own description, the partner's description, or both (for pairs beyond the required D-05 list)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audit & requirements
- `.planning/quick/260410-audit-mcp-tools-for-ai-effectiveness-and/260410-AUDIT.md` — The original audit identifying 21 mechanical descriptions, 6 proposed rewrites, and cross-reference gaps. Section 3 (Description Quality) is the primary input.
- `.planning/REQUIREMENTS.md` — DESC-01 and DESC-02 requirement definitions

### Tool source files (all 27)
- `Transport/Mcp/Tools/*.cs` — Every tool file's `[Description]` attribute on both the tool method and its parameters must be updated

### MCP tool design skill
- `.claude/skills/mcp-tool-design/mcp-tool-design.md` — Design principles for AI-effective MCP tools in this project (if it exists; check before reading)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- The 260410 audit doc contains 6 proposed rewrites that serve as exemplars for the new pattern
- 7-8 tools already have scenario-oriented descriptions that can be referenced for structure (even though they need RE-framing updates)

### Established Patterns
- Tool descriptions are `[Description("...")]` attributes on the public static async method in each `*Tool.cs` file
- Parameter descriptions are `[Description("...")]` attributes on each method parameter
- Tools use `[McpServerToolType]` class attribute and `[McpServerTool(Name = "snake_case_name")]` method attribute

### Integration Points
- Only `Transport/Mcp/Tools/*.cs` files are touched — no domain, infrastructure, or application layer changes
- Test files are NOT affected (tests assert on behavior, not description text)
- README.md is NOT in scope (already updated in Phase 9 CLEAN-03)

</code_context>

<specifics>
## Specific Ideas

- Descriptions must be framed for reverse engineers analyzing compiled binaries, not developers consuming NuGet packages
- "Use this when you just installed a NuGet library" → wrong framing for this project
- "Use this when starting analysis of an unfamiliar binary" → correct framing
- The exemplar from discussion: `find_usages` → "Finds all call sites, field reads, and property accesses of a specific member. Use this when tracing how a method propagates through a binary or assessing impact before patching. Returns matches with declaring type, method signature, and IL offset."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 13-scenario-description-sweep*
*Context gathered: 2026-04-10*
