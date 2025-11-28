using System.Diagnostics;
using System.Text;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goose.Tools;

/// <summary>
/// Shell command execution tool for running OS commands in the Goose.NET system
/// </summary>
public class ShellTool : ITool
{
    private readonly ILogger<ShellTool> _logger;
    private readonly ShellSecurityOptions _securityOptions;

    /// <summary>
    /// Creates a new shell tool instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="securityOptions">Security options for shell command execution</param>
    public ShellTool(
        ILogger<ShellTool> logger,
        IOptions<ToolSecurityOptions> securityOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityOptions = securityOptions?.Value?.Shell ?? new ShellSecurityOptions();
    }

    /// <summary>
    /// Gets the name of this tool
    /// </summary>
    public string Name => "run_shell";

    /// <summary>
    /// Gets the description of this tool
    /// </summary>
    public string Description => "Execute shell commands";

    /// <summary>
    /// Gets the parameter schema for this tool
    /// </summary>
    public string ParameterSchema => "{ \"type\": \"object\", \"properties\": { \"command\": { \"type\": \"string\" } }, \"required\": [\"command\"] }";

    /// <summary>
    /// Gets the risk level for this tool (Destructive - executes arbitrary shell commands)
    /// </summary>
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Destructive;

    /// <summary>
    /// Executes a shell command
    /// </summary>
    /// <param name="parameters">Tool parameters as JSON</param>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of tool execution</returns>
    public async Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing shell command: {Command}", parameters);

            // Parse the JSON to get the command parameter
            var json = System.Text.Json.JsonDocument.Parse(parameters);
            var command = json.RootElement.GetProperty("command").GetString();

            if (string.IsNullOrEmpty(command))
            {
                return new ToolResult
                {
                    ToolCallId = "shell-tool-call-id",
                    Success = false,
                    Error = "Command not provided",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Validate command (security)
            var commandName = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (string.IsNullOrEmpty(commandName))
            {
                return new ToolResult
                {
                    ToolCallId = "shell-tool-call-id",
                    Success = false,
                    Error = "Empty command provided",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Check whitelist if enabled
            if (_securityOptions.UseWhitelist &&
                !_securityOptions.AllowedCommands.Contains(commandName))
            {
                return new ToolResult
                {
                    ToolCallId = "shell-tool-call-id",
                    Success = false,
                    Error = $"Command '{commandName}' is not in the allowed commands list",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Execute the command using Process.Start (cross-platform)
            var processInfo = new ProcessStartInfo
            {
                FileName = commandName,
                Arguments = string.Join(" ", command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = context.WorkingDirectory
            };

            using var process = new Process { StartInfo = processInfo };

            // Capture output asynchronously to avoid deadlocks
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(args.Data);
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    lock (errorBuilder)
                    {
                        errorBuilder.AppendLine(args.Data);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with timeout
            var timeout = TimeSpan.FromSeconds(_securityOptions.TimeoutSeconds);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                return new ToolResult
                {
                    ToolCallId = "shell-tool-call-id",
                    Success = false,
                    Error = $"Command timed out after {_securityOptions.TimeoutSeconds} seconds",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            // Check output size limits
            if (output.Length > _securityOptions.MaxOutputSizeBytes)
            {
                output = output.Substring(0, _securityOptions.MaxOutputSizeBytes) +
                         $"\n... (output truncated, exceeded {_securityOptions.MaxOutputSizeBytes} bytes)";
            }

            if (process.ExitCode != 0)
            {
                return new ToolResult
                {
                    ToolCallId = "shell-tool-call-id",
                    Success = false,
                    Error = $"Command failed with exit code {process.ExitCode}: {error}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            return new ToolResult
            {
                ToolCallId = "shell-tool-call-id",
                Success = true,
                Output = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute shell command: {Command}", parameters);
            
            return new ToolResult
            {
                ToolCallId = "shell-tool-call-id",
                Success = false,
                Error = $"Failed to execute shell command: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Validates that the shell tool can execute in the current context
    /// </summary>
    /// <param name="parameters">Tool parameters to validate</param>
    /// <param name="context">Execution context</param>
    /// <returns>Validation result</returns>
    public async Task<ValidationResult> ValidateAsync(
        string parameters,
        ToolContext context)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(parameters);
            
            // Validate required properties
            if (!json.RootElement.TryGetProperty("command", out _))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Command parameter is required"
                };
            }

            return new ValidationResult
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation failed: {ex.Message}"
            };
        }
    }
}
