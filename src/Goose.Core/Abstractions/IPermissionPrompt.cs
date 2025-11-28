using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Interface for prompting users for permission to execute tools
/// </summary>
public interface IPermissionPrompt
{
    /// <summary>
    /// Prompts the user to approve or deny a tool execution
    /// </summary>
    /// <param name="toolCall">The tool call requesting execution</param>
    /// <param name="riskLevel">The risk level of the tool</param>
    /// <param name="inspectionResult">The security inspection result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user's decision (Allow, Deny) and whether to remember it</returns>
    Task<(PermissionDecision Decision, bool RememberDecision)> PromptUserAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        InspectionResult inspectionResult,
        CancellationToken cancellationToken = default);
}
