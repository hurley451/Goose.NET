using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Inspects tool calls for security threats and risks
/// </summary>
public interface IPermissionInspector
{
    /// <summary>
    /// Inspects a tool call for potential security issues
    /// </summary>
    /// <param name="toolCall">The tool call to inspect</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Inspection results including detected threats</returns>
    Task<InspectionResult> InspectAsync(
        ToolCall toolCall,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
