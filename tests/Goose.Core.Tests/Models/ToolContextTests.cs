using Goose.Core.Models;
using Xunit;

namespace Goose.Core.Tests.Models;

public class ToolContextTests
{
    [Fact]
    public void ToolContext_WithRequiredProperties_CreatesCorrectly()
    {
        // Arrange & Act
        var context = new ToolContext
        {
            WorkingDirectory = "/home/user/project"
        };

        // Assert
        Assert.Equal("/home/user/project", context.WorkingDirectory);
        Assert.NotNull(context.Metadata);
        Assert.Empty(context.Metadata);
    }

    [Fact]
    public void ToolContext_WithEnvironmentVariables_StoresCorrectly()
    {
        // Arrange
        var envVars = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin:/usr/local/bin",
            ["HOME"] = "/home/user"
        };

        // Act
        var context = new ToolContext
        {
            WorkingDirectory = "/test",
            EnvironmentVariables = envVars
        };

        // Assert
        Assert.NotNull(context.EnvironmentVariables);
        Assert.Equal(2, context.EnvironmentVariables.Count);
        Assert.Equal("/usr/bin:/usr/local/bin", context.EnvironmentVariables["PATH"]);
        Assert.Equal("/home/user", context.EnvironmentVariables["HOME"]);
    }

    [Fact]
    public void ToolContext_Metadata_CanBeModified()
    {
        // Arrange
        var context = new ToolContext
        {
            WorkingDirectory = "/test"
        };

        // Act
        context.Metadata["userId"] = "user123";
        context.Metadata["sessionId"] = "session456";

        // Assert
        Assert.Equal(2, context.Metadata.Count);
        Assert.Equal("user123", context.Metadata["userId"]);
        Assert.Equal("session456", context.Metadata["sessionId"]);
    }

    [Fact]
    public void ToolContext_EnvironmentVariables_DefaultsToEmpty()
    {
        // Arrange & Act
        var context = new ToolContext
        {
            WorkingDirectory = "/test"
        };

        // Assert - EnvironmentVariables should be null or empty by default
        Assert.True(context.EnvironmentVariables == null || context.EnvironmentVariables.Count == 0);
    }
}
