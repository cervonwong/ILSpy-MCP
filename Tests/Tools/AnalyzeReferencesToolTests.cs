using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class AnalyzeReferencesToolTests
{
    private readonly ToolTestFixture _fixture;

    public AnalyzeReferencesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnalyzeReferences_Implementors_RoutesCorrectly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeReferencesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "implementors",
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            null,
            CancellationToken.None);

        result.Should().Contain("Implementors of");
        result.Should().Contain("FileRepository");
        result.Should().Contain("DatabaseRepository");
    }

    [Fact]
    public async Task AnalyzeReferences_Usages_RoutesCorrectly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeReferencesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "usages",
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            "Save",
            CancellationToken.None);

        result.Should().Contain("Usages of");
        result.Should().Contain("DataService");
    }

    [Fact]
    public async Task AnalyzeReferences_Dependencies_RoutesCorrectly()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeReferencesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "dependencies",
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            "ProcessData",
            CancellationToken.None);

        result.Should().Contain("Dependencies of");
        result.Should().Contain("Save");
    }

    [Fact]
    public async Task AnalyzeReferences_InvalidType_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeReferencesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "invalid_type",
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_ANALYSIS_TYPE");
    }

    [Fact]
    public async Task AnalyzeReferences_UsagesWithoutMemberName_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeReferencesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "usages",
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
    }
}
