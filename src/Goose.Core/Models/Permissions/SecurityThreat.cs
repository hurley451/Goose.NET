namespace Goose.Core.Models.Permissions;

/// <summary>
/// Represents a detected security threat in a tool call
/// </summary>
public record SecurityThreat
{
    /// <summary>
    /// The type of threat detected
    /// </summary>
    public required ThreatType Type { get; init; }

    /// <summary>
    /// Severity level of the threat
    /// </summary>
    public required ThreatLevel Level { get; init; }

    /// <summary>
    /// Description of the threat
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The specific pattern or value that triggered the detection
    /// </summary>
    public string? DetectedPattern { get; init; }

    /// <summary>
    /// Recommendation for how to handle this threat
    /// </summary>
    public string? Recommendation { get; init; }
}

/// <summary>
/// Types of security threats that can be detected
/// </summary>
public enum ThreatType
{
    /// <summary>
    /// Malicious or dangerous command detected
    /// </summary>
    MaliciousCommand,

    /// <summary>
    /// Access to sensitive file or directory
    /// </summary>
    SensitiveFileAccess,

    /// <summary>
    /// Potential data exfiltration via network
    /// </summary>
    NetworkExfiltration,

    /// <summary>
    /// Attempt to escalate privileges
    /// </summary>
    PrivilegeEscalation,

    /// <summary>
    /// Code execution vulnerability
    /// </summary>
    CodeExecution,

    /// <summary>
    /// Repeated tool calls (potential infinite loop)
    /// </summary>
    Repetition,

    /// <summary>
    /// System modification attempt
    /// </summary>
    SystemModification
}

/// <summary>
/// Severity levels for security threats
/// </summary>
public enum ThreatLevel
{
    /// <summary>
    /// No threat detected
    /// </summary>
    None = 0,

    /// <summary>
    /// Low severity - informational
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium severity - requires caution
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High severity - potentially dangerous
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical severity - extremely dangerous
    /// </summary>
    Critical = 4
}
