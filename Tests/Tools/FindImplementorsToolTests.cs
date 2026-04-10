using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindImplementorsToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindImplementorsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindImplementors_Interface_ReturnsImplementors()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Implementors of");
        result.Should().Contain("FileRepository");
        result.Should().Contain("DatabaseRepository");
    }

    [Fact]
    public async Task FindImplementors_BaseClass_ReturnsDerived()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.FileRepository",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Implementors of");
        result.Should().Contain("CachedFileRepository");
    }

    [Fact]
    public async Task FindImplementors_NoImplementors_ReturnsEmptyMessage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("0 found");
        result.Should().Contain("No implementors found");
    }

    [Fact]
    public async Task FindImplementors_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }
}
