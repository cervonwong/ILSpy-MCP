using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Services;
using ILSpy.Mcp.Infrastructure.Decompiler;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Tests.Fixtures;

public sealed class ToolTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public string TestAssemblyPath { get; }

    public ToolTestFixture()
    {
        TestAssemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "ILSpy.Mcp.TestTargets.dll");

        if (!File.Exists(TestAssemblyPath))
            throw new FileNotFoundException(
                $"TestTargets DLL not found at {TestAssemblyPath}. Ensure the TestTargets project is referenced and built.");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.Configure<ILSpyOptions>(options =>
        {
            options.DefaultTimeoutSeconds = 30;
            options.MaxDecompilationSize = 1_048_576;
            options.MaxConcurrentOperations = 10;
        });

        services.AddSingleton<ITimeoutService, TimeoutService>();
        services.AddScoped<IDecompilerService, ILSpyDecompilerService>();

        services.AddScoped<DecompileTypeUseCase>();
        services.AddScoped<DecompileMethodUseCase>();
        services.AddScoped<ListAssemblyTypesUseCase>();
        services.AddScoped<AnalyzeAssemblyUseCase>();
        services.AddScoped<GetTypeMembersUseCase>();
        services.AddScoped<FindTypeHierarchyUseCase>();
        services.AddScoped<SearchMembersByNameUseCase>();
        services.AddScoped<FindExtensionMethodsUseCase>();

        services.AddScoped<DecompileTypeTool>();
        services.AddScoped<DecompileMethodTool>();
        services.AddScoped<ListAssemblyTypesTool>();
        services.AddScoped<AnalyzeAssemblyTool>();
        services.AddScoped<GetTypeMembersTool>();
        services.AddScoped<FindTypeHierarchyTool>();
        services.AddScoped<SearchMembersByNameTool>();
        services.AddScoped<FindExtensionMethodsTool>();

        ServiceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a new service scope for resolving scoped services.
    /// Each test should create its own scope to avoid cross-test contamination.
    /// </summary>
    public IServiceScope CreateScope() => ServiceProvider.CreateScope();

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}
