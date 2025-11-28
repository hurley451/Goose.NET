using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Goose.Core.Services;

/// <summary>
/// Inspects tool calls for security threats and risks
/// </summary>
public class PermissionInspector : IPermissionInspector
{
    private readonly ILogger<PermissionInspector> _logger;
    private readonly HashSet<string> _recentCommands = new();
    private readonly object _lock = new();

    // Threat detection patterns
    private static readonly Dictionary<ThreatType, List<string>> ThreatPatterns = new()
    {
        {
            ThreatType.MaliciousCommand, new List<string>
            {
                "rm -rf /", "format c:", "del /f /s /q", "dd if=/dev/zero",
                "mkfs", ":(){ :|:& };:", "chmod -R 777 /", "chown -R",
                "> /dev/sda", "wget.*| sh", "curl.*| bash", "eval(", "exec(",
                "system(", "popen(", "shell_exec(", "passthru("
            }
        },
        {
            ThreatType.SensitiveFileAccess, new List<string>
            {
                "/etc/passwd", "/etc/shadow", "/etc/sudoers", "~/.ssh/",
                "~/.aws/credentials", "~/.config/gcloud/", "/root/",
                "c:\\windows\\system32\\", "%systemroot%", "c:\\users\\.*\\ntuser",
                ".pem$", ".key$", ".p12$", ".pfx$", "private.*key",
                "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"
            }
        },
        {
            ThreatType.NetworkExfiltration, new List<string>
            {
                "curl.*http", "wget.*http", "nc.*-e", "netcat.*-e",
                "python.*socket", "perl.*socket", "| nc ", "| ncat ",
                "base64.*|.*curl", "tar.*|.*ssh", "scp ", "rsync.*ssh",
                "ftp ", "sftp ", "http.*POST.*data"
            }
        },
        {
            ThreatType.PrivilegeEscalation, new List<string>
            {
                "sudo ", "su -", "runas", "gsudo", "doas ",
                "pkexec", "setuid", "setgid", "chmod.*4[0-7][0-7][0-7]",
                "chmod.*u\\+s", "chmod.*g\\+s", "/etc/sudoers",
                "visudo", "usermod.*-g.*root"
            }
        },
        {
            ThreatType.CodeExecution, new List<string>
            {
                "eval\\(", "exec\\(", "compile\\(", "__import__",
                "importlib", "pickle.loads", "marshal.loads",
                "os.system", "subprocess.call", "subprocess.Popen",
                "Runtime.getRuntime", "ProcessBuilder", "ScriptEngine",
                "javascript:", "data:text/html", "vbscript:", "file:///"
            }
        },
        {
            ThreatType.SystemModification, new List<string>
            {
                "/etc/hosts", "/etc/resolv.conf", "/etc/crontab",
                "registry add", "reg add", "regedit", "bcdedit",
                "diskpart", "fdisk", "parted", "gpt", "mbr",
                "systemctl disable", "chkconfig", "launchctl",
                "/library/launchdaemons", "/library/startupitems"
            }
        }
    };

    // Sensitive paths that should trigger warnings
    private static readonly List<string> SensitivePaths = new()
    {
        "/etc/", "/sys/", "/proc/", "/dev/", "/boot/",
        "/var/log/", "/usr/bin/", "/usr/sbin/", "/bin/", "/sbin/",
        "c:\\windows\\", "c:\\program files\\", "%programfiles%",
        "%systemroot%", "%windir%", "/system/", "/library/",
        "~/.ssh/", "~/.gnupg/", "~/.aws/", "~/.azure/"
    };

