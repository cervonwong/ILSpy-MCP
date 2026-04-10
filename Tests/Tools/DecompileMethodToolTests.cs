using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class DecompileMethodToolTests
{
    private readonly ToolTestFixture _fixture;

    public DecompileMethodToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DecompileMethod_GetGreeting_ReturnsSource()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            null,
            CancellationToken.None);

        result.Should().Contain("GetGreeting");
        result.Should().Contain("Hello");
    }

    [Fact]
    public async Task DecompileMethod_Calculate_ReturnsSource()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "Calculate",
            null,
            CancellationToken.None);

        result.Should().Contain("Calculate");
    }

    [Fact]
    public async Task DecompileMethod_NonExistentMethod_ThrowsMethodNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "NonExistentMethod",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("METHOD_NOT_FOUND");
    }

    [Fact]
    public async Task DecompileMethod_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            "SomeMethod",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task DecompileMethod_AlwaysAppendsTruncationFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            null,
            CancellationToken.None);

        result.Should().Contain("[truncation:");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"totalLines\":");
        result.Should().Contain("\"returnedLines\":");
    }

    [Fact]
    public async Task DecompileMethod_Constructor_ReturnsCode()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            ".ctor",
            null,
            CancellationToken.None);

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("SimpleClass");
    }
}
