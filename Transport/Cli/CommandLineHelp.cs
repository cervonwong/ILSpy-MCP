namespace ILSpy.Mcp.Transport.Cli;

public static class CommandLineHelp
{
    public static bool IsRequested(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        if (string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return args.Any(static arg =>
            string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));
    }

    public static string GetText()
    {
        return """
ILSpy MCP Server

Usage:
  ilspy-mcp [--transport <stdio|http>] [--host <value>] [--port <value>]
  ilspy-mcp help

Options:
  --transport <stdio|http>  Transport mode (default: stdio)
  --host <value>            HTTP bind host (HTTP transport only, default: 0.0.0.0)
  --port <value>            HTTP bind port (HTTP transport only, default: 3001)
  -h, --help                Show this help message and exit
""";
    }
}
