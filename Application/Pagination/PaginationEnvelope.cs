using System.Text;
using System.Text.Json;

namespace ILSpy.Mcp.Application.Pagination;

/// <summary>
/// Emits the canonical [pagination:...] footer defined in docs/PAGINATION.md.
/// Field order is LOCKED: total, returned, offset, truncated, nextOffset.
/// Every paginable tool in Phase 10+ uses this helper — do not inline a footer block.
/// </summary>
public static class PaginationEnvelope
{
    /// <summary>
    /// Appends a leading newline then the [pagination:{...}] footer to the StringBuilder.
    /// Computes truncated = (offset + returned &lt; total) and nextOffset = truncated ? offset + returned : null.
    /// </summary>
    public static void AppendFooter(StringBuilder sb, int total, int returned, int offset)
    {
        var truncated = offset + returned < total;
        int? nextOffset = truncated ? offset + returned : (int?)null;
        var footerPayload = JsonSerializer.Serialize(new
        {
            total,
            returned,
            offset,
            truncated,
            nextOffset,
        });
        sb.AppendLine();
        sb.Append("[pagination:");
        sb.Append(footerPayload);
        sb.Append(']');
    }
}
