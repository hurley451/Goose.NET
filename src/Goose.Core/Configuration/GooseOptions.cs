using Goose.Core.Models;

namespace Goose.Core.Configuration;

/// <summary>
/// Root configuration options for the Goose application
/// </summary>
public class GooseOptions
{
    /// <summary>
    /// The default AI provider to use
    /// </summary>
    public string DefaultProvider { get; set; } = "anthropic";
    
    /// <summary>
    /// Directory where session files are stored
    /// </summary>
    public string SessionDirectory { get; set; } = "~/.goose/sessions";
    
    /// <summary>
    /// Maximum tokens to generate in a single response
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
    
    /// <summary>
    /// Temperature setting for response generation (0.0-2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>
    /// Configuration for various providers
    /// </summary>
    public Dictionary<string, ProviderOptions> Providers { get; set; } = new();
}
