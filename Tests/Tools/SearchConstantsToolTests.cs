using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class SearchConstantsToolTests
{
    private readonly ToolTestFixture _fixture;

    public SearchConstantsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindsExactConstant()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            42,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("42");
        result.Should().Contain("ConstantContainer");
        result.Should().Contain("GetMagicNumber");
    }

    [Fact]
    public async Task FindsNegativeConstant()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            -1,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("-1");
        result.Should().Contain("GetNegative");
    }

    [Fact]
    public async Task FindsLargeConstant()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            1234567890,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("1234567890");
        result.Should().Contain("GetBigNumber");
    }

    [Fact]
    public async Task FindsZero()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            0,
            cancellationToken: CancellationToken.None);

        // Zero is used in many places across the assembly — verify count is > 0
        result.Should().Contain("total matches")
              .And.NotStartWith("Constant search for 0: 0 total matches");
    }

    [Fact]
    public async Task ReturnsEmptyForUnusedValue()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            999999999,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("0 total matches");
    }

    [Fact]
    public async Task InvalidAssembly_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            42,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    [Fact]
    public async Task SearchConstants_ShowsMethodSignatureWithFullTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchConstantsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            42,
            cancellationToken: CancellationToken.None);

        // Should contain full method FQN with parentheses (method signature format)
        result.Should().Contain("ConstantContainer");
        result.Should().Contain("GetMagicNumber()");
    }
}
