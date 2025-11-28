using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Microsoft.Extensions.Logging;

namespace Goose.Core.Services;

/// <summary>
/// Analyzes tool capabilities and classifies their risk levels
/// </summary>
public class ToolClassifier : IToolClassifier
{
    private readonly ILogger<ToolClassifier> _logger;

    /// <summary>
    /// Creates a new tool classifier instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public ToolClassifier(ILogger<ToolClassifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes a tool call and determines its effective risk level
    /// </summary>
    /// <param name="toolCall">The tool call to analyze</param>
    /// <param name="tool">The tool being invoked</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The classified risk level for this specific invocation</returns>
    public Task<ToolRiskLevel> ClassifyAsync(
        ToolCall toolCall,
        ITool tool,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        // Start with the tool's declared risk level
        var baseRiskLevel = tool.RiskLevel;

        // Analyze the specific invocation to potentially elevate risk level
        var effectiveRiskLevel = AnalyzeInvocation(toolCall, tool, context, baseRiskLevel);

        _logger.LogDebug(
            "Classified tool '{ToolName}' as {RiskLevel} (base: {BaseRiskLevel})",
            tool.Name,
            effectiveRiskLevel,
            baseRiskLevel);

        return Task.FromResult(effectiveRiskLevel);
    }

    /// <summary>
    /// Analyzes a specific tool invocation to determine if risk should be elevated
    /// </summary>
    /// <param name="toolCall">The tool call</param>
    /// <param name="tool">The tool</param>
    /// <param name="context">The context</param>
    /// <param name="baseRiskLevel">The tool's base risk level</param>
    /// <returns>The effective risk level for this invocation</returns>
    private ToolRiskLevel AnalyzeInvocation(
        ToolCall toolCall,
        ITool tool,
        ToolContext context,
        ToolRiskLevel baseRiskLevel)
    {
        var riskLevel = baseRiskLevel;

        // Analyze parameters for risk escalation patterns
        if (!string.IsNullOrEmpty(toolCall.Parameters))
        {
            var parameters = toolCall.Parameters.ToLowerInvariant();

            // Check for potentially dangerous patterns
            if (ContainsDangerousPatterns(parameters))
            {
                riskLevel = ElevateRiskLevel(riskLevel);
                _logger.LogWarning(
                    "Elevated risk level for tool '{ToolName}' due to dangerous patterns in parameters",
                    tool.Name);
            }

            // Check for system file access
            if (tool.Name.Contains("file", StringComparison.OrdinalIgnoreCase) &&
                ContainsSystemFilePath(parameters))
            {
                riskLevel = ElevateRiskLevel(riskLevel);
                _logger.LogWarning(
                    "Elevated risk level for tool '{ToolName}' due to system file access",
                    tool.Name);
            }

            // Check for privileged operations
            if (ContainsPrivilegedOperations(parameters))
            {
                riskLevel = ElevateRiskLevel(riskLevel);
                _logger.LogWarning(
                    "Elevated risk level for tool '{ToolName}' due to privileged operations",
                    tool.Name);
            }
        }

        return riskLevel;
    }

    /// <summary>
    /// Elevates a risk level to the next tier
    /// </summary>
    /// <param name="current">Current risk level</param>
    /// <returns>Elevated risk level</returns>
    private static ToolRiskLevel ElevateRiskLevel(ToolRiskLevel current)
    {
        return current switch
        {
            ToolRiskLevel.ReadOnly => ToolRiskLevel.ReadWrite,
            ToolRiskLevel.ReadWrite => ToolRiskLevel.Destructive,
            ToolRiskLevel.Destructive => ToolRiskLevel.Critical,
            ToolRiskLevel.Critical => ToolRiskLevel.Critical, // Already at max
            _ => current
        };
    }

    /// <summary>
    /// Checks if parameters contain dangerous patterns
    /// </summary>
    /// <param name="parameters">Parameters to check</param>
    /// <returns>True if dangerous patterns detected</returns>
    private static bool ContainsDangerousPatterns(string parameters)
    {
        // Check for common malicious patterns
        string[] dangerousPatterns =
        {
            "rm -rf",
            "format",
            "del /f",
            "drop table",
            "truncate",
            "curl | sh",
            "wget | sh",
            "eval(",
            "exec(",
            "base64 -d",
            "; cat",
            "| nc",
            "/etc/passwd",
            "/etc/shadow"
        };

        return dangerousPatterns.Any(pattern =>
            parameters.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if parameters contain system file paths
    /// </summary>
    /// <param name="parameters">Parameters to check</param>
    /// <returns>True if system file paths detected</returns>
    private static bool ContainsSystemFilePath(string parameters)
    {
        string[] systemPaths =
        {
            "/etc/",
            "/sys/",
            "/proc/",
            "/dev/",
            "/boot/",
            "c:\\windows\\",
            "c:\\program files\\",
            "%systemroot%",
            "%windir%",
            "/library/",
            "/system/"
        };

        return systemPaths.Any(path =>
            parameters.Contains(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if parameters contain privileged operations
    /// </summary>
    /// <param name="parameters">Parameters to check</param>
    /// <returns>True if privileged operations detected</returns>
    private static bool ContainsPrivilegedOperations(string parameters)
    {
        string[] privilegedKeywords =
        {
            "sudo",
            "su -",
            "chmod 777",
            "chown root",
            "admin",
            "administrator",
            "runas",
            "elevation",
            "root@"
        };

        return privilegedKeywords.Any(keyword =>
            parameters.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
