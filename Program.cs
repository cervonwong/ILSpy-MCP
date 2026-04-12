using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Services;
using ILSpy.Mcp.Infrastructure.Decompiler;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

if (CommandLineHelp.IsRequested(args))
{
    Console.WriteLine(CommandLineHelp.GetText());
    return 0;
}

// Determine transport mode from args, env, or config (highest priority first)
var transportMode = "stdio"; // default

// Check CLI args first (highest priority)
var transportArgIndex = Array.IndexOf(args, "--transport");
if (transportArgIndex >= 0 && transportArgIndex + 1 < args.Length)
{
    transportMode = args[transportArgIndex + 1].ToLowerInvariant();
}
// Then env var
else if (Environment.GetEnvironmentVariable("ILSPY_TRANSPORT") is string envTransport
         && !string.IsNullOrWhiteSpace(envTransport))
{
    transportMode = envTransport.ToLowerInvariant();
}
// Then appsettings.json
else
{
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();
    var configTransport = config["Transport:Type"];
    if (!string.IsNullOrWhiteSpace(configTransport))
    {
        transportMode = configTransport.ToLowerInvariant();
    }
}

if (transportMode == "http")
{
    // HTTP mode - use WebApplication for ASP.NET Core pipeline
    var builder = WebApplication.CreateBuilder(args);

    // Register switch-mapped command-line provider AFTER CreateBuilder so it
    // overrides env vars and appsettings.json. This gives --host / --port the
    // precedence: CLI > env > appsettings.json > defaults.
    builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
    {
        { "--host", "Transport:Http:Host" },
        { "--port", "Transport:Http:Port" },
    });

    // Configure logging to stderr
    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    // Configure options
    builder.Services.Configure<ILSpyOptions>(
        builder.Configuration.GetSection(ILSpyOptions.SectionName));

    // Configure MCP server with HTTP transport
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    // Register all services
    RegisterServices(builder.Services);

    // Read port/host via the unit-tested helper so the production code path and
    // the tests exercise the exact same resolution logic.
    var (host, port) = HttpBindingResolver.Resolve(builder.Configuration);

    var app = builder.Build();
    app.MapMcp("/mcp");

    Console.Error.WriteLine($"ILSpy MCP server listening on http://{host}:{port}");
    app.Run($"http://{host}:{port}");
    return 0;
}
else
{
    // Reject --host / --port in stdio mode: these flags are HTTP-only and silently
    // accepting them would hide misconfiguration (especially when transport is
    // flipped via env var and the flags no longer apply).
    if (HttpBindingResolver.StdioHasBindingFlags(args))
    {
        var offending = args.Contains("--host") ? "--host" : "--port";
        Console.Error.WriteLine(
            $"Error: {offending} is only valid with --transport http (current transport: stdio).");
        return 2;
    }

    // Stdio mode - use Host.CreateApplicationBuilder (original behavior)
    var builder = Host.CreateApplicationBuilder(args);

    // Configure logging to stderr
    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    // Configure options
    builder.Services.Configure<ILSpyOptions>(
        builder.Configuration.GetSection(ILSpyOptions.SectionName));

    // Configure MCP server with stdio transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    // Register all services
    RegisterServices(builder.Services);

    await builder.Build().RunAsync();
    return 0;
}

// Shared service registration to avoid duplication
static void RegisterServices(IServiceCollection services)
{
    // Options validation
    services.AddSingleton<IValidateOptions<ILSpyOptions>, ILSpyOptionsValidator>();
    services.AddOptions<ILSpyOptions>().ValidateOnStart();

    // Application services
    services.AddSingleton<ITimeoutService, TimeoutService>();
    services.AddSingleton<IConcurrencyLimiter, ConcurrencyLimiter>();

    // Domain services (ports)
    services.AddScoped<IDecompilerService, ILSpyDecompilerService>();
    services.AddScoped<IDisassemblyService, ILSpyDisassemblyService>();
    services.AddScoped<ICrossReferenceService, ILSpyCrossReferenceService>();
    services.AddScoped<IAssemblyInspectionService, ILSpyAssemblyInspectionService>();
    services.AddScoped<ISearchService, ILSpySearchService>();
    services.AddScoped<ICrossAssemblyService, ILSpyCrossAssemblyService>();

    // Application use cases
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
    services.AddScoped<ExportProjectUseCase>();
    services.AddScoped<ListNamespaceTypesUseCase>();

    // MCP tool handlers
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
    services.AddScoped<ExportProjectTool>();
    services.AddScoped<ListNamespaceTypesTool>();
}

public static class HttpBindingResolver
{
    public const string DefaultHost = "0.0.0.0";
    public const int DefaultPort = 3001;

    public static (string Host, int Port) Resolve(IConfiguration configuration)
    {
        var host = configuration.GetValue<string>("Transport:Http:Host");
        if (string.IsNullOrWhiteSpace(host))
        {
            host = DefaultHost;
        }

        var port = configuration.GetValue<int?>("Transport:Http:Port") ?? DefaultPort;

        return (host, port);
    }

    public static bool StdioHasBindingFlags(string[] args)
    {
        return args.Contains("--host") || args.Contains("--port");
    }
}

public static class CommandLineHelp
{
    public static bool IsRequested(string[] args)
    {
        return args.Any(static arg =>
            string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase));
    }

    public static string GetText()
    {
        return """
ILSpy MCP Server

Usage:
  ilspy-mcp [--transport <stdio|http>] [--host <value>] [--port <value>]
  ilspy-mcp help

Options:
  --transport <stdio|http>  Transport mode (default: stdio)
  --host <value>            HTTP bind host (HTTP transport only, default: 0.0.0.0)
  --port <value>            HTTP bind port (HTTP transport only, default: 3001)
  -h, --help                Show this help message and exit
""";
    }
}
