namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Value object representing a validated directory path.
/// </summary>
public sealed record DirectoryPath
{
    public string Value { get; }

    private DirectoryPath(string value) => Value = value;

    public static DirectoryPath Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Directory path cannot be null or empty.", nameof(path));
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        return new DirectoryPath(Path.GetFullPath(path));
    }
}
