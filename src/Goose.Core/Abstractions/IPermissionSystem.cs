using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Coordinates the permission system components
/// </summary>
public interface IPermissionSystem
{
    /// <summary>
    /// Requests permission to execute a tool
    /// </summary>
    /// <param name="toolCall">The tool call requesting execution</param>
    /// <param name="riskLevel">The tool's risk level</param>
    /// <param name="context">The execution context</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Permission response indicating whether to allow execution</returns>
    Task<PermissionResponse> RequestPermissionAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        ToolContext context,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tool has been previously approved
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if previously approved, false otherwise</returns>
    Task<bool> IsApprovedAsync(
        string toolName,
        string sessionId,
        CancellationToken cancellationToken = default);
}
