using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Services;

/// <summary>
/// Null implementation of permission prompt that automatically allows all requests
/// Used for CLI scenarios or when no UI is available
/// </summary>
public class NullPermissionPrompt : IPermissionPrompt
{
    /// <summary>
    /// Gets the singleton instance
    /// </summary>
    public static readonly NullPermissionPrompt Instance = new();

    private NullPermissionPrompt()
    {
    }

    /// <summary>
    /// Automatically allows the request without prompting
    /// </summary>
    public Task<(PermissionDecision Decision, bool RememberDecision)> PromptUserAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        InspectionResult inspectionResult,
        CancellationToken cancellationToken = default)
    {
        // Automatically allow without prompting
        return Task.FromResult((PermissionDecision.Allow, false));
    }
}
