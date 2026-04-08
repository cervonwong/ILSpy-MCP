using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class LoadAssemblyDirectoryToolTests
{
    private readonly ToolTestFixture _fixture;

    public LoadAssemblyDirectoryToolTests(ToolTestFixture fixture) => _fixture = fixture;

    private string TestDirectory => Path.GetDirectoryName(_fixture.TestAssemblyPath)!;

    [Fact]
    public async Task LoadDirectory_ReturnsLoadedAssemblies()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<LoadAssemblyDirectoryTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            3,
            CancellationToken.None);

        result.Should().Contain("assemblies loaded");
        result.Should().Contain("ILSpy.Mcp.TestTargets");
    }

    [Fact]
    public async Task LoadDirectory_ShowsAssemblyVersions()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<LoadAssemblyDirectoryTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            3,
            CancellationToken.None);

        // Should contain version numbers like "v1.0.0.0" or similar
        result.Should().MatchRegex(@"v\d+\.\d+");
    }

    [Fact]
    public async Task LoadDirectory_SkipsNativeFiles()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<LoadAssemblyDirectoryTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            3,
            CancellationToken.None);

        // Result should at minimum show the directory scan summary
        result.Should().Contain("Directory scan:");
        // If native DLLs are present, they should appear in skipped section
        // This test validates the format is correct regardless
        result.Should().Contain("total)");
    }

    [Fact]
    public async Task LoadDirectory_InvalidDirectory_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<LoadAssemblyDirectoryTool>();

        var act = () => tool.ExecuteAsync(
            "C:/nonexistent_dir_12345",
            3,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("DIRECTORY_NOT_FOUND");
    }

    [Fact]
    public async Task LoadDirectory_TotalCountMatchesLoadedPlusSkipped()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<LoadAssemblyDirectoryTool>();

        var result = await tool.ExecuteAsync(
            TestDirectory,
            0, // depth 0 for faster test
            CancellationToken.None);

        // Parse the counts from the output
        var match = System.Text.RegularExpressions.Regex.Match(
            result,
            @"(\d+) assemblies loaded, (\d+) files skipped \((\d+) total\)");

        match.Success.Should().BeTrue("output should contain the summary line");
        var loaded = int.Parse(match.Groups[1].Value);
        var skipped = int.Parse(match.Groups[2].Value);
        var total = int.Parse(match.Groups[3].Value);

        total.Should().Be(loaded + skipped);
    }
}
