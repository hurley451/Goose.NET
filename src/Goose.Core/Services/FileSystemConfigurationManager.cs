using Goose.Core.Abstractions;
using Goose.Core.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Goose.Core.Services;

/// <summary>
/// File system based configuration manager
/// </summary>
public class FileSystemConfigurationManager : IGooseConfigurationManager
{
    private readonly ILogger<FileSystemConfigurationManager> _logger;
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public FileSystemConfigurationManager(ILogger<FileSystemConfigurationManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get config directory (default to ~/.goose)
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".goose");

        // Ensure directory exists
        Directory.CreateDirectory(configDir);

        _configFilePath = Path.Combine(configDir, "config.json");
    }

    public async Task<GooseOptions> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Configuration file not found at {ConfigPath}, returning default configuration", _configFilePath);
                return new GooseOptions();
            }

            _logger.LogInformation("Loading configuration from {ConfigPath}", _configFilePath);

            var json = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
            var options = JsonSerializer.Deserialize<GooseOptions>(json, _jsonOptions);

            if (options == null)
            {
                _logger.LogWarning("Failed to deserialize configuration, returning default configuration");
                return new GooseOptions();
            }

            _logger.LogInformation("Successfully loaded configuration");
            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from {ConfigPath}", _configFilePath);
            return new GooseOptions();
        }
    }

    public async Task SaveConfigurationAsync(GooseOptions options, CancellationToken cancellationToken = default)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        try
        {
            _logger.LogInformation("Saving configuration to {ConfigPath}", _configFilePath);

            var json = JsonSerializer.Serialize(options, _jsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);

            _logger.LogInformation("Successfully saved configuration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to {ConfigPath}", _configFilePath);
            throw;
        }
    }

    public string GetConfigurationPath()
    {
        return _configFilePath;
    }
}
