using Goose.Core.Abstractions;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Goose.Tools.Extensions;

/// <summary>
/// Extension methods for configuring Goose tools
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all default Goose tools to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing tool security settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGooseTools(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure security options
        services.Configure<ToolSecurityOptions>(
            configuration.GetSection(ToolSecurityOptions.SectionName));

        // Register file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();

        // Register tools
        services.AddSingleton<ITool, FileTool>();
        services.AddSingleton<ITool, ShellTool>();

        return services;
    }

    /// <summary>
    /// Adds all default Goose tools with custom security options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure tool security options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGooseTools(
        this IServiceCollection services,
        Action<ToolSecurityOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<ITool, FileTool>();
        services.AddSingleton<ITool, ShellTool>();

        return services;
    }

    /// <summary>
    /// Adds the file tool to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFileTool(this IServiceCollection services)
    {
        services.AddSingleton<ITool, FileTool>();
        return services;
    }

    /// <summary>
    /// Adds the shell tool to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddShellTool(this IServiceCollection services)
    {
        services.AddSingleton<ITool, ShellTool>();
        return services;
    }
}
