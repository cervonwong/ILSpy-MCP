namespace ILSpy.Mcp.Domain.Errors;

public sealed class MethodNotFoundException : DomainException
{
    public string MethodName { get; }
    public string TypeName { get; }

    public MethodNotFoundException(string methodName, string typeName)
        : base("METHOD_NOT_FOUND", $"Method '{methodName}' not found in type '{typeName}'")
    {
        MethodName = methodName;
        TypeName = typeName;
    }
}
