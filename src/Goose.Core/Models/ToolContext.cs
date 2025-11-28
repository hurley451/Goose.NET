namespace Goose.Core.Models;

/// <summary>
/// Context information for executing a tool
/// </summary>
public class ToolContext
{
    /// <summary>
    /// The working directory where the tool should operate
    /// </summary>
    public required string WorkingDirectory { get; init; }
    
    /// <summary>
    /// Environment variables available to the tool
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// Additional metadata for the tool execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();
}
