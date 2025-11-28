using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Goose.Core.Tests;

public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    
    public ToolRegistryTests()
    {
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
    }
    
    [Fact]
    public void Constructor_InitializesWithTools()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("Tool1");

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("Tool2");

        var tools = new List<ITool>
        {
            mockTool1.Object,
            mockTool2.Object
        };

        // Act
        var registry = new ToolRegistry(tools, _mockLogger.Object);

        // Assert
        Assert.NotNull(registry);
        Assert.Equal(2, registry.GetAllTools().Count);
    }
    
    [Fact]
    public void Register_AddsTool()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        
        // Act
        registry.Register(mockTool.Object);
        
        // Assert
        var tool = registry.GetTool("TestTool");
        Assert.NotNull(tool);
    }
    
    [Fact]
    public void Register_ThrowsArgumentNullException_WhenToolIsNull()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }
    
    [Fact]
    public void GetTool_ReturnsNull_WhenToolNotFound()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act
        var tool = registry.GetTool("NonExistent");
        
        // Assert
        Assert.Null(tool);
    }
    
    [Fact]
    public void GetAllTools_ReturnsAllRegisteredTools()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("TestTool1");
        
        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("TestTool2");
        
        registry.Register(mockTool1.Object);
        registry.Register(mockTool2.Object);
        
        // Act
        var allTools = registry.GetAllTools();
        
        // Assert
        Assert.Equal(2, allTools.Count);
    }
    
    [Fact]
    public void TryGetTool_FindsExistingTool()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        
        registry.Register(mockTool.Object);
        
        // Act
        var found = registry.TryGetTool("TestTool", out var tool);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(tool);
    }
    
    [Fact]
    public void TryGetTool_ReturnsFalse_WhenToolNotFound()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act
        var found = registry.TryGetTool("NonExistent", out var tool);
        
        // Assert
        Assert.False(found);
        Assert.Null(tool);
    }
    
    [Fact]
    public void LoadToolsFromAssemblyAsync_ReturnsZero_WhenNoValidToolTypes()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act
        var result = registry.LoadToolsFromAssemblyAsync(typeof(object).Assembly).Result;
        
        // Assert
        Assert.Equal(0, result);
    }
    
    [Fact]
    public void GetToolMetadata_ReturnsToolMetadata_WhenToolExists()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A test tool");
        
        registry.Register(mockTool.Object);
        
        // Act
        var metadata = registry.GetToolMetadata("TestTool");
        
        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("TestTool", metadata.Name);
        Assert.Equal("A test tool", metadata.Description);
    }
    
    [Fact]
    public void GetToolMetadata_ReturnsNull_WhenToolDoesNotExist()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act
        var metadata = registry.GetToolMetadata("NonExistent");
        
        // Assert
        Assert.Null(metadata);
    }
    
    [Fact]
    public void ValidateTool_ReturnsInvalid_WhenToolDoesNotExist()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        
        // Act
        var result = registry.ValidateTool("NonExistent");
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not found", result.ErrorMessage ?? "");
    }
    
    [Fact]
    public void CreatePluginManifest_ReturnsValidManifest()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A test tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        registry.Register(mockTool.Object);

        // Act
        var manifest = registry.CreatePluginManifest();

        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.Tools);
        Assert.NotNull(manifest.Metadata);
        Assert.Contains("TotalTools", manifest.Metadata.Keys);
    }

    [Fact]
    public void Register_ThrowsArgumentException_WhenToolNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => registry.Register(mockTool.Object));
        Assert.Contains("cannot be null or whitespace", ex.Message);
    }

    [Fact]
    public void Register_ThrowsArgumentException_WhenToolNameContainsInvalidCharacters()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("Invalid@Tool!");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => registry.Register(mockTool.Object));
        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public void Register_ThrowsArgumentException_WhenParameterSchemaIsInvalidJson()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("valid_tool");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns("{ invalid json }");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => registry.Register(mockTool.Object));
        Assert.Contains("invalid JSON parameter schema", ex.Message);
    }

    [Fact]
    public void Register_Succeeds_WithValidToolName()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("valid_tool-123");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        // Act
        registry.Register(mockTool.Object);

        // Assert
        Assert.True(registry.IsToolRegistered("valid_tool-123"));
    }

    [Fact]
    public void Unregister_RemovesTool_WhenToolExists()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        registry.Register(mockTool.Object);

        // Act
        var result = registry.Unregister("TestTool");

        // Assert
        Assert.True(result);
        Assert.False(registry.IsToolRegistered("TestTool"));
    }

    [Fact]
    public void Unregister_ReturnsFalse_WhenToolDoesNotExist()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var result = registry.Unregister("NonExistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsToolRegistered_ReturnsTrue_WhenToolExists()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        registry.Register(mockTool.Object);

        // Act
        var result = registry.IsToolRegistered("TestTool");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsToolRegistered_ReturnsFalse_WhenToolDoesNotExist()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var result = registry.IsToolRegistered("NonExistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetToolCount_ReturnsCorrectCount()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("Tool1");
        mockTool1.Setup(t => t.Description).Returns("A tool");
        mockTool1.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("Tool2");
        mockTool2.Setup(t => t.Description).Returns("A tool");
        mockTool2.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        // Act
        registry.Register(mockTool1.Object);
        registry.Register(mockTool2.Object);

        // Assert
        Assert.Equal(2, registry.GetToolCount());
    }

    [Fact]
    public void ValidateTool_ReturnsInvalid_WhenToolNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var result = registry.ValidateTool("");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be null or whitespace", result.ErrorMessage ?? "");
    }

    [Fact]
    public void ValidateTool_ReturnsInvalid_WhenToolNameContainsInvalidCharacters()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var result = registry.ValidateTool("Invalid@Tool!");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid characters", result.ErrorMessage ?? "");
    }

    [Fact]
    public void ValidateTool_ReturnsValid_WhenToolIsValid()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("ValidTool");
        mockTool.Setup(t => t.Description).Returns("A valid tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        registry.Register(mockTool.Object);

        // Act
        var result = registry.ValidateTool("ValidTool");

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateToolParametersAsync_ReturnsInvalid_WhenToolDoesNotExist()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var context = new ToolContext
        {
            WorkingDirectory = "/tmp",
            EnvironmentVariables = new Dictionary<string, string>()
        };

        // Act
        var result = await registry.ValidateToolParametersAsync(
            "NonExistent",
            @"{ ""param"": ""value"" }",
            context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not found", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_ReturnsInvalid_WhenParametersAreInvalidJson()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");

        registry.Register(mockTool.Object);

        var context = new ToolContext
        {
            WorkingDirectory = "/tmp",
            EnvironmentVariables = new Dictionary<string, string>()
        };

        // Act
        var result = await registry.ValidateToolParametersAsync(
            "TestTool",
            "{ invalid json }",
            context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid JSON", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_DelegatesToToolValidation()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"" }");
        mockTool.Setup(t => t.ValidateAsync(It.IsAny<string>(), It.IsAny<ToolContext>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        registry.Register(mockTool.Object);

        var context = new ToolContext
        {
            WorkingDirectory = "/tmp",
            EnvironmentVariables = new Dictionary<string, string>()
        };

        // Act
        var result = await registry.ValidateToolParametersAsync(
            "TestTool",
            @"{ ""param"": ""value"" }",
            context);

        // Assert
        Assert.True(result.IsValid);
        mockTool.Verify(t => t.ValidateAsync(It.IsAny<string>(), It.IsAny<ToolContext>()), Times.Once);
    }

    [Fact]
    public void GetToolMetadata_ParsesParameterSchemaCorrectly()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A test tool");
        mockTool.Setup(t => t.ParameterSchema).Returns(@"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }");

        registry.Register(mockTool.Object);

        // Act
        var metadata = registry.GetToolMetadata("TestTool");

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.ParametersSchema);
        Assert.Contains("type", metadata.ParametersSchema.Keys);
        Assert.Equal("object", metadata.ParametersSchema["type"]);
    }

    [Fact]
    public void GetTool_ReturnsNull_WhenToolNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var tool = registry.GetTool("");

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void TryGetTool_ReturnsFalse_WhenToolNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(Enumerable.Empty<ITool>(), _mockLogger.Object);

        // Act
        var found = registry.TryGetTool("", out var tool);

        // Assert
        Assert.False(found);
        Assert.Null(tool);
    }
}
