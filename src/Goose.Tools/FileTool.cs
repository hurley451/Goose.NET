using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goose.Tools;

/// <summary>
/// File system tool for reading files in the Goose.NET system
/// </summary>
public class FileTool : ITool
{
    private readonly ILogger<FileTool> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly FileSecurityOptions _securityOptions;

    /// <summary>
    /// Creates a new file tool instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="fileSystem">FileSystem abstraction for file operations</param>
    /// <param name="securityOptions">Security options for file operations</param>
    public FileTool(
        ILogger<FileTool> logger,
        IFileSystem fileSystem,
        IOptions<ToolSecurityOptions> securityOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _securityOptions = securityOptions?.Value?.File ?? new FileSecurityOptions();
    }

    /// <summary>
    /// Gets the name of this tool
    /// </summary>
    public string Name => "read_file";

    /// <summary>
    /// Gets the description of this tool
    /// </summary>
    public string Description => "Read the contents of a file";

    /// <summary>
    /// Gets the parameter schema for this tool
    /// </summary>
    public string ParameterSchema => "{ \"type\": \"object\", \"properties\": { \"path\": { \"type\": \"string\" } }, \"required\": [\"path\"] }";

    /// <summary>
    /// Gets the risk level for this tool (ReadOnly - only reads files without modifications)
    /// </summary>
    public ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    /// <summary>
    /// Executes the file reading operation
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
            _logger.LogInformation("Reading file: {Path}", parameters);

            // Parse the JSON to get the path parameter
            var json = System.Text.Json.JsonDocument.Parse(parameters);
            var path = json.RootElement.GetProperty("path").GetString();

            if (string.IsNullOrEmpty(path))
            {
                return new ToolResult
                {
                    ToolCallId = "file-tool-call-id",
                    Success = false,
                    Error = "File path not provided",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Validate the file path (security)
            var fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(context.WorkingDirectory, path));

            // Security validation
            var validationError = ValidateFilePath(fullPath, context.WorkingDirectory);
            if (validationError != null)
            {
                return new ToolResult
                {
                    ToolCallId = "file-tool-call-id",
                    Success = false,
                    Error = validationError,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Check file size before reading
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > _securityOptions.MaxFileSizeBytes)
            {
                return new ToolResult
                {
                    ToolCallId = "file-tool-call-id",
                    Success = false,
                    Error = $"File size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_securityOptions.MaxFileSizeBytes} bytes)",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Read the file content
            var content = await _fileSystem.ReadAllTextAsync(fullPath, cancellationToken);

            return new ToolResult
            {
                ToolCallId = "file-tool-call-id",
                Success = true,
                Output = content,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file: {Path}", parameters);
            
            return new ToolResult
            {
                ToolCallId = "file-tool-call-id",
                Success = false,
                Error = $"Failed to read file: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Validates that the file tool can execute in the current context
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
            if (!json.RootElement.TryGetProperty("path", out _))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Path parameter is required"
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

    /// <summary>
    /// Validates file path against security policies
    /// </summary>
    /// <param name="fullPath">Full path to validate</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>Error message if validation fails, null otherwise</returns>
    private string? ValidateFilePath(string fullPath, string workingDirectory)
    {
        // Normalize paths for comparison
        var normalizedPath = Path.GetFullPath(fullPath);
        var normalizedWorkingDir = Path.GetFullPath(workingDirectory);

        // Check if file exists
        if (!File.Exists(normalizedPath))
        {
            return $"File not found: {fullPath}";
        }

        // Check for blocked extensions
        var extension = Path.GetExtension(normalizedPath);
        if (_securityOptions.BlockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return $"File extension '{extension}' is blocked for security reasons";
        }

        // Check working directory boundary if enforced
        if (_securityOptions.EnforceWorkingDirectoryBoundary)
        {
            var isInWorkingDirectory = normalizedPath.StartsWith(
                normalizedWorkingDir + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) || normalizedPath == normalizedWorkingDir;

            // Check if path is in allowed directories
            var isInAllowedDirectory = _securityOptions.AllowedDirectories.Any(allowedDir =>
            {
                var normalizedAllowedDir = Path.GetFullPath(allowedDir);
                return normalizedPath.StartsWith(
                    normalizedAllowedDir + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) || normalizedPath == normalizedAllowedDir;
            });

            if (!isInWorkingDirectory && !isInAllowedDirectory)
            {
                return $"Access denied: File '{fullPath}' is outside allowed directories";
            }
        }

        return null;
    }
}

/// <summary>
/// File system abstraction interface for better testability
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Reads all text from a file asynchronously
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
}
