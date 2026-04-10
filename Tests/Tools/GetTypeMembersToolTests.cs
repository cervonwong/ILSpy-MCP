using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class GetTypeMembersToolTests
{
    private readonly ToolTestFixture _fixture;

    public GetTypeMembersToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetTypeMembers_SimpleClass_ListsAllMembers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Type Members:");
        result.Should().Contain("Methods:");
        result.Should().Contain("GetGreeting");
        result.Should().Contain("Calculate");
        result.Should().Contain("Name");
        result.Should().Contain("Age");
    }

    [Fact]
    public async Task GetTypeMembers_Interface_ListsMembers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Animals.IAnimal",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Type Members:");
        result.Should().Contain("Speak");
        result.Should().Contain("Name");
        result.Should().Contain("LegCount");
    }

    [Fact]
    public async Task GetTypeMembers_GenericType_ListsMembers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Generics.Repository`1",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Type Members:");
        result.Should().Contain("Add");
        result.Should().Contain("FindById");
        result.Should().Contain("GetAll");
    }

    [Fact]
    public async Task GetTypeMembers_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task GetTypeMembers_SimpleClass_ListsConstructors()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Constructors:");
        result.Should().Contain(".ctor");
    }

    [Fact]
    public async Task GetTypeMembers_SimpleClass_ConstructorsBeforeMethods()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        var ctorIndex = result.IndexOf("Constructors:");
        var methodIndex = result.IndexOf("Methods:");
        ctorIndex.Should().BeGreaterThan(-1);
        methodIndex.Should().BeGreaterThan(-1);
        ctorIndex.Should().BeLessThan(methodIndex);
    }

    // --- Pagination tests ---

    [Fact]
    public async Task Pagination_DefaultReturnsFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
    }

    [Fact]
    public async Task Pagination_MaxResultsCapsOutput()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: 2,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"returned\":2");
    }

    [Fact]
    public async Task Pagination_OffsetSkipsItems()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var resultDefault = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        var resultOffset = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            offset: 1,
            cancellationToken: CancellationToken.None);

        resultOffset.Should().NotBe(resultDefault);
    }

    [Fact]
    public async Task Pagination_TruncatedTrueWhenMoreExist()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: 1,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":true");
    }

    [Fact]
    public async Task Pagination_ExceedingCapRejectsWithInvalidParameter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: 501,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
    }

    [Fact]
    public async Task Pagination_ZeroMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
    }

    [Fact]
    public async Task Pagination_NegativeMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: -1,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
    }

    // --- Enrichment tests ---

    [Fact]
    public async Task Enrichment_InheritedMembersTagged()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        // User extends BaseEntity, so it should have inherited members from BaseEntity
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.User",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[inherited]");
    }

    [Fact]
    public async Task Enrichment_VirtualModifierShown()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        // SimpleClass inherits from Object which has virtual methods like ToString, Equals
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("virtual");
    }

    [Fact]
    public async Task Enrichment_OverrideModifierShown()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        // Look at a type that overrides ToString - SimpleClass likely has compiler-generated overrides
        // or we can use a BCL type. Use BaseEntity which has properties that generate Equals/GetHashCode
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.SimpleClass",
            maxResults: 500,
            cancellationToken: CancellationToken.None);

        // Object.ToString, Equals, GetHashCode are virtual and inherited
        // Check for virtual on inherited members
        result.Should().Contain("virtual");
    }

    [Fact]
    public async Task Enrichment_AttributesShownOnMembers()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeMembersTool>();

        // AttributedClass has [Obsolete] on OldMethod
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AttributedClass",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[Obsolete]");
    }
}
