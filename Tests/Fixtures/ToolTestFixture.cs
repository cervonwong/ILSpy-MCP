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
        services.AddSingleton<IConcurrencyLimiter, ConcurrencyLimiter>();
        services.AddScoped<IDecompilerService, ILSpyDecompilerService>();
        services.AddScoped<IDisassemblyService, ILSpyDisassemblyService>();
        services.AddScoped<ICrossReferenceService, ILSpyCrossReferenceService>();
        services.AddScoped<IAssemblyInspectionService, ILSpyAssemblyInspectionService>();
        services.AddScoped<ISearchService, ILSpySearchService>();
        services.AddScoped<ICrossAssemblyService, ILSpyCrossAssemblyService>();

        services.AddScoped<DecompileTypeUseCase>();
        services.AddScoped<DecompileMethodUseCase>();
        services.AddScoped<DisassembleTypeUseCase>();
        services.AddScoped<DisassembleMethodUseCase>();
        services.AddScoped<ListAssemblyTypesUseCase>();
        services.AddScoped<AnalyzeAssemblyUseCase>();
        services.AddScoped<GetTypeMembersUseCase>();
        services.AddScoped<FindTypeHierarchyUseCase>();
        services.AddScoped<SearchMembersByNameUseCase>();
        services.AddScoped<FindExtensionMethodsUseCase>();
        services.AddScoped<FindUsagesUseCase>();
        services.AddScoped<FindImplementorsUseCase>();
        services.AddScoped<FindDependenciesUseCase>();
        services.AddScoped<FindInstantiationsUseCase>();
        services.AddScoped<GetAssemblyMetadataUseCase>();
        services.AddScoped<GetAssemblyAttributesUseCase>();
        services.AddScoped<GetTypeAttributesUseCase>();
        services.AddScoped<GetMemberAttributesUseCase>();
        services.AddScoped<ListEmbeddedResourcesUseCase>();
        services.AddScoped<ExtractResourceUseCase>();
        services.AddScoped<FindCompilerGeneratedTypesUseCase>();
        services.AddScoped<SearchStringsUseCase>();
        services.AddScoped<SearchConstantsUseCase>();
        services.AddScoped<ResolveTypeUseCase>();
        services.AddScoped<LoadAssemblyDirectoryUseCase>();

        services.AddScoped<DecompileTypeTool>();
        services.AddScoped<DecompileMethodTool>();
        services.AddScoped<DisassembleTypeTool>();
        services.AddScoped<DisassembleMethodTool>();
        services.AddScoped<ListAssemblyTypesTool>();
        services.AddScoped<AnalyzeAssemblyTool>();
        services.AddScoped<GetTypeMembersTool>();
        services.AddScoped<FindTypeHierarchyTool>();
        services.AddScoped<SearchMembersByNameTool>();
        services.AddScoped<FindExtensionMethodsTool>();
        services.AddScoped<FindUsagesTool>();
        services.AddScoped<FindImplementorsTool>();
        services.AddScoped<FindDependenciesTool>();
        services.AddScoped<FindInstantiationsTool>();
        services.AddScoped<AnalyzeReferencesTool>();
        services.AddScoped<GetAssemblyMetadataTool>();
        services.AddScoped<GetAssemblyAttributesTool>();
        services.AddScoped<GetTypeAttributesTool>();
        services.AddScoped<GetMemberAttributesTool>();
        services.AddScoped<ListEmbeddedResourcesTool>();
        services.AddScoped<ExtractResourceTool>();
        services.AddScoped<FindCompilerGeneratedTypesTool>();
        services.AddScoped<SearchStringsTool>();
        services.AddScoped<SearchConstantsTool>();
        services.AddScoped<ResolveTypeTool>();
        services.AddScoped<LoadAssemblyDirectoryTool>();

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
