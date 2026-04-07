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
using ModelContextProtocol.Server;

// Determine transport mode from args, env, or config
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

if (transportMode == "http")
{
    // HTTP mode - use WebApplication for ASP.NET Core pipeline
    var builder = WebApplication.CreateBuilder(args);

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

    // Read port/host from config
    var port = builder.Configuration.GetValue<int>("Transport:Http:Port", 3001);
    var host = builder.Configuration.GetValue<string>("Transport:Http:Host") ?? "0.0.0.0";

    var app = builder.Build();
    app.MapMcp();

    Console.Error.WriteLine($"ILSpy MCP server listening on http://{host}:{port}");
    app.Run($"http://{host}:{port}");
}
else
{
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
}

// Shared service registration to avoid duplication
static void RegisterServices(IServiceCollection services)
{
    // Application services
    services.AddSingleton<ITimeoutService, TimeoutService>();
    services.AddSingleton<IConcurrencyLimiter, ConcurrencyLimiter>();

    // Domain services (ports)
    services.AddScoped<IDecompilerService, ILSpyDecompilerService>();

    // Application use cases
    services.AddScoped<DecompileTypeUseCase>();
    services.AddScoped<DecompileMethodUseCase>();
    services.AddScoped<ListAssemblyTypesUseCase>();
    services.AddScoped<AnalyzeAssemblyUseCase>();
    services.AddScoped<GetTypeMembersUseCase>();
    services.AddScoped<FindTypeHierarchyUseCase>();
    services.AddScoped<SearchMembersByNameUseCase>();
    services.AddScoped<FindExtensionMethodsUseCase>();

    // MCP tool handlers
    services.AddScoped<DecompileTypeTool>();
    services.AddScoped<DecompileMethodTool>();
    services.AddScoped<ListAssemblyTypesTool>();
    services.AddScoped<AnalyzeAssemblyTool>();
    services.AddScoped<GetTypeMembersTool>();
    services.AddScoped<FindTypeHierarchyTool>();
    services.AddScoped<SearchMembersByNameTool>();
    services.AddScoped<FindExtensionMethodsTool>();
}
