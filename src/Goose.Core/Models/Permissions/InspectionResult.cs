namespace Goose.Core.Models.Permissions;

/// <summary>
/// Result of inspecting a tool call for security threats
/// </summary>
public record InspectionResult
{
    /// <summary>
    /// Whether the tool call is considered safe
    /// </summary>
    public required bool IsSafe { get; init; }

    /// <summary>
    /// Overall threat level detected
    /// </summary>
    public required ThreatLevel ThreatLevel { get; init; }

    /// <summary>
    /// List of detected threats
    /// </summary>
    public required IReadOnlyList<SecurityThreat> Threats { get; init; }

    /// <summary>
    /// Additional context or information about the inspection
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Creates a safe inspection result with no threats
    /// </summary>
    public static InspectionResult Safe() => new()
    {
        IsSafe = true,
        ThreatLevel = ThreatLevel.None,
        Threats = Array.Empty<SecurityThreat>()
    };

    /// <summary>
    /// Creates an unsafe inspection result with the given threats
    /// </summary>
    public static InspectionResult Unsafe(IReadOnlyList<SecurityThreat> threats, string? context = null)
    {
        var maxThreatLevel = threats.Any()
            ? threats.Max(t => t.Level)
            : ThreatLevel.None;

        return new InspectionResult
        {
            IsSafe = maxThreatLevel == ThreatLevel.None,
            ThreatLevel = maxThreatLevel,
            Threats = threats,
            Context = context
        };
    }
}
