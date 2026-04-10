using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class ExportProjectToolTests
{
    private readonly ToolTestFixture _fixture;

    public ExportProjectToolTests(ToolTestFixture fixture) => _fixture = fixture;

    private string CreateTempDir()
    {
        return Path.Combine(Path.GetTempPath(), "ilspy_mcp_test_" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task ExportsProjectToDirectory()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var scope = _fixture.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

            var result = await tool.ExecuteAsync(
                _fixture.TestAssemblyPath,
                tempDir,
                cancellationToken: CancellationToken.None);

            result.Should().Contain("Project exported to:");
            result.Should().Contain(".csproj");
            result.Should().Contain(".cs");

            // Verify the .csproj file actually exists on disk
            var csprojFiles = Directory.GetFiles(tempDir, "*.csproj", SearchOption.TopDirectoryOnly);
            csprojFiles.Should().NotBeEmpty("a .csproj file should have been created");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreatesDirectoryIfNotExists()
    {
        var tempDir = CreateTempDir();
        try
        {
            Directory.Exists(tempDir).Should().BeFalse("temp dir should not exist yet");

            using var scope = _fixture.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

            var result = await tool.ExecuteAsync(
                _fixture.TestAssemblyPath,
                tempDir,
                cancellationToken: CancellationToken.None);

            Directory.Exists(tempDir).Should().BeTrue("directory should have been auto-created");
            result.Should().Contain("Project exported to:");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FailsOnNonEmptyDirectory()
    {
        var tempDir = CreateTempDir();
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "dummy.txt"), "existing file");

            using var scope = _fixture.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

            var act = () => tool.ExecuteAsync(
                _fixture.TestAssemblyPath,
                tempDir,
                cancellationToken: CancellationToken.None);

            var ex = await act.Should().ThrowAsync<McpToolException>();
            ex.Which.ErrorCode.Should().Be("DIRECTORY_NOT_EMPTY");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReturnsFileListingWithRelativePaths()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var scope = _fixture.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

            var result = await tool.ExecuteAsync(
                _fixture.TestAssemblyPath,
                tempDir,
                cancellationToken: CancellationToken.None);

            // Extract source file lines
            var lines = result.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.EndsWith(".cs"))
                .ToList();

            lines.Should().NotBeEmpty("should list at least one .cs file");

            foreach (var relativePath in lines)
            {
                // Should be relative (not absolute)
                Path.IsPathRooted(relativePath).Should().BeFalse(
                    $"path '{relativePath}' should be relative");

                // File should actually exist
                var fullPath = Path.Combine(tempDir, relativePath);
                File.Exists(fullPath).Should().BeTrue(
                    $"file '{relativePath}' should exist at '{fullPath}'");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task InvalidAssembly_ThrowsError()
    {
        var tempDir = CreateTempDir();
        try
        {
            using var scope = _fixture.CreateScope();
            var tool = scope.ServiceProvider.GetRequiredService<ExportProjectTool>();

            var act = () => tool.ExecuteAsync(
                "/nonexistent/assembly.dll",
                tempDir,
                cancellationToken: CancellationToken.None);

            await act.Should().ThrowAsync<McpToolException>();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
