using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class AnalyzeAssemblyToolTests
{
    private readonly ToolTestFixture _fixture;

    public AnalyzeAssemblyToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnalyzeAssembly_ReturnsStructuralInfo()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeAssemblyTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath, null, CancellationToken.None);

        result.Should().Contain("Assembly:");
        result.Should().Contain("Total Types:");
        result.Should().Contain("Public Types:");
    }

    [Fact]
    public async Task AnalyzeAssembly_ShowsNamespaces()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeAssemblyTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath, null, CancellationToken.None);

        result.Should().Contain("Namespaces:");
        result.Should().Contain("ILSpy.Mcp.TestTargets.Animals");
        result.Should().Contain("ILSpy.Mcp.TestTargets.Shapes");
    }

    [Fact]
    public async Task AnalyzeAssembly_InvalidAssembly_ThrowsException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeAssemblyTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll", null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }
}