    /// <summary>
    /// Creates a new permission inspector instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public PermissionInspector(ILogger<PermissionInspector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Inspects a tool call for potential security issues
    /// </summary>
    /// <param name="toolCall">The tool call to inspect</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Inspection results including detected threats</returns>
    public Task<InspectionResult> InspectAsync(
        ToolCall toolCall,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var threats = new List<SecurityThreat>();

        // Check for command repetition (potential loop)
        if (DetectRepetition(toolCall))
        {
            threats.Add(new SecurityThreat
            {
                Type = ThreatType.Repetition,
                Level = ThreatLevel.Medium,
                Description = "Repeated command execution detected - potential infinite loop",
                DetectedPattern = toolCall.Name,
                Recommendation = "Review the command execution pattern to ensure it's intentional"
            });
        }

        // Analyze parameters for threat patterns
        if (!string.IsNullOrEmpty(toolCall.Parameters))
        {
            var parameterThreats = AnalyzeParameters(toolCall.Parameters, toolCall.Name);
            threats.AddRange(parameterThreats);
        }

        // Check for sensitive path access in file operations
        if (IsFileOperation(toolCall.Name) && !string.IsNullOrEmpty(toolCall.Parameters))
        {
            var pathThreats = AnalyzeFilePaths(toolCall.Parameters);
            threats.AddRange(pathThreats);
        }

        // Determine overall safety
        var result = threats.Any()
            ? InspectionResult.Unsafe(threats, $"Detected {threats.Count} potential security threat(s)")
            : InspectionResult.Safe();

        if (!result.IsSafe)
        {
            _logger.LogWarning(
                "Security inspection failed for tool '{ToolName}': {ThreatCount} threats detected (Max level: {MaxLevel})",
                toolCall.Name,
                threats.Count,
                result.ThreatLevel);

            foreach (var threat in threats)
            {
                _logger.LogWarning(
                    "  - {ThreatType} ({ThreatLevel}): {Description}",
                    threat.Type,
                    threat.Level,
                    threat.Description);
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Detects if a command is being repeated suspiciously
    /// </summary>
    /// <param name="toolCall">The tool call</param>
    /// <returns>True if repetition detected</returns>
    private bool DetectRepetition(ToolCall toolCall)
    {
        var commandSignature = $"{toolCall.Name}:{toolCall.Parameters}";

        lock (_lock)
        {
            if (_recentCommands.Contains(commandSignature))
            {
                return true;
            }

            _recentCommands.Add(commandSignature);

            // Keep only last 100 commands to prevent memory bloat
            if (_recentCommands.Count > 100)
            {
                _recentCommands.Remove(_recentCommands.First());
            }
        }

        return false;
    }

    /// <summary>
    /// Analyzes parameters for threat patterns
    /// </summary>
    /// <param name="parameters">Parameters to analyze</param>
    /// <param name="toolName">Name of the tool</param>
    /// <returns>List of detected threats</returns>
    private List<SecurityThreat> AnalyzeParameters(string parameters, string toolName)
    {
        var threats = new List<SecurityThreat>();
        var lowerParams = parameters.ToLowerInvariant();

        foreach (var (threatType, patterns) in ThreatPatterns)
        {
            foreach (var pattern in patterns)
            {
                try
                {
                    if (Regex.IsMatch(lowerParams, pattern, RegexOptions.IgnoreCase))
                    {
                        var level = CalculateThreatLevel(threatType, pattern, lowerParams);

                        threats.Add(new SecurityThreat
                        {
                            Type = threatType,
                            Level = level,
                            Description = GetThreatDescription(threatType, pattern),
                            DetectedPattern = pattern,
                            Recommendation = GetThreatRecommendation(threatType)
                        });

                        _logger.LogDebug(
                            "Detected {ThreatType} threat in tool '{ToolName}' matching pattern '{Pattern}'",
                            threatType,
                            toolName,
                            pattern);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.LogWarning(
                        "Regex timeout while checking pattern '{Pattern}' in tool '{ToolName}'",
                        pattern,
                        toolName);
                }
            }
        }

        return threats;
    }

    /// <summary>
    /// Analyzes file paths for sensitive locations
    /// </summary>
    /// <param name="parameters">Parameters containing file paths</param>
    /// <returns>List of detected threats</returns>
    private List<SecurityThreat> AnalyzeFilePaths(string parameters)
    {
        var threats = new List<SecurityThreat>();
        var lowerParams = parameters.ToLowerInvariant();

        foreach (var sensitivePath in SensitivePaths)
        {
            if (lowerParams.Contains(sensitivePath.ToLowerInvariant()))
            {
                threats.Add(new SecurityThreat
                {
                    Type = ThreatType.SensitiveFileAccess,
                    Level = ThreatLevel.High,
                    Description = $"Access to sensitive system path: {sensitivePath}",
                    DetectedPattern = sensitivePath,
                    Recommendation = "Ensure this access is authorized and necessary"
                });
            }
        }

        return threats;
    }

    /// <summary>
    /// Calculates the threat level based on context
    /// </summary>
    /// <param name="threatType">Type of threat</param>
    /// <param name="pattern">Matched pattern</param>
    /// <param name="parameters">Full parameters</param>
    /// <returns>Calculated threat level</returns>
    private static ThreatLevel CalculateThreatLevel(ThreatType threatType, string pattern, string parameters)
    {
        // Base threat level by type
        var baseLevel = threatType switch
        {
            ThreatType.MaliciousCommand => ThreatLevel.Critical,
            ThreatType.PrivilegeEscalation => ThreatLevel.Critical,
            ThreatType.SensitiveFileAccess => ThreatLevel.High,
            ThreatType.NetworkExfiltration => ThreatLevel.High,
            ThreatType.CodeExecution => ThreatLevel.High,
            ThreatType.SystemModification => ThreatLevel.High,
            ThreatType.Repetition => ThreatLevel.Medium,
            _ => ThreatLevel.Low
        };

        // Escalate if multiple red flags
        if (pattern.Contains("rm -rf") || pattern.Contains("format") || pattern.Contains("/dev/zero"))
        {
            baseLevel = ThreatLevel.Critical;
        }

        return baseLevel;
    }

    /// <summary>
    /// Gets a human-readable description for a threat
    /// </summary>
    /// <param name="threatType">Type of threat</param>
    /// <param name="pattern">Matched pattern</param>
    /// <returns>Description string</returns>
    private static string GetThreatDescription(ThreatType threatType, string pattern)
    {
        return threatType switch
        {
            ThreatType.MaliciousCommand => $"Potentially destructive command detected: {pattern}",
            ThreatType.SensitiveFileAccess => $"Access to sensitive files/directories: {pattern}",
            ThreatType.NetworkExfiltration => $"Potential data exfiltration detected: {pattern}",
            ThreatType.PrivilegeEscalation => $"Privilege escalation attempt detected: {pattern}",
            ThreatType.CodeExecution => $"Arbitrary code execution detected: {pattern}",
            ThreatType.SystemModification => $"System modification detected: {pattern}",
            ThreatType.Repetition => "Repeated command execution",
            _ => $"Security threat detected: {pattern}"
        };
    }

    /// <summary>
    /// Gets a recommendation for handling a threat
    /// </summary>
    /// <param name="threatType">Type of threat</param>
    /// <returns>Recommendation string</returns>
    private static string GetThreatRecommendation(ThreatType threatType)
    {
        return threatType switch
        {
            ThreatType.MaliciousCommand => "Carefully review this command before execution. Consider denying if unintentional.",
            ThreatType.SensitiveFileAccess => "Verify that accessing these files is necessary and authorized.",
            ThreatType.NetworkExfiltration => "Ensure data transmission is intentional and to a trusted destination.",
            ThreatType.PrivilegeEscalation => "Verify that elevated privileges are necessary for this operation.",
            ThreatType.CodeExecution => "Review the code being executed for malicious content.",
            ThreatType.SystemModification => "Ensure system modifications are intentional and reversible.",
            ThreatType.Repetition => "Check for infinite loops or unintended command repetition.",
            _ => "Review this operation carefully before proceeding."
        };
    }

    /// <summary>
    /// Checks if a tool is a file operation
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <returns>True if file operation</returns>
    private static bool IsFileOperation(string toolName)
    {
        return toolName.Contains("file", StringComparison.OrdinalIgnoreCase) ||
               toolName.Contains("read", StringComparison.OrdinalIgnoreCase) ||
               toolName.Contains("write", StringComparison.OrdinalIgnoreCase) ||
               toolName.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
               toolName.Contains("path", StringComparison.OrdinalIgnoreCase);
    }
}
