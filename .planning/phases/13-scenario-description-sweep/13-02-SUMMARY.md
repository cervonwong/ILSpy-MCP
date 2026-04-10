---
phase: 13-scenario-description-sweep
plan: 02
subsystem: transport/mcp-tools
tags: [descriptions, reverse-engineering, tool-design]
dependency_graph:
  requires: []
  provides: [DESC-01, DESC-02]
  affects: [Transport/Mcp/Tools/*]
tech_stack:
  added: []
  patterns: [three-part-description, RE-framing, cross-reference-guidance]
key_files:
  created: []
  modified:
    - Transport/Mcp/Tools/FindCompilerGeneratedTypesTool.cs
    - Transport/Mcp/Tools/FindDependenciesTool.cs
    - Transport/Mcp/Tools/FindExtensionMethodsTool.cs
    - Transport/Mcp/Tools/FindImplementorsTool.cs
    - Transport/Mcp/Tools/FindInstantiationsTool.cs
    - Transport/Mcp/Tools/FindTypeHierarchyTool.cs
    - Transport/Mcp/Tools/FindUsagesTool.cs
    - Transport/Mcp/Tools/GetAssemblyAttributesTool.cs
    - Transport/Mcp/Tools/GetAssemblyMetadataTool.cs
    - Transport/Mcp/Tools/GetMemberAttributesTool.cs
    - Transport/Mcp/Tools/GetTypeAttributesTool.cs
    - Transport/Mcp/Tools/GetTypeMembersTool.cs
    - Transport/Mcp/Tools/SearchConstantsTool.cs
    - Transport/Mcp/Tools/SearchStringsTool.cs
decisions:
  - get_type_members uses inline decompile_type cross-reference with cost guidance per D-05
requirements-completed:
  - DESC-01
  - DESC-02
metrics:
  duration: 3m
  completed: 2026-04-10T13:56:09Z
  tasks_completed: 2
  tasks_total: 2
---

# Phase 13 Plan 02: Find/Get/Search Tool Description Sweep Summary

RE-framed [Description] attributes for 14 find_*/get_*/search_* tools to three-part pattern (purpose + RE scenario + output hint) with standardized parameter descriptions and decompile_type cross-reference on get_type_members.

## Task Results

### Task 1: Rewrite find_* tool descriptions (7 tools)
- **Commit:** fb6a7d4
- **Files:** 7 tool files updated
- Replaced all tool-level and parameter-level [Description] attributes
- Removed NuGet-consumer language ("IntelliSense", "valid cast operations")
- Standardized assemblyPath to "Path to the .NET assembly (.dll/.exe)"
- Standardized maxResults/offset parameter descriptions (concise, no colons)

### Task 2: Rewrite get_*/search_* tool descriptions (7 tools) and verify build
- **Commit:** a5cb399
- **Files:** 7 tool files updated
- get_type_members cross-references decompile_type with cost guidance
- search_strings includes RE scenarios (hardcoded URLs, API keys)
- search_constants includes RE scenarios (magic numbers, status codes)
- `dotnet build ILSpy.Mcp.sln` succeeded with 0 errors

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

- All 14 tool files contain "Use this when" or "Use this to" in tool-level [Description]: PASS
- No tool file contains "NuGet" in any [Description]: PASS
- get_type_members cross-references decompile_type with cost guidance: PASS
- `dotnet build ILSpy.Mcp.sln` exits with code 0: PASS

## Self-Check: PASSED
