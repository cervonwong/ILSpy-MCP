using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class DecompileTypeToolTests
{
    private readonly ToolTestFixture _fixture;

    public DecompileTypeToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DecompileType_SimpleClass_ReturnsDecompiledSource()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            null,
            CancellationToken.None);

        result.Should().Contain("class SimpleClass");
        result.Should().Contain("GetGreeting");
        result.Should().Contain("Calculate");
        result.Should().Contain("Name");
    }

    [Fact]
    public async Task DecompileType_InterfaceType_ReturnsDecompiledSource()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Animals.IAnimal",
            null,
            CancellationToken.None);

        result.Should().Contain("interface IAnimal");
        result.Should().Contain("Speak");
        result.Should().Contain("Name");
    }

    [Fact]
    public async Task DecompileType_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task DecompileType_InvalidAssembly_ThrowsAssemblyLoadFailed()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll",
            "SomeType",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }
}
