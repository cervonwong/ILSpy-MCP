# ILSpy MCP Server

A Model Context Protocol (MCP) server that provides .NET assembly decompilation and analysis capabilities.

## What is this?

ILSpy MCP Server enables AI assistants (like Claude Code, Cursor) to decompile and analyze .NET assemblies directly through natural language commands. It integrates [ILSpy](https://github.com/icsharpcode/ILSpy) to provide powerful reverse-engineering capabilities.

## Quick Start

### Prerequisites

- .NET 9.0 SDK or higher (not needed for pre-built binaries)
- MCP-compatible client (Claude Code, Cursor, Claude Desktop, etc.)

### Installation

Install as a global dotnet tool from NuGet:

```bash
dotnet tool install -g ILSpyMcp.Server
```

To update to the latest version:

```bash
dotnet tool update -g ILSpyMcp.Server
```

### Pre-built Binaries (No .NET Required)

Self-contained binaries are available for airgapped environments or systems without a .NET runtime. Download the latest release from the [GitHub Releases page](https://github.com/gentledepp/ILSpy-Mcp/releases).

| Platform | File |
|----------|------|
| Windows x64 | `ilspy-mcp-win-x64.zip` |
| Linux x64 | `ilspy-mcp-linux-x64.tar.gz` |
| Linux ARM64 | `ilspy-mcp-linux-arm64.tar.gz` |
| macOS x64 | `ilspy-mcp-osx-x64.zip` |
| macOS ARM64 | `ilspy-mcp-osx-arm64.zip` |

Each release includes SHA256 checksums (`.sha256` files) for verifying download integrity.

**Windows:**

```powershell
Expand-Archive ilspy-mcp-win-x64.zip -DestinationPath ilspy-mcp
.\ilspy-mcp\ILSpy.Mcp.exe
```

**Linux:**

```bash
tar -xzf ilspy-mcp-linux-x64.tar.gz -C ilspy-mcp
chmod +x ilspy-mcp/ILSpy.Mcp
./ilspy-mcp/ILSpy.Mcp
```

**macOS:**

```bash
unzip ilspy-mcp-osx-arm64.zip -d ilspy-mcp
chmod +x ilspy-mcp/ILSpy.Mcp
./ilspy-mcp/ILSpy.Mcp
```

To configure an MCP client with the pre-built binary, point the command to the extracted path:

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "type": "stdio",
      "command": "/path/to/ilspy-mcp/ILSpy.Mcp",
      "args": []
    }
  }
}
```

### Configure MCP Client

For **Claude Code**, register the MCP server:

```bash
claude mcp add ilspy-mcp --command "ilspy-mcp" --scope user
```

Or create/update `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "type": "stdio",
      "command": "ilspy-mcp",
      "args": []
    }
  }
}
```

For **Cursor**, add to your MCP settings:

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "command": "ilspy-mcp",
      "args": []
    }
  }
}
```

For **Claude Desktop**, add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "command": "ilspy-mcp",
      "args": []
    }
  }
}
```

## Usage Examples

### Decompile a Type
```
Decompile the String class from /path/to/System.Runtime.dll
```

### List All Types
```
List all types in the assembly /path/to/MyLibrary.dll
```

### Find a Specific Method
```
Find the CalculateTotal method in /path/to/Calculator.dll
```

### Analyze Type Hierarchy
```
Show me the type hierarchy for ProductService in /path/to/ECommerce.dll
```

### Search Members
```
Search for members containing "Authenticate" in /path/to/Auth.dll
```

## Available Tools

| Tool | Description |
|------|-------------|
| `decompile_type` | Decompile and analyze a .NET type from a DLL |
| `decompile_method` | Decompile and analyze a specific method |
| `list_assembly_types` | List all types in an assembly |
| `analyze_assembly` | Get architectural overview of an assembly |
| `get_type_members` | Get complete API surface of a type |
| `find_type_hierarchy` | Find inheritance relationships |
| `search_members_by_name` | Search for members by name |
| `find_extension_methods` | Find extension methods for a type |

## Configuration

The server can be configured via environment variables:

- `ILSpy__MaxDecompilationSize`: Maximum size of decompiled code in bytes (default: 1048576 = 1 MB)
- `ILSpy__DefaultTimeoutSeconds`: Default timeout for operations in seconds (default: 30)
- `ILSpy__MaxConcurrentOperations`: Maximum number of concurrent operations (default: 10)

## Architecture

This server follows a clean architecture with clear separation of concerns:

- **Domain**: Core business logic and entities
- **Application**: Use cases and application services
- **Infrastructure**: External system adapters (ILSpy, file system)
- **Transport**: MCP protocol layer

## Security

- All operations are read-only (no file modifications)
- Assembly path validation
- Timeout and cancellation support
- Request context propagation

## License

MIT — see [LICENSE](LICENSE) for details.
