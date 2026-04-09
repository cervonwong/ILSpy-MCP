using System.Text.RegularExpressions;

namespace ILSpy.Mcp.Transport.Mcp.Errors;

/// <summary>
/// Sanitizes error messages before they are returned to MCP clients,
/// removing sensitive server directory structure information.
/// </summary>
internal static class ErrorSanitizer
{
    /// <summary>
    /// Replaces full directory paths in error messages with just the filename,
    /// to avoid leaking server directory structure to clients.
    /// </summary>
    internal static string SanitizePath(string message)
    {
        // Use a regex to find Windows and Unix absolute paths and replace with just the filename
        // Match patterns like C:\Users\...\file.dll or /opt/app/.../file.dll
        return Regex.Replace(message, @"(?:[A-Za-z]:\\|/)(?:[^\s""']+[/\\])+([^\s""'/\\]+)", "$1");
    }
}
