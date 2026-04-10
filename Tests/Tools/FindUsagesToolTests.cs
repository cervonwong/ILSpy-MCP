using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindUsagesToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindUsagesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindUsages_MethodCall_ReturnsCallSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindUsagesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            "Save",
            CancellationToken.None);

        result.Should().Contain("Usages of");
        result.Should().Contain("Save");
        result.Should().Contain("DataService");
    }

    [Fact]
    public async Task FindUsages_MethodOnClass_ReturnsCallSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindUsagesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            "Load",
            CancellationToken.None);

        result.Should().Contain("Usages of");
        result.Should().Contain("Load");
        result.Should().Contain("DataService");
    }

    [Fact]
    public async Task FindUsages_NoResults_ReturnsEmptyMessage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindUsagesTool>();

        // CachedFileRepository.Load is declared with 'new', so it's a separate member
        // that nobody calls through CachedFileRepository type
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.CachedFileRepository",
            "Load",
            CancellationToken.None);

        result.Should().Contain("0 found");
    }

    [Fact]
    public async Task FindUsages_NonExistentMember_ThrowsMemberNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindUsagesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            "NonExistentMember",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task FindUsages_InvalidAssembly_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindUsagesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "SomeType",
            "SomeMember",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}
