using Goose.Core.Models;
using Goose.Tools;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Tools;

/// <summary>
/// Unit tests for FileTool implementation
/// </summary>
public class FileToolTests
{
    private readonly Mock<ILogger<FileTool>> _mockLogger;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly ToolSecurityOptions _defaultSecurityOptions;
    private readonly ToolContext _defaultContext;

    public FileToolTests()
    {
        _mockLogger = new Mock<ILogger<FileTool>>();
        _mockFileSystem = new Mock<IFileSystem>();

        _defaultSecurityOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10_000_000,  // 10MB
                BlockedExtensions = new List<string> { ".exe", ".dll", ".so" },
                EnforceWorkingDirectoryBoundary = false,
                AllowedDirectories = new List<string>()
            }
        };

        _defaultContext = new ToolContext
        {
            WorkingDirectory = Path.GetTempPath(),
            EnvironmentVariables = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void FileTool_HasCorrectMetadata()
    {
        // Arrange
        var tool = CreateFileTool();

        // Assert
        Assert.Equal("read_file", tool.Name);
        Assert.Equal("Read the contents of a file", tool.Description);
        Assert.Contains("\"path\"", tool.ParameterSchema);
        Assert.Contains("\"required\"", tool.ParameterSchema);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPath_ReturnsFileContent()
    {
        // Arrange
        var expectedContent = "Hello, World!";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");

        _mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(tempFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContent);

        var tool = CreateFileTool();
        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, _defaultContext);

            // Assert
            Assert.True(result.Success, result.Error ?? "No error");
            Assert.Equal(expectedContent, result.Output);
            Assert.Null(result.Error);
            Assert.True(result.Duration > TimeSpan.Zero);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithRelativePath_ResolvesCorrectly()
    {
        // Arrange
        var fileName = "relative_test.txt";
        var fullPath = Path.Combine(_defaultContext.WorkingDirectory, fileName);
        var expectedContent = "Relative path test";

        _mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(fullPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContent);

        // Create temp file
        var tempFile = Path.GetTempFileName();
        var tempFileName = Path.GetFileName(tempFile);
        File.WriteAllText(tempFile, "test");

        var tool = CreateFileTool();
        var parameters = $@"{{ ""path"": ""{tempFileName}"" }}";

        try
        {
            // Act - use temp directory as working directory
            var context = new ToolContext
            {
                WorkingDirectory = Path.GetDirectoryName(tempFile)!,
                EnvironmentVariables = new Dictionary<string, string>()
            };
            var result = await tool.ExecuteAsync(parameters, context);

            // Assert
            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var tool = CreateFileTool();
        var nonExistentPath = Path.Combine(_defaultContext.WorkingDirectory, "nonexistent.txt");
        var parameters = $@"{{ ""path"": ""{nonExistentPath.Replace("\\", "\\\\")}"" }}";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPath_ReturnsError()
    {
        // Arrange
        var tool = CreateFileTool();
        var parameters = @"{ ""path"": """" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not provided", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPathParameter_ReturnsError()
    {
        // Arrange
        var tool = CreateFileTool();
        var parameters = @"{ ""otherParam"": ""value"" }";

        // Act
        var result = await tool.ExecuteAsync(parameters, _defaultContext);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithBlockedExtension_ReturnsError()
    {
        // Arrange
        var tool = CreateFileTool();

        // Create a temp .exe file for testing
        var tempExe = Path.GetTempFileName();
        File.Move(tempExe, tempExe + ".exe");
        tempExe = tempExe + ".exe";
        File.WriteAllText(tempExe, "test");

        var parameters = $@"{{ ""path"": ""{tempExe.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, _defaultContext);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempExe);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithFileTooLarge_ReturnsError()
    {
        // Arrange
        var smallSizeOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10,  // Only 10 bytes allowed
                BlockedExtensions = new List<string>(),
                EnforceWorkingDirectoryBoundary = false,
                AllowedDirectories = new List<string>()
            }
        };

        var tool = CreateFileTool(smallSizeOptions);

        // Create a temp file larger than limit
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "This content is definitely larger than 10 bytes!");

        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, _defaultContext);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("size", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exceeds", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OutsideWorkingDirectory_WithBoundaryEnforced_ReturnsError()
    {
        // Arrange
        var strictOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10_000_000,
                BlockedExtensions = new List<string>(),
                EnforceWorkingDirectoryBoundary = true,
                AllowedDirectories = new List<string>()
            }
        };

        var tool = CreateFileTool(strictOptions);

        // Create a file outside the working directory
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test content");

        var outsideWorkingDir = new ToolContext
        {
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "restricted_dir"),
            EnvironmentVariables = new Dictionary<string, string>()
        };

        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, outsideWorkingDir);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("denied", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InAllowedDirectory_WithBoundaryEnforced_Succeeds()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var tempDir = Path.GetDirectoryName(tempFile)!;
        File.WriteAllText(tempFile, "test content");

        var allowedDirOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10_000_000,
                BlockedExtensions = new List<string>(),
                EnforceWorkingDirectoryBoundary = true,
                AllowedDirectories = new List<string> { tempDir }
            }
        };

        _mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(tempFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test content");

        var tool = CreateFileTool(allowedDirOptions);

        var restrictedContext = new ToolContext
        {
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "different_dir"),
            EnvironmentVariables = new Dictionary<string, string>()
        };

        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, restrictedContext);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("test content", result.Output);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithIOException_ReturnsError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");

        _mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(tempFile, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk read error"));

        var tool = CreateFileTool();
        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, _defaultContext);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("Failed to read file", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");

        var tool = CreateFileTool();
        var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

        try
        {
            // Act
            var result = await tool.ExecuteAsync(parameters, _defaultContext, cts.Token);

            // Assert - Tool catches OperationCanceledException and returns error result
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidateAsync_WithValidParameters_ReturnsValid()
    {
        // Arrange
        var tool = CreateFileTool();
        var parameters = @"{ ""path"": ""/some/path.txt"" }";

        // Act
        var result = await tool.ValidateAsync(parameters, _defaultContext);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithMissingPath_ReturnsInvalid()
    {
        // Arrange
        var tool = CreateFileTool();
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
        var tool = CreateFileTool();
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
            new FileTool(null!, _mockFileSystem.Object, Options.Create(_defaultSecurityOptions));
        });
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            new FileTool(_mockLogger.Object, null!, Options.Create(_defaultSecurityOptions));
        });
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        var tool = new FileTool(_mockLogger.Object, _mockFileSystem.Object, null!);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("read_file", tool.Name);
    }

    private FileTool CreateFileTool(ToolSecurityOptions? securityOptions = null)
    {
        return new FileTool(
            _mockLogger.Object,
            _mockFileSystem.Object,
            Options.Create(securityOptions ?? _defaultSecurityOptions));
    }
}
