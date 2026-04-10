using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class DisassembleTypeToolTests
{
    private readonly ToolTestFixture _fixture;

    public DisassembleTypeToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DisassembleType_SimpleClass_ReturnsMethodSignatures()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        result.Should().Contain("GetGreeting");
        result.Should().Contain("Calculate");
        // D-02: type-level disassembly should NOT contain IL bodies
        result.Should().NotContain(".maxstack");
        result.Should().NotContain("IL_0");
    }

    [Fact]
    public async Task DisassembleType_SimpleClass_ContainsSummaryHeader()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("// Type: ILSpy.Mcp.TestTargets.SimpleClass");
        result.Should().Contain("// Assembly:");
        result.Should().Contain("// Methods:");
    }

    [Fact]
    public async Task DisassembleType_SimpleClass_ContainsFieldDeclarations()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".field");
        result.Should().Contain("_id");
    }

    [Fact]
    public async Task DisassembleType_ShowTokens_AddsTokenNumbers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            showTokens: true,
            cancellationToken: CancellationToken.None);

        // Token format like /* 06000001 */
        result.Should().MatchRegex(@"\/\*\s*[0-9A-Fa-f]+\s*\*\/");
    }

    [Fact]
    public async Task DisassembleType_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            false,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task DisassembleType_InvalidAssemblyPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "SomeType",
            false,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    [Fact]
    public async Task DisassembleType_Interface_ReturnsMethodSignatures()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Animals.IAnimal",
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        // Interface methods have no body
        result.Should().NotContain(".maxstack");
    }

    [Fact]
    public async Task DisassembleType_AlwaysAppendsTruncationFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[truncation:");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"totalLines\":");
        result.Should().Contain("\"returnedLines\":");
    }

    [Fact]
    public async Task DisassembleType_ResolveDeep_ExpandsFieldTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        // SimpleClass has string and int32 fields -- resolveDeep should expand them
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            showTokens: false,
            resolveDeep: true,
            cancellationToken: CancellationToken.None);

        // Deep resolution should expand IL type abbreviations in field/method signatures
        result.Should().Contain("System.String");
    }

    [Fact]
    public async Task DisassembleType_DefaultResolveDeep_BackwardCompatible()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleTypeTool>();

        // Call with default resolveDeep (false) -- existing behavior preserved
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            showTokens: false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        result.Should().Contain("GetGreeting");
        result.Should().Contain("Calculate");
        result.Should().NotContain(".maxstack");
    }
}
