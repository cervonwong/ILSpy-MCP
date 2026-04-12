using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class SearchMembersByNameToolTests
{
    private readonly ToolTestFixture _fixture;

    public SearchMembersByNameToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SearchMembers_ByMethodName_FindsMatches()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Calculate",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Search results for 'Calculate'");
        result.Should().Contain("Calculate");
        result.Should().Contain("SimpleClass");
    }

    [Fact]
    public async Task SearchMembers_ByPropertyName_FindsMatches()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Name",
            "property",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Search results for 'Name'");
        result.Should().Contain("Name");
    }

    [Fact]
    public async Task SearchMembers_NoResults_ReturnsZeroCount()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ZzzNonExistentMember",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Found 0 matching members");
    }

    [Fact]
    public async Task SearchMembers_InvalidAssembly_ThrowsException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll",
            "ToString",
            null,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }
}
