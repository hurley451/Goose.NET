using Goose.Core.Configuration;

namespace Goose.Core.Abstractions;

/// <summary>
/// Manages application configuration persistence
/// </summary>
public interface IGooseConfigurationManager
{
    /// <summary>
    /// Loads configuration from the default location
    /// </summary>
    Task<GooseOptions> LoadConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration to the default location
    /// </summary>
    Task SaveConfigurationAsync(GooseOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configuration file path
    /// </summary>
    string GetConfigurationPath();
}
