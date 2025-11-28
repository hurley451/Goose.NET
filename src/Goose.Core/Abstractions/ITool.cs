using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Defines a tool that can be invoked by the AI agent
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique identifier for the tool
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON schema defining the tool's parameters
    /// </summary>
    string ParameterSchema { get; }

    /// <summary>
    /// The risk level of this tool based on its potential impact
    /// </summary>
    ToolRiskLevel RiskLevel { get; }
    
    /// <summary>
    /// Executes the tool with the given parameters
    /// </summary>
    /// <param name="parameters">Tool parameters as JSON</param>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of tool execution</returns>
    Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that the tool can execute in the current context
    /// </summary>
    /// <param name="parameters">Tool parameters to validate</param>
    /// <param name="context">Execution context</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateAsync(
        string parameters,
        ToolContext context);
}
