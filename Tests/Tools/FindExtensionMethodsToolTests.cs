using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindExtensionMethodsToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindExtensionMethodsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindExtensionMethods_StringType_FindsExtensions()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "System.String",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Extension methods for type: System.String");
        result.Should().Contain("Reverse");
        result.Should().Contain("IsPalindrome");
        result.Should().Contain("Truncate");
    }

    [Fact]
    public async Task FindExtensionMethods_TypeWithNoExtensions_ReturnsEmpty()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "System.Int32",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("No extension methods found");
    }

    [Fact]
    public async Task FindExtensionMethods_InvalidAssembly_ThrowsException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll",
            "System.String",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }

    // ===== Pagination tests (modelled on FindUsagesToolTests) =====

    [Fact]
    public async Task Pagination_FooterPresent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.TrimEnd().Should().EndWith("]");
    }

    [Fact]
    public async Task Pagination_FooterShapeRegex()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
            cancellationToken: CancellationToken.None);

        // Field order is LOCKED by the contract: total, returned, offset, truncated, nextOffset
        result.Should().MatchRegex(
            @"\[pagination:\{""total"":\d+,""returned"":\d+,""offset"":\d+,""truncated"":(true|false),""nextOffset"":(\d+|null)\}\]");
    }

    [Fact]
    public async Task Pagination_FirstPageTruncated()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        // Act — must NOT throw
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
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
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods.ExtensionTarget",
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }
}
