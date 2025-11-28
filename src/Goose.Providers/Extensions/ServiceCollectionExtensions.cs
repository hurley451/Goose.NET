using Goose.Core.Abstractions;
using Goose.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Goose.Providers.Extensions;

/// <summary>
/// Extension methods for configuring AI provider services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Anthropic provider to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing provider settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAnthropicProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var providerConfig = configuration
            .GetSection("Goose:Providers:anthropic")
            .Get<ProviderConfiguration>() ?? new ProviderConfiguration();

        services.AddHttpClient<AnthropicProvider>(client =>
        {
            client.BaseAddress = new Uri(providerConfig.BaseUrl ?? "https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("x-api-key",
                Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? providerConfig.ApiKey ?? "");
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            client.Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds ?? 300);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddScoped<IProvider>(sp => sp.GetRequiredService<AnthropicProvider>());

        return services;
    }

    /// <summary>
    /// Adds the OpenAI provider to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing provider settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var providerConfig = configuration
            .GetSection("Goose:Providers:openai")
            .Get<ProviderConfiguration>() ?? new ProviderConfiguration();

        services.AddHttpClient<OpenAIProvider>(client =>
        {
            client.BaseAddress = new Uri(providerConfig.BaseUrl ?? "https://api.openai.com");
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? providerConfig.ApiKey ?? ""}");
            client.Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds ?? 300);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddScoped<IProvider>(sp => sp.GetRequiredService<OpenAIProvider>());

        return services;
    }

    /// <summary>
    /// Adds a provider based on the configuration's default provider setting
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDefaultProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var defaultProvider = configuration.GetValue<string>("Goose:DefaultProvider") ?? "anthropic";

        return defaultProvider.ToLowerInvariant() switch
        {
            "anthropic" => services.AddAnthropicProvider(configuration),
            "openai" => services.AddOpenAIProvider(configuration),
            _ => throw new InvalidOperationException($"Unknown provider: {defaultProvider}")
        };
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 4,
                durationOfBreak: TimeSpan.FromMinutes(1));
    }
}

/// <summary>
/// Configuration for a provider
/// </summary>
public class ProviderConfiguration
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public int? TimeoutSeconds { get; set; }
}
