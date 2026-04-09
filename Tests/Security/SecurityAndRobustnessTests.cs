using FluentAssertions;
using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Services;
using ILSpy.Mcp.Infrastructure.Decompiler;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ILSpy.Mcp.Tests.Security;

[Collection("ToolTests")]
public class SecurityAndRobustnessTests
{
    private readonly ToolTestFixture _fixture;

    public SecurityAndRobustnessTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReDoSPattern_DoesNotHang()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchStringsTool>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // The call should either complete quickly (regex timeout fires at ~1s)
        // or throw McpToolException with INVALID_PATTERN. It must NOT hang.
        Func<Task> act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "(a+)+b",
            cancellationToken: cts.Token);

        // Accept either successful completion or an expected error
        try
        {
            await act();
            // If it completes without throwing, that's fine — pattern matched nothing or completed fast
        }
        catch (McpToolException ex) when (ex.ErrorCode is "INVALID_PATTERN" or "TIMEOUT" or "INTERNAL_ERROR")
        {
            // Expected — regex timeout or pattern rejection
        }

        // If we reach here within 10 seconds, the test passes (no hang)
        cts.IsCancellationRequested.Should().BeFalse(
            "the operation should complete well before the 10-second safety net");
    }

    [Fact]
    public async Task ExtractResource_NegativeOffset_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.txt",
            offset: -1,
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }

    [Fact]
    public async Task ExtractResource_ZeroLimit_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.txt",
            limit: 0,
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }

    [Fact]
    public async Task ExtractResource_NegativeLimit_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.txt",
            limit: -5,
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }

    [Fact]
    public async Task ExportProject_EmptyOutputDirectory_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "",
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }

    [Fact]
    public async Task ExportProject_WhitespaceOutputDirectory_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "   ",
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }

    [Fact]
    public async Task DecompileType_SmallMaxSize_TruncatesOutput()
    {
        // Build a custom ServiceProvider with MaxDecompilationSize = 100 bytes
        using var customProvider = BuildProviderWithSmallMaxSize(100);
        using var scope = customProvider.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DecompileTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[Output truncated at 100 bytes");
    }

    /// <summary>
    /// Builds a minimal ServiceProvider with the same registrations as ToolTestFixture
    /// but with a custom MaxDecompilationSize for testing truncation.
    /// </summary>
    private ServiceProvider BuildProviderWithSmallMaxSize(int maxDecompilationSize)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.Configure<ILSpyOptions>(options =>
        {
            options.DefaultTimeoutSeconds = 30;
            options.MaxDecompilationSize = maxDecompilationSize;
            options.MaxConcurrentOperations = 10;
        });

        services.AddSingleton<ITimeoutService, TimeoutService>();
        services.AddSingleton<IConcurrencyLimiter, ConcurrencyLimiter>();
        services.AddScoped<IDecompilerService, ILSpyDecompilerService>();
        services.AddScoped<DecompileTypeUseCase>();
        services.AddScoped<DecompileTypeTool>();

        return services.BuildServiceProvider();
    }
}
