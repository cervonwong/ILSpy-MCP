using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindDependenciesToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindDependenciesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    // ---- Pre-existing functional tests ----

    [Fact]
    public async Task FindDependencies_SpecificMethod_ReturnsDeps()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            "ProcessData",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Dependencies of");
        result.Should().Contain("Save");
    }

    [Fact]
    public async Task FindDependencies_TypeLevel_ReturnsAllMethodDeps()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Dependencies of");
        // Should include deps from all methods
        result.Should().Contain("Save");
        result.Should().Contain("Load");
    }

    [Fact]
    public async Task FindDependencies_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            null,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task FindDependencies_NonExistentMember_ThrowsMemberNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            "NonExistentMethod",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    // ---- Pagination tests ----

    private const string DepKitchenSinkType = "ILSpy.Mcp.TestTargets.Pagination.Dependencies.DependencyKitchenSink";

    [Fact]
    public async Task Pagination_FooterPresent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.TrimEnd().Should().EndWith("]");  // footer is the last line
    }

    [Fact]
    public async Task Pagination_FooterShapeRegex()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            cancellationToken: CancellationToken.None);

        // Field order is LOCKED by the contract: total, returned, offset, truncated, nextOffset
        result.Should().MatchRegex(
            @"\[pagination:\{""total"":\d+,""returned"":\d+,""offset"":\d+,""truncated"":(true|false),""nextOffset"":(\d+|null)\}\]");
    }

    [Fact]
    public async Task Pagination_FirstPageTruncated()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            // default maxResults=100, offset=0
            cancellationToken: CancellationToken.None);

        // Must have > 100 total deps so default page is truncated
        result.Should().Contain("\"returned\":100");
        result.Should().Contain("\"offset\":0");
        result.Should().Contain("\"truncated\":true");
        result.Should().Contain("\"nextOffset\":100");
    }

    [Fact]
    public async Task Pagination_FinalPage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            maxResults: 100,
            offset: 100,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"offset\":100");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"nextOffset\":null");
    }

    [Fact]
    public async Task Pagination_OffsetBeyondTotal()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        // Act — must NOT throw
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            maxResults: 100,
            offset: 10000,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"returned\":0");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"nextOffset\":null");
    }

    [Fact]
    public async Task Pagination_CeilingRejected()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
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
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    // ---- OUTPUT-02 enrichment tests ----

    [Fact]
    public async Task FindDependencies_Enrichment_PerLineKindMarker()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            cancellationToken: CancellationToken.None);

        // Assert body contains a [Kind] marker inline, proving per-line markers are emitted
        result.Should().MatchRegex(@"\[(MethodCall|FieldAccess|TypeReference|VirtualCall)\]");
    }

    [Fact]
    public async Task FindDependencies_Enrichment_ShowsDefiningAssembly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[System.");  // defining-assembly bracket
    }

    [Fact]
    public async Task FindDependencies_Enrichment_FlatLayoutNoHeaders()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            DepKitchenSinkType,
            null,
            cancellationToken: CancellationToken.None);

        // The old grouped format had lines like "MethodCall:" standalone. The new flat format must NOT.
        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Should().NotContain("MethodCall:");
        lines.Should().NotContain("FieldAccess:");
        lines.Should().NotContain("TypeReference:");
        lines.Should().NotContain("VirtualCall:");
    }
}
