using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class NativeDllGuardTests
{
    private readonly ToolTestFixture _fixture;

    // kernel32.dll is a native (non-.NET) PE file present on all Windows installations
    private const string NativeDllPath = @"C:\Windows\System32\kernel32.dll";

    public NativeDllGuardTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnalyzeAssembly_NativeDll_ThrowsWithNotDotNetMessage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<AnalyzeAssemblyTool>();

        var act = () => tool.ExecuteAsync(NativeDllPath, null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("ASSEMBLY_LOAD_FAILED");
        ex.Which.Message.Should().Contain("not a .NET assembly");
    }

    [Fact]
    public async Task DecompileType_NativeDll_ThrowsWithNotDotNetMessage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var act = () => tool.ExecuteAsync(
            NativeDllPath, "SomeType", null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("ASSEMBLY_LOAD_FAILED");
        ex.Which.Message.Should().Contain("not a .NET assembly");
    }

    [Fact]
    public async Task DisassembleType_NativeDll_ThrowsWithNotDotNetMessage()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var act = () => tool.ExecuteAsync(
            NativeDllPath, "SomeType", false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("ASSEMBLY_LOAD_FAILED");
        ex.Which.Message.Should().Contain("not a .NET assembly");
    }
}
