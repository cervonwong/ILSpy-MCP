namespace ILSpy.Mcp.TestTargets;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CustomInfoAttribute : Attribute
{
    public string Description { get; }

    public CustomInfoAttribute(string description)
    {
        Description = description;
    }
}

[Serializable]
[CustomInfo("Test class")]
public class AttributedClass
{
    [field: NonSerialized]
    private int _transient;

    [Obsolete("Use NewMethod instead")]
    public void OldMethod()
    {
    }

    public void NewMethod()
    {
    }
}
