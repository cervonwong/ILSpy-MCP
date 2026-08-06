# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.1] - 2026-08-06

### Fixed

- **Search tools ignored non-public types**: `find_extension_methods` and `search_members_by_name` filtered results to `public` types only, silently omitting `internal`/`private` types. Both tools now search all types defined in the assembly (#2)
- **Config file watching caused CPU spikes**: the .NET host watched the working directory (often the agent's project directory) for configuration changes, producing notification floods and CPU spikes during compilation. Config reload-on-change is now disabled via `DOTNET_hostBuilder__reloadConfigOnChange` (#3)

## [1.2.0] - 2026-04-12

### Added

- **Pagination contract**: All list-returning tools now accept `maxResults` and `offset` parameters with sensible defaults, and always return `[pagination:{...}]` footer with `total`, `returned`, `truncated`, and `nextOffset` fields
- **IL token resolution**: `disassemble_type` and `disassemble_method` now resolve metadata token references inline (fully-qualified names for `call`/`callvirt`/`newobj`/`ldfld`/`ldstr` instructions)
- `resolveDeep` flag for IL disassembly tools to expand full type signatures for parameters and generics
- Match enrichment for all `find_*` and `search_*` tools — each match is now self-describing with declaring type, containing method, IL offset
- Surrounding IL window in `search_strings` output for context around matched string literals
- HTTP server support with `--transport http`, `--host`, and `--port` CLI options for remote/VM analysis

### Changed

- **Scenario-oriented descriptions**: All 27 tool descriptions rewritten with "Use this when..." framing to help AI agents pick the right tool on first call
- `decompile_namespace` renamed to `list_namespace_types` to match actual behavior (enumerates types with signatures, not a decompile operation)
- `analyze_references` dispatcher tool removed; four `find_*` tools (`find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations`) are now the sole cross-reference entry points
- `get_type_members` now distinguishes inherited vs declared members and exposes virtual/abstract/sealed flags
- Source-returning tools (`decompile_type`, `decompile_method`, `disassemble_type`, `disassemble_method`) now report truncation metadata when output exceeds size cap

### Fixed

- `ExportProjectUseCase` layering violation — no longer imports `McpToolException` from Transport layer
- Error code inconsistency in `FindDependenciesTool` (`METHOD_NOT_FOUND` vs `MEMBER_NOT_FOUND`)
- Silent truncation in bounded-output tools — now always visible via pagination footer

### Dependencies

- ModelContextProtocol SDK upgraded from 0.4.0-preview.3 to 1.2.0
- ICSharpCode.Decompiler upgraded from 9.1.0.7988 to 10.0.0.8330
- Microsoft.Extensions.Hosting upgraded from 8.0.0 to 10.0.0

## [1.0.0] - 2026-04-08

### Added

- **Decompilation tools**: `decompile_type`, `decompile_method`, `list_namespace_types`
- **IL disassembly tools**: `disassemble_type`, `disassemble_method`
- **Type analysis tools**: `list_assembly_types`, `get_type_members`, `find_type_hierarchy`, `find_implementors`, `find_extension_methods`, `find_compiler_generated_types`, `search_members_by_name`
- **Cross-reference tools**: `find_usages`, `find_dependencies`, `find_instantiations`
- **Assembly inspection tools**: `analyze_assembly`, `get_assembly_metadata`, `get_assembly_attributes`, `get_type_attributes`, `get_member_attributes`, `list_embedded_resources`, `extract_resource`
- **Search tools**: `search_strings`, `search_constants`
- **Cross-assembly tools**: `resolve_type`, `load_assembly_directory`
- **Bulk operation tools**: `export_project`
- Cross-platform support: Windows, Linux, macOS (x64 and ARM64)
- Self-contained binaries with no runtime dependencies
- stdio transport for local MCP client integration
- Configurable timeouts and operation limits
- Comprehensive README with tool reference and usage examples

[1.2.1]: https://github.com/cervonwong/ILSpy-MCP/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/cervonwong/ILSpy-MCP/compare/v1.0.0...v1.2.0
[1.0.0]: https://github.com/cervonwong/ILSpy-MCP/releases/tag/v1.0.0
