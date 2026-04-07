namespace ILSpy.Mcp.TestTargets;

public class Outer
{
    public class Inner
    {
        public string Value { get; set; } = string.Empty;
    }

    public enum InnerEnum
    {
        A,
        B,
        C
    }

    private class PrivateNested
    {
        public int Secret;
    }

    public Inner CreateInner()
    {
        return new Inner();
    }
}
