using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using Goose.Tools;
using Goose.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Integration;

/// <summary>
/// Integration tests for tool execution with real tool registry
/// </summary>
public class ToolExecutionIntegrationTests
{
    [Fact]
    public void ToolRegistry_WithMultipleTools_RetrievesCorrectTool()
    {
        // Arrange
        var tool1 = CreateMockTool("read_file", "Read a file");
        var tool2 = CreateMockTool("write_file", "Write a file");
        var tool3 = CreateMockTool("shell_command", "Run shell command");

        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool1.Object, tool2.Object, tool3.Object }, mockLogger.Object);

        // Act
        var result = registry.GetTool("write_file");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("write_file", result.Name);
        Assert.Equal("Write a file", result.Description);
    }

    [Fact]
    public void ToolRegistry_GetNonExistentTool_ReturnsNull()
    {
        // Arrange
        var tool = CreateMockTool("existing_tool", "An existing tool");
        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool.Object }, mockLogger.Object);

        // Act
        var result = registry.GetTool("nonexistent_tool");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToolRegistry_TryGetTool_ReturnsCorrectResult()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test tool");
        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool.Object }, mockLogger.Object);

        // Act
        var found = registry.TryGetTool("test_tool", out var retrievedTool);

        // Assert
        Assert.True(found);
        Assert.NotNull(retrievedTool);
        Assert.Equal("test_tool", retrievedTool.Name);
    }

    [Fact]
    public void ToolRegistry_TryGetNonExistentTool_ReturnsFalse()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test tool");
        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool.Object }, mockLogger.Object);

        // Act
        var found = registry.TryGetTool("missing_tool", out var retrievedTool);

        // Assert
        Assert.False(found);
        Assert.Null(retrievedTool);
    }

    [Fact]
    public async Task ToolExecution_WithComplexParameters_ExecutesCorrectly()
    {
        // Arrange
        var capturedParams = string.Empty;
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("complex_tool");
        mockTool.Setup(t => t.Description).Returns("Tool with complex params");
        mockTool.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, ToolContext, CancellationToken>((p, c, ct) => capturedParams = p)
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "test",
                Success = true,
                Output = "Executed with complex params",
                Duration = TimeSpan.FromMilliseconds(50)
            });

        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { mockTool.Object }, mockLogger.Object);
        var context = new ToolContext
        {
            WorkingDirectory = Environment.CurrentDirectory,
            EnvironmentVariables = new Dictionary<string, string>()
        };

        var complexParams = @"{
            ""path"": ""/home/user/file.txt"",
            ""options"": {
                ""encoding"": ""utf-8"",
                ""maxSize"": 1024
            },
            ""tags"": [""important"", ""review""]
        }";

        // Act
        var tool = registry.GetTool("complex_tool");
        Assert.NotNull(tool);
        var result = await tool.ExecuteAsync(complexParams, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Contains("complex params", result.Output);

        // Verify the parameters were passed correctly
        Assert.Contains("/home/user/file.txt", capturedParams);
        Assert.Contains("utf-8", capturedParams);
    }

    [Fact]
    public async Task ToolExecution_WithEnvironmentVariables_PassesContextCorrectly()
    {
        // Arrange
        ToolContext? capturedContext = null;
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("env_tool");
        mockTool.Setup(t => t.Description).Returns("Tool using environment");
        mockTool.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, ToolContext, CancellationToken>((p, c, ct) => capturedContext = c)
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "test",
                Success = true,
                Output = "Environment context received",
                Duration = TimeSpan.FromMilliseconds(30)
            });

        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { mockTool.Object }, mockLogger.Object);
        var context = new ToolContext
        {
            WorkingDirectory = "/test/directory",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["API_KEY"] = "test-key-123",
                ["DEBUG"] = "true"
            }
        };

        // Act
        var tool = registry.GetTool("env_tool");
        Assert.NotNull(tool);
        var result = await tool.ExecuteAsync("{}", context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedContext);
        Assert.Equal("/test/directory", capturedContext.WorkingDirectory);
        Assert.Equal("test-key-123", capturedContext.EnvironmentVariables["API_KEY"]);
        Assert.Equal("true", capturedContext.EnvironmentVariables["DEBUG"]);
    }

    [Fact]
    public async Task ToolExecution_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("slow_tool");
        mockTool.Setup(t => t.Description).Returns("A slow tool");
        mockTool.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .Returns(async (string p, ToolContext c, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct); // 5 second delay
                return new ToolResult
                {
                    ToolCallId = "test",
                    Success = true,
                    Output = "Completed",
                    Duration = TimeSpan.FromSeconds(5)
                };
            });

        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { mockTool.Object }, mockLogger.Object);
        var context = new ToolContext
        {
            WorkingDirectory = Environment.CurrentDirectory,
            EnvironmentVariables = new Dictionary<string, string>()
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act & Assert
        var tool = registry.GetTool("slow_tool");
        Assert.NotNull(tool);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await tool.ExecuteAsync("{}", context, cts.Token);
        });
    }

    [Fact]
    public void ToolRegistry_GetAllTools_ReturnsAllRegisteredTools()
    {
        // Arrange
        var tool1 = CreateMockTool("tool_1", "First tool");
        var tool2 = CreateMockTool("tool_2", "Second tool");
        var tool3 = CreateMockTool("tool_3", "Third tool");

        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool1.Object, tool2.Object, tool3.Object }, mockLogger.Object);

        // Act
        var allTools = registry.GetAllTools();

        // Assert
        Assert.NotNull(allTools);
        Assert.Equal(3, allTools.Count);
        Assert.Contains(allTools, t => t.Name == "tool_1");
        Assert.Contains(allTools, t => t.Name == "tool_2");
        Assert.Contains(allTools, t => t.Name == "tool_3");
        Assert.All(allTools, t => Assert.NotNull(t.Description));
        Assert.All(allTools, t => Assert.NotNull(t.ParameterSchema));
    }

    [Fact]
    public void ToolRegistry_RegisterAdditionalTool_AddsToRegistry()
    {
        // Arrange
        var tool1 = CreateMockTool("tool_1", "First tool");
        var mockLogger = new Mock<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(new[] { tool1.Object }, mockLogger.Object);

        var tool2 = CreateMockTool("tool_2", "Second tool");

        // Act
        registry.Register(tool2.Object);
        var allTools = registry.GetAllTools();

        // Assert
        Assert.Equal(2, allTools.Count);
        Assert.Contains(allTools, t => t.Name == "tool_1");
        Assert.Contains(allTools, t => t.Name == "tool_2");
    }

    private Mock<ITool> CreateMockTool(string name, string description)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.ParameterSchema).Returns("{ \"type\": \"object\" }");
        mock.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = $"{name}_call",
                Success = true,
                Output = $"Output from {name}",
                Duration = TimeSpan.FromMilliseconds(50)
            });
        return mock;
    }

    #region Real Tool Integration Tests

    [Fact]
    public async Task RealFileTool_ReadAndExecute_Succeeds()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileTool>>();
        var mockFileSystem = new Mock<IFileSystem>();
        var mockRegistryLogger = new Mock<ILogger<ToolRegistry>>();

        var securityOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10_000_000,
                BlockedExtensions = new List<string> { ".exe" },
                EnforceWorkingDirectoryBoundary = false,
                AllowedDirectories = new List<string>()
            }
        };

        var fileTool = new FileTool(
            mockLogger.Object,
            mockFileSystem.Object,
            Options.Create(securityOptions));

        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), mockRegistryLogger.Object);
        registry.Register(fileTool);

        // Create real temp file
        var tempFile = Path.GetTempFileName();
        var testContent = "Integration test content";
        File.WriteAllText(tempFile, testContent);

        mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testContent);

        try
        {
            var context = new ToolContext
            {
                WorkingDirectory = Path.GetDirectoryName(tempFile)!,
                EnvironmentVariables = new Dictionary<string, string>()
            };

            var parameters = $@"{{ ""path"": ""{tempFile.Replace("\\", "\\\\")}"" }}";

            // Act
            var tool = registry.GetTool("read_file");
            Assert.NotNull(tool);
            var result = await tool.ExecuteAsync(parameters, context);

            // Assert
            Assert.True(result.Success, result.Error ?? "No error");
            Assert.Equal(testContent, result.Output);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RealShellTool_ExecuteCrossPlatformCommand_Succeeds()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ShellTool>>();
        var mockRegistryLogger = new Mock<ILogger<ToolRegistry>>();

        var securityOptions = new ToolSecurityOptions
        {
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = false,
                AllowedCommands = new HashSet<string>(),
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 1_000_000
            }
        };

        var shellTool = new ShellTool(mockLogger.Object, Options.Create(securityOptions));
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), mockRegistryLogger.Object);
        registry.Register(shellTool);

        var context = new ToolContext
        {
            WorkingDirectory = Environment.CurrentDirectory,
            EnvironmentVariables = new Dictionary<string, string>()
        };

        string command;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd /c echo Test Output";
        }
        else
        {
            command = "echo Test Output";
        }

        var parameters = $@"{{ ""command"": ""{command}"" }}";

        // Act
        var tool = registry.GetTool("run_shell");
        Assert.NotNull(tool);
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.True(result.Success, result.Error ?? "No error");
        Assert.Contains("Test Output", result.Output ?? "");
    }

    [Fact]
    public async Task MultipleRealTools_RegisteredAndExecuted_WorkCorrectly()
    {
        // Arrange
        var fileLogger = new Mock<ILogger<FileTool>>();
        var shellLogger = new Mock<ILogger<ShellTool>>();
        var registryLogger = new Mock<ILogger<ToolRegistry>>();
        var mockFileSystem = new Mock<IFileSystem>();

        var securityOptions = new ToolSecurityOptions
        {
            File = new FileSecurityOptions
            {
                MaxFileSizeBytes = 10_000_000,
                BlockedExtensions = new List<string>(),
                EnforceWorkingDirectoryBoundary = false,
                AllowedDirectories = new List<string>()
            },
            Shell = new ShellSecurityOptions
            {
                UseWhitelist = false,
                AllowedCommands = new HashSet<string>(),
                TimeoutSeconds = 30,
                MaxOutputSizeBytes = 1_000_000
            }
        };

        var fileTool = new FileTool(fileLogger.Object, mockFileSystem.Object, Options.Create(securityOptions));
        var shellTool = new ShellTool(shellLogger.Object, Options.Create(securityOptions));

        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), registryLogger.Object);
        registry.Register(fileTool);
        registry.Register(shellTool);

        // Assert - Both registered
        Assert.Equal(2, registry.GetToolCount());
        Assert.True(registry.IsToolRegistered("read_file"));
        Assert.True(registry.IsToolRegistered("run_shell"));

        // Create manifest
        var manifest = registry.CreatePluginManifest();
        Assert.Equal(2, manifest.Tools.Count);
        Assert.All(manifest.Tools, t => Assert.NotEmpty(t.ParametersSchema));
    }

    #endregion
}
