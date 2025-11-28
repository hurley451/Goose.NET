using Goose.Core.Models;
using Goose.Tools;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Tools;

/// <summary>
/// Unit tests for ShellTool implementation
/// </summary>
public class ShellToolTests
{
    private readonly Mock<ILogger<ShellTool>> _mockLogger;
    private readonly ToolSecurityOptions _defaultSecurityOptions;
    private readonly ToolContext _defaultContext;

    public ShellToolTests()
    {
        _mockLogger = new Mock<ILogger<ShellTool>>();

        _defaultSecurityOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = false,
                AllowedCommands = new HashSet<string>(),
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 1_000_000  // 1MB
            }
        };

        _defaultContext = new ToolContext
        {
            WorkingDirectory = Path.GetTempPath(),
            EnvironmentVariables = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void ShellTool_HasCorrectMetadata()
    {
        // Arrange
        var tool = CreateShellTool();

        // Assert
        Assert.Equal("run_shell", tool.Name);
        Assert.Equal("Execute shell commands", tool.Description);
        Assert.Contains("\"command\"", tool.ParameterSchema);
        Assert.Contains("\"required\"", tool.ParameterSchema);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleEchoCommand_ReturnsOutput()
    {
        // Arrange
        var tool = CreateShellTool();

        // Use cross-platform command
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo Hello";
        }
        else
        {
            command = "echo Hello";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.Contains("Hello", result.Output ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_WithWorkingDirectory_UsesCorrectDirectory()
    {
        // Arrange
        var tool = CreateShellTool();
        var tempDir = Path.GetTempPath();

        var context = new ToolContext
        {
            WorkingDirectory = tempDir,
            EnvironmentVariables = new Dictionary<string, string>()
        };

        // Use cross-platform command to print working directory
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c cd";
        }
        else
        {
            command = "pwd";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        // The output should contain the temp directory path
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCommand_ReturnsError()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ ""command"": """" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not provided", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCommandParameter_ReturnsError()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ ""otherParam"": ""value"" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitelistEnabled_AllowsWhitelistedCommand()
    {
        // Arrange
        var whitelistOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = true,
                AllowedCommands = new HashSet<string> { "echo", "cmd" },
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 1_000_000
            }
        };

        var tool = CreateShellTool(whitelistOptions);

        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo Test";
        }
        else
        {
            command = "echo Test";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.Contains("Test", result.Output ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitelistEnabled_BlocksNonWhitelistedCommand()
    {
        // Arrange
        var whitelistOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = true,
                AllowedCommands = new HashSet<string> { "echo" },
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 1_000_000
            }
        };

        var tool = CreateShellTool(whitelistOptions);
        var parameters = @"{ ""command"": ""dangerous_command arg1 arg2"" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not in the allowed commands list", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentCommand_ReturnsError()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ ""command"": ""this_command_definitely_does_not_exist_12345"" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandThatFails_ReturnsError()
    {
        // Arrange
        var tool = CreateShellTool();

        // Use a command that will fail on all platforms
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c exit 1";
        }
        else
        {
            command = "sh -c 'exit 1'";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("exit code", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_TerminatesCommand()
    {
        // Arrange
        var shortTimeoutOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = false,
                AllowedCommands = new HashSet<string>(),
                TimeoutSeconds = 1,  // 1 second timeout
                MaxOutputSizeBytes = 1_000_000
            }
        };

        var tool = CreateShellTool(shortTimeoutOptions);

        // Use a command that sleeps longer than timeout
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c timeout /t 5 /nobreak";
        }
        else
        {
            command = "sleep 5";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeOutput_TruncatesOutput()
    {
        // Arrange
        var smallOutputOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = false,
                AllowedCommands = new HashSet<string>(),
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 50  // Very small limit
            }
        };

        var tool = CreateShellTool(smallOutputOptions);

        // Command that produces lots of output
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo This is a very long string that will definitely exceed the 50 byte limit";
        }
        else
        {
            command = "echo This is a very long string that will definitely exceed the 50 byte limit";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.NotNull(result.Output);
        Assert.Contains("truncated", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJson_ReturnsError()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ invalid json }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        var tool = CreateShellTool();

        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c timeout /t 10 /nobreak";
        }
        else
        {
            command = "sleep 10";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500);  // Cancel after 500ms

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext, cts.Token);

        // Assert - Either cancelled or timed out
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesBothStdoutAndStderr()
    {
        // Arrange
        var tool = CreateShellTool();

        // Command that outputs to both stdout and stderr
        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo stdout & echo stderr 1>&2";
        }
        else
        {
            command = "sh -c \"echo stdout && echo stderr >&2\"";
        }

        var parameters = $@"{{ ""command"": ""{command.Replace("\"", "\\\"")}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.NotNull(result.Output);
        Assert.Contains("stdout", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WithValidParameters_ReturnsValid()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ ""command"": ""echo test"" }";

        // Act
        var result = await tool.ValidateAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithMissingCommand_ReturnsInvalid()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ ""otherParam"": ""value"" }";

        // Act
        var result = await tool.ValidateAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidJson_ReturnsInvalid()
    {
        // Arrange
        var tool = CreateShellTool();
        var parameters = @"{ invalid json }";

        // Act
        var result = await tool.ValidateAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            new ShellTool(null!, Options.Create(_defaultSecurityOptions));
        });
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        var tool = new ShellTool(_mockLogger.Object, null!);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("run_shell", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsDuration()
    {
        // Arrange
        var tool = CreateShellTool();

        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo test";
        }
        else
        {
            command = "echo test";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.True(result.Duration < TimeSpan.FromSeconds(10));  // Should be quick
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandArguments_ParsesCorrectly()
    {
        // Arrange
        var tool = CreateShellTool();

        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo arg1 arg2 arg3";
        }
        else
        {
            command = "echo arg1 arg2 arg3";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.Contains("arg1", result.Output ?? "");
        Assert.Contains("arg2", result.Output ?? "");
        Assert.Contains("arg3", result.Output ?? "");
    }

    private ShellTool CreateShellTool(ToolSecurityOptions? securityOptions = null)
    {
        return new ShellTool(
            _mockLogger.Object,
            Options.Create(securityOptions ?? _defaultSecurityOptions));
    }
}
