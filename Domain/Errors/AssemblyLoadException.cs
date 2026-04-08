namespace ILSpy.Mcp.Domain.Errors;

public sealed class AssemblyLoadException : DomainException
{
    public string AssemblyPath { get; }

    public AssemblyLoadException(string assemblyPath, Exception innerException)
        : base("ASSEMBLY_LOAD_FAILED", $"Failed to load assembly '{assemblyPath}'", innerException)
    {
        AssemblyPath = assemblyPath;
    }

    public AssemblyLoadException(string assemblyPath, string message, Exception innerException)
        : base("ASSEMBLY_LOAD_FAILED", message, innerException)
    {
        AssemblyPath = assemblyPath;
    }
}
