namespace Goose.Core.Models.Permissions;

/// <summary>
/// Represents a request for permission to execute a tool
/// </summary>
public record PermissionRequest
{
    /// <summary>
    /// The tool call requesting permission
    /// </summary>
    public required ToolCall ToolCall { get; init; }

    /// <summary>
    /// The tool's risk level
    /// </summary>
    public required ToolRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Security inspection results
    /// </summary>
    public required InspectionResult InspectionResult { get; init; }

    /// <summary>
    /// The session ID this request is for
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// When the request was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
