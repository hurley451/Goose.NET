using Goose.Core.Abstractions;
using Goose.Core.Configuration;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Goose.Core.Extensions;

/// <summary>
/// Extension methods for configuring Goose services in dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core Goose services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Goose options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGooseCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<GooseOptions>(configuration.GetSection("Goose"));
        services.Configure<PermissionOptions>(configuration.GetSection("Goose:Permissions"));

        // Register memory cache (required for OptimizedFileSystemSessionManager)
        services.AddMemoryCache();

        // Register telemetry
        services.AddSingleton<ITelemetry, InMemoryTelemetry>();

        // Register permission system services
        services.AddSingleton<IToolClassifier, ToolClassifier>();
        services.AddSingleton<IPermissionInspector, PermissionInspector>();
        services.AddSingleton<IPermissionJudge, PermissionJudge>();
        services.AddSingleton<IPermissionStore, PermissionStore>();
        services.AddSingleton<IPermissionSystem, PermissionSystem>();

        // Register core services
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<ISessionManager, FileSystemSessionManager>();
        services.AddScoped<IConversationAgent, ConversationAgent>();

        return services;
    }

    /// <summary>
    /// Adds Goose core services with options configuration action
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Goose options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGooseCore(
        this IServiceCollection services,
        Action<GooseOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Configure default permission options
        services.Configure<PermissionOptions>(options =>
        {
            // Default permission settings
            options.Mode = Models.Permissions.PermissionMode.SmartApprove;
            options.AutoApproveReadWrite = false;
            options.RememberDecisions = true;
            options.MaxRememberedPermissions = 100;
        });

        // Register memory cache (required for OptimizedFileSystemSessionManager)
        services.AddMemoryCache();

        // Register telemetry
        services.AddSingleton<ITelemetry, InMemoryTelemetry>();

        // Register permission system services
        services.AddSingleton<IToolClassifier, ToolClassifier>();
        services.AddSingleton<IPermissionInspector, PermissionInspector>();
        services.AddSingleton<IPermissionJudge, PermissionJudge>();
        services.AddSingleton<IPermissionStore, PermissionStore>();
        services.AddSingleton<IPermissionSystem, PermissionSystem>();

        // Register core services
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<ISessionManager, FileSystemSessionManager>();
        services.AddScoped<IConversationAgent, ConversationAgent>();

        return services;
    }
}
