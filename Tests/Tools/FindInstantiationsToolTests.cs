using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindInstantiationsToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindInstantiationsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindInstantiations_KnownType_ReturnsNewObjSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.FileRepository",
            CancellationToken.None);

        result.Should().Contain("Instantiations of");
        result.Should().Contain("DataService");
        result.Should().Contain("FileProcessor");
    }

    [Fact]
    public async Task FindInstantiations_DatabaseRepository_ReturnsNewObjSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DatabaseRepository",
            CancellationToken.None);

        result.Should().Contain("Instantiations of");
        result.Should().Contain("FileProcessor");
    }

    [Fact]
    public async Task FindInstantiations_NeverInstantiated_ReturnsEmpty()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        // IRepository is an interface, never constructed directly
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            CancellationToken.None);

        result.Should().Contain("0 found");
        result.Should().Contain("No instantiation sites found");
    }

    [Fact]
    public async Task FindInstantiations_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    // ===== Pagination tests (modelled on FindUsagesToolTests) =====

    [Fact]
    public async Task Pagination_FooterPresent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.TrimEnd().Should().EndWith("]");
    }

    [Fact]
    public async Task Pagination_FooterShapeRegex()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
            cancellationToken: CancellationToken.None);

        // Field order is LOCKED by the contract: total, returned, offset, truncated, nextOffset
        result.Should().MatchRegex(
            @"\[pagination:\{""total"":\d+,""returned"":\d+,""offset"":\d+,""truncated"":(true|false),""nextOffset"":(\d+|null)\}\]");
    }

    [Fact]
    public async Task Pagination_FirstPageTruncated()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        // Act — must NOT throw
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    [Fact]
    public async Task FindInstantiations_Enrichment_ShowsFqnMethodSignatureAndIlOffset()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.Instantiations.InstantiationsTarget",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("System.Void");  // FQN return type — proves FormatMethodSignature upgrade is visible
        result.Should().Contain("IL_");          // IL offset prefix
    }
}
