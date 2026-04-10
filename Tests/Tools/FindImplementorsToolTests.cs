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

    private const string ImplementorsTarget = "ILSpy.Mcp.TestTargets.Pagination.Implementors.IImplementorsTarget";

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

    // ===== Pagination tests (Phase 10 — PAGE-02 contract) =====

    [Fact]
    public async Task Pagination_FooterPresent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.TrimEnd().Should().EndWith("]");
    }

    [Fact]
    public async Task Pagination_FooterShapeRegex()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            cancellationToken: CancellationToken.None);

        // Field order is LOCKED by the contract: total, returned, offset, truncated, nextOffset
        result.Should().MatchRegex(
            @"\[pagination:\{""total"":\d+,""returned"":\d+,""offset"":\d+,""truncated"":(true|false),""nextOffset"":(\d+|null)\}\]");
    }

    [Fact]
    public async Task Pagination_FirstPageTruncated()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            // default maxResults=100, offset=0
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"returned\":100");
        result.Should().Contain("\"offset\":0");
        result.Should().Contain("\"truncated\":true");
        result.Should().Contain("\"nextOffset\":100");
    }

    [Fact]
    public async Task Pagination_FinalPage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            maxResults: 100,
            offset: 100,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"nextOffset\":null");
    }

    [Fact]
    public async Task Pagination_OffsetBeyondTotal()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        // Act - must NOT throw
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
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
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
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
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    // ===== OUTPUT-03 enrichment tests =====

    [Fact]
    public async Task FindImplementors_Enrichment_PerLineDirectTransitiveMarker()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            cancellationToken: CancellationToken.None);

        result.Should().MatchRegex(@"\[(direct|transitive)\]");
    }

    [Fact]
    public async Task FindImplementors_Enrichment_FlatLayoutNoSectionHeaders()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            cancellationToken: CancellationToken.None);

        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Should().NotContain("Direct:");
        lines.Should().NotContain("Indirect:");
    }

    [Fact]
    public async Task FindImplementors_Enrichment_DirectBeforeTransitive()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindImplementorsTool>();

        // Use maxResults large enough to see both groups on one page
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ImplementorsTarget,
            maxResults: 200,
            cancellationToken: CancellationToken.None);

        var directIdx = result.IndexOf("[direct]");
        var transIdx = result.IndexOf("[transitive]");
        directIdx.Should().BeGreaterThan(-1, "fixture has direct implementors");
        transIdx.Should().BeGreaterThan(-1, "fixture has transitive implementors");
        directIdx.Should().BeLessThan(transIdx, "direct implementors must appear before transitive within a page (D-07 sort)");
    }
}
