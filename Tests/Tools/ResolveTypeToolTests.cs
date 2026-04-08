using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class ResolveTypeToolTests
{
    private readonly ToolTestFixture _fixture;

    public ResolveTypeToolTests(ToolTestFixture fixture) => _fixture = fixture;

    private string TestDirectory => Path.GetDirectoryName(_fixture.TestAssemblyPath)!;

    [Fact]
    public async Task ResolveType_ExactFullName_FindsAssembly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ResolveTypeTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            3,
            CancellationToken.None);

        result.Should().Contain("ILSpy.Mcp.TestTargets");
        result.Should().Contain("SimpleClass");
        result.Should().Contain("match(es) found");
    }

    [Fact]
    public async Task ResolveType_PartialName_FindsAssembly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ResolveTypeTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            "SimpleClass",
            3,
            CancellationToken.None);

        result.Should().Contain("ILSpy.Mcp.TestTargets.SimpleClass");
    }

    [Fact]
    public async Task ResolveType_NonExistentType_ReturnsEmpty()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ResolveTypeTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            "ZzzNonExistentType999",
            3,
            CancellationToken.None);

        result.Should().Contain("0 match");
    }

    [Fact]
    public async Task ResolveType_InvalidDirectory_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ResolveTypeTool>();

        var act = () => tool.ExecuteAsync(
            "C:/nonexistent_dir_12345",
            "SomeType",
            3,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("DIRECTORY_NOT_FOUND");
    }

    [Fact]
    public async Task ResolveType_DepthZero_OnlyScansRoot()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ResolveTypeTool>();

        // TestTargets.dll is in the root of AppContext.BaseDirectory, so depth 0 should find it
        var result = await tool.ExecuteAsync(
            TestDirectory,
            "SimpleClass",
            0,
            CancellationToken.None);

        result.Should().Contain("SimpleClass");
    }
}
