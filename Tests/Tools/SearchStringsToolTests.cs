using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class SearchStringsToolTests
{
    private readonly ToolTestFixture _fixture;

    public SearchStringsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindsStringByExactPattern()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Hello, World!",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Hello, World!");
        result.Should().Contain("StringContainer");
        result.Should().Contain("GetGreeting");
    }

    [Fact]
    public async Task FindsStringByRegexPattern()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "https?://",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("https://example.com/api");
        result.Should().Contain("GetUrl");
    }

    [Fact]
    public async Task FindsMultipleMatches()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ".*",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("total matches");
        // There should be many strings in the assembly — verify count is > 0
        result.Should().MatchRegex(@"\d+ total matches")
              .And.NotStartWith("String search for '.*': 0 total matches");
    }

    [Fact]
    public async Task ReturnsEmptyForNoMatch()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ZZZZNOTFOUND999",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("0 total matches");
    }

    [Fact]
    public async Task PaginationWorks()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var page1 = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ".*",
            maxResults: 2,
            offset: 0,
            cancellationToken: CancellationToken.None);

        var page2 = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            ".*",
            maxResults: 2,
            offset: 2,
            cancellationToken: CancellationToken.None);

        page1.Should().NotBe(page2);
        page1.Should().Contain("showing 1-2");
        page2.Should().Contain("showing 3-4");
    }

    [Fact]
    public async Task InvalidRegex_ThrowsInvalidPattern()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "[invalid",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PATTERN");
    }

    [Fact]
    public async Task InvalidAssembly_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "test",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    [Fact]
    public async Task SearchStrings_ShowsMethodSignatureWithParameterTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Hello, World!",
            cancellationToken: CancellationToken.None);

        // Should contain full method FQN with parentheses (method signature format)
        result.Should().Contain("StringContainer");
        result.Should().Contain("GetGreeting()");
    }

    [Fact]
    public async Task SearchStrings_ShowsSurroundingILWindow()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "SearchContext:Target",
            cancellationToken: CancellationToken.None);

        // Should contain the match marker and surrounding IL instructions
        result.Should().Contain("<-- match");
        result.Should().MatchRegex(@"IL_[0-9A-Fa-f]{4}");
    }
}
