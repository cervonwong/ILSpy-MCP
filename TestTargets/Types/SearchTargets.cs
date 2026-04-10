namespace ILSpy.Mcp.TestTargets.Search;

/// <summary>
/// Contains methods with known string literals for search_strings testing.
/// </summary>
public class StringContainer
{
    public string GetGreeting() => "Hello, World!";
    public string GetUrl() => "https://example.com/api";
    public string GetEmpty() => "";
    public void LogMessage() { var msg = "Error: connection failed"; _ = msg; }
}

/// <summary>
/// Contains methods with known numeric constants for search_constants testing.
/// </summary>
public class ConstantContainer
{
    public int GetMagicNumber() => 42;
    public int GetNegative() => -1;
    public long GetBigNumber() => 1234567890L;
    public void UseConstants()
    {
        int x = 255;
        int y = 0;
        _ = x + y;
    }
}
