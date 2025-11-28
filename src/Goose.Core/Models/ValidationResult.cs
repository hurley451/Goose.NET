namespace Goose.Core.Models;

/// <summary>
/// Represents the result of validating a tool execution
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public required bool IsValid { get; init; }
    
    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// List of validation errors if any
    /// </summary>
    public IReadOnlyList<string>? Errors { get; init; }
}
