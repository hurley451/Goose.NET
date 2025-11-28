using System.Runtime.InteropServices;

namespace Goose.Tools.Configuration;

/// <summary>
/// Security configuration options for tools
/// </summary>
public class ToolSecurityOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Goose:Tools:Security";

    /// <summary>
    /// File security options
    /// </summary>
    public FileSecurityOptions File { get; set; } = new();

    /// <summary>
    /// Shell security options
    /// </summary>
    public ShellSecurityOptions Shell { get; set; } = new();
}

/// <summary>
/// Security options for file operations
/// </summary>
public class FileSecurityOptions
{
    /// <summary>
    /// Whether to enforce working directory boundary
    /// </summary>
    public bool EnforceWorkingDirectoryBoundary { get; set; } = true;

    /// <summary>
    /// Additional allowed directories outside working directory
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = new();

    /// <summary>
    /// Blocked file extensions for security
    /// </summary>
    public List<string> BlockedExtensions { get; set; } = new() { ".exe", ".dll", ".so", ".dylib" };

    /// <summary>
    /// Maximum file size to read in bytes (default 10MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>
/// Security options for shell command execution
/// </summary>
public class ShellSecurityOptions
{
    /// <summary>
    /// Whether to use command whitelist (if false, all commands allowed)
    /// </summary>
    public bool UseWhitelist { get; set; } = true;

    /// <summary>
    /// Allowed shell commands
    /// </summary>
    public HashSet<string> AllowedCommands { get; set; } = GetDefaultAllowedCommands();

    /// <summary>
    /// Maximum execution timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum output size in bytes
    /// </summary>
    public int MaxOutputSizeBytes { get; set; } = 1024 * 1024; // 1MB

    private static HashSet<string> GetDefaultAllowedCommands()
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            commands.UnionWith(new[] { "dir", "type", "echo", "cd", "where", "findstr" });
        }
        else
        {
            commands.UnionWith(new[] { "ls", "cat", "echo", "pwd", "grep", "find", "which" });
        }

        return commands;
    }
}
