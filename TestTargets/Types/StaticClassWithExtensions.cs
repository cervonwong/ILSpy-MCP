namespace ILSpy.Mcp.TestTargets;

public static class StringExtensions
{
    public static string Reverse(this string s)
    {
        var chars = s.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    public static bool IsPalindrome(this string s)
    {
        var reversed = s.Reverse();
        return string.Equals(s, reversed, StringComparison.OrdinalIgnoreCase);
    }

    public static string Truncate(this string s, int maxLength)
    {
        if (s.Length <= maxLength)
            return s;

        return s[..maxLength] + "...";
    }
}
