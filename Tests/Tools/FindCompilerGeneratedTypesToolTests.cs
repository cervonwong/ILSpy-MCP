using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindCompilerGeneratedTypesToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindCompilerGeneratedTypesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_FindsAsyncStateMachine()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        // Async state machines contain "d__" in their name
        result.Should().MatchRegex(@"d__\d+");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_FindsDisplayClass()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("DisplayClass");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_ShowsParentMethod()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("DoWorkAsync");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_ShowsParentType()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        // Should show AsyncExample or LambdaExample as parent type
        result.Should().Match(r => r.Contains("AsyncExample") || r.Contains("LambdaExample"));
    }

    [Fact]
    public async Task FindCompilerGenerated_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    // ===== Pagination tests (modelled on FindUsagesToolTests) =====

    [Fact]
    public async Task Pagination_FooterPresent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.TrimEnd().Should().EndWith("]");
    }

    [Fact]
    public async Task Pagination_FooterShapeRegex()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        // Field order is LOCKED by the contract: total, returned, offset, truncated, nextOffset
        result.Should().MatchRegex(
            @"\[pagination:\{""total"":\d+,""returned"":\d+,""offset"":\d+,""truncated"":(true|false),""nextOffset"":(\d+|null)\}\]");
    }

    [Fact]
    public async Task Pagination_FirstPageTruncated()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            // default maxResults=100, offset=0
            cancellationToken: CancellationToken.None);

        // Total = Phase 7 fixture CG types + Pagination.CompilerGenerated's 105 async state machines
        // Don't hard-code total, but first page must be truncated with 100 returned
        result.Should().Contain("\"returned\":100");
        result.Should().Contain("\"offset\":0");
        result.Should().Contain("\"truncated\":true");
        result.Should().Contain("\"nextOffset\":100");
    }

    [Fact]
    public async Task Pagination_FinalPage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        // Offset past the first page — whatever remains should be the final page
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            maxResults: 500,
            offset: 100,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"nextOffset\":null");
    }

    [Fact]
    public async Task Pagination_OffsetBeyondTotal()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        // Act — must NOT throw
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            maxResults: 100,
            offset: 10000,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"returned\":0");
        result.Should().Contain("\"offset\":10000");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"nextOffset\":null");
    }

    [Fact]
    public async Task Pagination_CeilingRejected()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            maxResults: 501,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Pagination_ZeroMaxResultsRejected()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }
}
