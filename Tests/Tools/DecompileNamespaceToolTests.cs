using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class DecompileNamespaceToolTests
{
    private readonly ToolTestFixture _fixture;

    public DecompileNamespaceToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListsTypesInNamespace()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Namespace: ILSpy.Mcp.TestTargets");
        result.Should().Contain("SimpleClass");
        result.Should().Contain("types)");
    }

    [Fact]
    public async Task OrdersByKindThenAlphabetically()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets",
            cancellationToken: CancellationToken.None);

        // Enums should appear before Classes in the output
        var enumsIndex = result.IndexOf("Enums:");
        var classesIndex = result.IndexOf("Classes:");
        enumsIndex.Should().BeGreaterThan(-1, "Enums section should exist");
        classesIndex.Should().BeGreaterThan(-1, "Classes section should exist");
        enumsIndex.Should().BeLessThan(classesIndex, "Enums should appear before Classes");

        // Delegates should appear after Classes
        var delegatesIndex = result.IndexOf("Delegates:");
        delegatesIndex.Should().BeGreaterThan(-1, "Delegates section should exist");
        delegatesIndex.Should().BeGreaterThan(classesIndex, "Delegates should appear after Classes");
    }

    [Fact]
    public async Task NestedTypesIndentedUnderParent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets",
            cancellationToken: CancellationToken.None);

        // Outer class should have nested types listed under it
        result.Should().Contain("Outer");
        result.Should().Contain("Nested types:");
        result.Should().Contain("Inner");
    }

    [Fact]
    public async Task InvalidNamespace_ThrowsNamespaceNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Namespace.XYZ",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("NAMESPACE_NOT_FOUND");
    }

    [Fact]
    public async Task MaxTypesLimitsOutput()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets",
            maxTypes: 2,
            cancellationToken: CancellationToken.None);

        // Count how many top-level type entries appear (lines with kind labels at indent level 2)
        var lines = result.Split('\n');
        var typeEntries = lines.Count(l =>
            l.TrimStart().StartsWith("class ") ||
            l.TrimStart().StartsWith("interface ") ||
            l.TrimStart().StartsWith("struct ") ||
            l.TrimStart().StartsWith("enum ") ||
            l.TrimStart().StartsWith("delegate ") ||
            l.TrimStart().StartsWith("type "));

        // Only count top-level (2-space indent), not nested (6-space indent)
        var topLevelEntries = lines.Count(l =>
            l.StartsWith("  ") && !l.StartsWith("    ") &&
            (l.TrimStart().StartsWith("class ") ||
             l.TrimStart().StartsWith("interface ") ||
             l.TrimStart().StartsWith("struct ") ||
             l.TrimStart().StartsWith("enum ") ||
             l.TrimStart().StartsWith("delegate ") ||
             l.TrimStart().StartsWith("type ")));

        topLevelEntries.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public async Task InvalidAssembly_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileNamespaceTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "SomeNamespace",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}
