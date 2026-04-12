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
}
