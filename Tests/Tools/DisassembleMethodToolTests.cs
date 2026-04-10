using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class DisassembleMethodToolTests
{
    private readonly ToolTestFixture _fixture;

    public DisassembleMethodToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DisassembleMethod_GetGreeting_ReturnsFullILBody()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            false,
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        result.Should().Contain(".maxstack");
        result.Should().Contain("IL_");
        result.Should().Contain("ldstr");
        result.Should().Contain("ret");
    }

    [Fact]
    public async Task DisassembleMethod_Calculate_ContainsExpectedOpcodes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "Calculate",
            false,
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("IL_");
        result.Should().Contain("ldarg");
        result.Should().Contain("ret");
        // The if (a < 0 || b < 0) check produces a conditional branch
        result.Should().MatchRegex(@"(blt|bge|clt|brtrue|brfalse)");
    }

    [Fact]
    public async Task DisassembleMethod_Constructor_ReturnsIL()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        // Use Circle which has a single constructor (no overload ambiguity)
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Shapes.Circle",
            ".ctor",
            false,
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        result.Should().Contain(".maxstack");
        result.Should().Contain("IL_");
    }

    [Fact]
    public async Task DisassembleMethod_ShowBytes_AddsHexSequences()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            showBytes: true,
            showTokens: false,
            cancellationToken: CancellationToken.None);

        // showBytes adds hex byte annotations to IL output
        result.Should().MatchRegex(@"[0-9A-Fa-f]{2}");
    }

    [Fact]
    public async Task DisassembleMethod_ShowTokens_AddsTokenNumbers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            showBytes: false,
            showTokens: true,
            cancellationToken: CancellationToken.None);

        // Token format like /* 06000001 */
        result.Should().MatchRegex(@"\/\*\s*[0-9A-Fa-f]+\s*\*\/");
    }

    [Fact]
    public async Task DisassembleMethod_AbstractMethod_NoILBody()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Shapes.Shape",
            "Area",
            false,
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        // Abstract methods have no body - no .maxstack
        result.Should().NotContain(".maxstack");
    }

    [Fact]
    public async Task DisassembleMethod_NonExistentMethod_ThrowsMethodNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "NonExistentMethod",
            false,
            false,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("METHOD_NOT_FOUND");
    }

    [Fact]
    public async Task DisassembleMethod_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            "SomeMethod",
            false,
            false,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task DisassembleMethod_InvalidAssembly_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "SomeType",
            "SomeMethod",
            false,
            false,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    [Fact]
    public async Task DisassembleMethod_AlwaysAppendsTruncationFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            false,
            false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[truncation:");
        result.Should().Contain("\"truncated\":false");
        result.Should().Contain("\"totalLines\":");
        result.Should().Contain("\"returnedLines\":");
    }

    [Fact]
    public async Task DisassembleMethod_ResolveDeep_ExpandsILTypeAbbreviations()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        // GetGreeting uses string concatenation, so IL will have string references
        var resultDeep = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            showBytes: false,
            showTokens: false,
            resolveDeep: true,
            cancellationToken: CancellationToken.None);

        // Deep resolution should expand IL type abbreviations to FQNs
        resultDeep.Should().Contain("System.String");
    }

    [Fact]
    public async Task DisassembleMethod_ResolveDeep_ShowsFullParameterTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        // Calculate has int parameters and throws ArgumentException -- cross-assembly calls
        var resultDeep = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "Calculate",
            showBytes: false,
            showTokens: false,
            resolveDeep: true,
            cancellationToken: CancellationToken.None);

        // Deep resolution should expand 'string' to 'System.String' in operand positions
        resultDeep.Should().Contain("System.String");
    }

    [Fact]
    public async Task DisassembleMethod_DefaultResolveDeep_BackwardCompatible()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<DisassembleMethodTool>();

        // Call with default resolveDeep (false) -- should produce same output as before
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            "GetGreeting",
            showBytes: false,
            showTokens: false,
            cancellationToken: CancellationToken.None);

        result.Should().Contain(".method");
        result.Should().Contain(".maxstack");
        result.Should().Contain("IL_");
        result.Should().Contain("ldstr");
        result.Should().Contain("ret");
    }
}
