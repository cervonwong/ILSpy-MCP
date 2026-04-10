using System.Text;
using System.Text.Json;

namespace ILSpy.Mcp.Application.Pagination;

/// <summary>
/// Emits the canonical [truncation:...] footer for source-returning tools.
/// Sibling to PaginationEnvelope. Always appended (even when not truncated).
/// Field order is LOCKED: totalLines, returnedLines, truncated.
/// </summary>
public static class TruncationEnvelope
{
    /// <summary>
    /// Appends a [truncation:{...}] footer for source-returning tools.
    /// Uses line count as the unit (agents reason about lines, not bytes).
    /// </summary>
    public static void AppendSourceFooter(StringBuilder sb, int totalLines, int returnedLines, bool truncated)
    {
        var footerPayload = JsonSerializer.Serialize(new
        {
            totalLines,
            returnedLines,
            truncated,
        });
        sb.AppendLine();
        sb.Append("[truncation:");
        sb.Append(footerPayload);
        sb.Append(']');
    }

    /// <summary>
    /// Appends a [truncation:{...}] footer for export_project.
    /// </summary>
    public static void AppendExportFooter(StringBuilder sb, int totalTypes, int exportedTypes, bool truncated)
    {
        var footerPayload = JsonSerializer.Serialize(new
        {
            totalTypes,
            exportedTypes,
            truncated,
        });
        sb.AppendLine();
        sb.Append("[truncation:");
        sb.Append(footerPayload);
        sb.Append(']');
    }

    /// <summary>
    /// Appends a [truncation:{...}] footer for analyze_assembly.
    /// </summary>
    public static void AppendAnalysisFooter(StringBuilder sb, int totalPublicTypes, int displayedTypes, bool truncated)
    {
        var footerPayload = JsonSerializer.Serialize(new
        {
            totalPublicTypes,
            displayedTypes,
            truncated,
        });
        sb.AppendLine();
        sb.Append("[truncation:");
        sb.Append(footerPayload);
        sb.Append(']');
    }

    /// <summary>
    /// Helper to truncate source text at a character cap and compute line counts.
    /// Snaps the cut point back to the last newline boundary to avoid mid-line cuts.
    /// Returns (truncatedText, totalLines, returnedLines, wasTruncated).
    /// </summary>
    public static (string text, int totalLines, int returnedLines, bool truncated) TruncateSource(
        string fullText, int maxChars)
    {
        if (fullText.Length <= maxChars)
        {
            int lines = CountLines(fullText);
            return (fullText, lines, lines, false);
        }

        var totalLines = CountLines(fullText);
        // Snap back to last newline boundary to avoid cutting mid-line or mid-CRLF
        int cutAt = maxChars;
        while (cutAt > 0 && fullText[cutAt - 1] != '\n') cutAt--;
        if (cutAt == 0) cutAt = maxChars; // no newline found, cut at char limit
        var truncated = fullText[..cutAt];
        var returnedLines = CountLines(truncated);
        return (truncated, totalLines, returnedLines, true);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
        }
        return count;
    }
}
