# Getting Started with Goose.NET

Quick start guide to using Goose.NET in your applications.

## Prerequisites

- .NET 8.0 SDK or later
- An API key from a supported AI provider (Anthropic or OpenAI)
- Basic knowledge of C# and async/await

## Installation

### Via NuGet (when published)

```bash
dotnet add package Goose.Core
dotnet add package Goose.Providers
dotnet add package Goose.Tools
```

### Via Source

```bash
git clone https://github.com/yourusername/goose.net.git
cd goose.net
dotnet build
```

## Quick Start

### 1. Basic Console Application

Create a new console app and add Goose.NET:

```bash
dotnet new console -n MyGooseApp
cd MyGooseApp
dotnet add package Goose.Core
dotnet add package Goose.Providers
```

### 2. Configure Services

Create `appsettings.json`:

```json
{
  "Goose": {
    "DefaultProvider": "anthropic",
    "SessionDirectory": "~/.goose/sessions",
    "MaxTokens": 4096,
    "Temperature": 0.7,
    "Providers": {
      "anthropic": {
        "ApiKey": "your-anthropic-api-key-here",
        "Model": "claude-3-sonnet-20240229"
      },
      "openai": {
        "ApiKey": "your-openai-api-key-here",
        "Model": "gpt-4"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Goose": "Debug"
    }
  }
}
```

**Security Note:** Never commit API keys to source control! Use environment variables or user secrets:

```bash
# Using .NET User Secrets
dotnet user-secrets init
dotnet user-secrets set "Goose:Providers:anthropic:ApiKey" "your-key-here"

# Using Environment Variables
export GOOSE__PROVIDERS__ANTHROPIC__APIKEY="your-key-here"
```

### 3. Setup Dependency Injection

```csharp
using Goose.Core.Extensions;
using Goose.Providers.Extensions;
using Goose.Tools.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// Register Goose services
builder.Services.AddGooseCore(builder.Configuration);
builder.Services.AddGooseProviders(builder.Configuration);
builder.Services.AddGooseTools();

// Build the host
var host = builder.Build();

// Get the conversation agent
var agent = host.Services.GetRequiredService<IConversationAgent>();
var sessionManager = host.Services.GetRequiredService<ISessionManager>();
```

### 4. Create Your First Conversation

```csharp
using Goose.Core.Abstractions;
using Goose.Core.Models;

// Create a new session
var session = await sessionManager.CreateSessionAsync(new Session
{
    SessionId = Guid.NewGuid().ToString(),
    Name = "My First Conversation",
    Provider = "anthropic",
    WorkingDirectory = Environment.CurrentDirectory
});

// Create conversation context
var context = new ConversationContext
{
    SessionId = session.SessionId,
    WorkingDirectory = session.WorkingDirectory ?? Environment.CurrentDirectory,
    Messages = new List<Message>()
};

// Send a message
Console.WriteLine("You: Hello, what can you help me with?");
var response = await agent.ProcessMessageAsync(
    "Hello, what can you help me with?",
    context);

Console.WriteLine($"Assistant: {response.Content}");

// Save the context
await sessionManager.SaveContextAsync(session.SessionId, context);
```

## Complete Example

Here's a complete working example:

```csharp
using System;
using System.Threading.Tasks;
using Goose.Core.Abstractions;
using Goose.Core.Extensions;
using Goose.Core.Models;
using Goose.Providers.Extensions;
using Goose.Tools.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyGooseApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup
            var host = CreateHostBuilder(args).Build();
            var agent = host.Services.GetRequiredService<IConversationAgent>();
            var sessionManager = host.Services.GetRequiredService<ISessionManager>();

            // Create session
            var session = await sessionManager.CreateSessionAsync(new Session
            {
                SessionId = Guid.NewGuid().ToString(),
                Name = "Getting Started Demo",
                Provider = "anthropic",
                WorkingDirectory = Environment.CurrentDirectory
            });

            Console.WriteLine($"Session created: {session.SessionId}");

            // Create context
            var context = new ConversationContext
            {
                SessionId = session.SessionId,
                WorkingDirectory = session.WorkingDirectory ?? Environment.CurrentDirectory,
                Messages = new List<Message>()
            };

            // Interactive loop
            while (true)
            {
                Console.Write("\nYou: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    var response = await agent.ProcessMessageAsync(input, context);

                    Console.WriteLine($"\nAssistant: {response.Content}");

                    if (response.ToolResults?.Count > 0)
                    {
                        Console.WriteLine($"\n[Executed {response.ToolResults.Count} tool(s)]");
                        foreach (var result in response.ToolResults)
                        {
                            Console.WriteLine($"  - {result.ToolCallId}: {(result.Success ? "Success" : "Failed")}");
                        }
                    }

                    // Save context after each message
                    await sessionManager.SaveContextAsync(session.SessionId, context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError: {ex.Message}");
                }
            }

            Console.WriteLine("\nGoodbye!");
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddGooseCore(context.Configuration);
                    services.AddGooseProviders(context.Configuration);
                    services.AddGooseTools();
                });
    }
}
```

## Streaming Responses

For a better user experience, use streaming:

```csharp
Console.Write("Assistant: ");

await foreach (var item in agent.ProcessMessageStreamAsync(input, context))
{
    if (item is StreamChunk chunk)
    {
        Console.Write(chunk.Content);
    }
    else if (item is ToolResult toolResult)
    {
        Console.WriteLine($"\n[Tool: {toolResult.ToolCallId}]");
    }
    else if (item is AgentResponse finalResponse)
    {
        Console.WriteLine(); // New line after streaming complete
    }
}
```

## Working with Tools

### Registering Custom Tools

```csharp
using Goose.Core.Abstractions;
using Goose.Core.Models;

public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Performs basic math calculations";
    public string ParameterSchema => """
    {
      "type": "object",
      "properties": {
        "operation": { "type": "string", "enum": ["add", "subtract", "multiply", "divide"] },
        "a": { "type": "number" },
        "b": { "type": "number" }
      },
      "required": ["operation", "a", "b"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var param = JsonSerializer.Deserialize<CalculatorParams>(parameters);
            if (param == null)
                return CreateError("Invalid parameters");

            double result = param.Operation switch
            {
                "add" => param.A + param.B,
                "subtract" => param.A - param.B,
                "multiply" => param.A * param.B,
                "divide" => param.B != 0 ? param.A / param.B : throw new DivideByZeroException(),
                _ => throw new ArgumentException($"Unknown operation: {param.Operation}")
            };

            return new ToolResult
            {
                ToolCallId = "",  // Set by agent
                Success = true,
                Output = result.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return CreateError(ex.Message);
        }
    }

    public Task<ValidationResult> ValidateAsync(string parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var param = JsonSerializer.Deserialize<CalculatorParams>(parameters);
            if (param == null)
                return Task.FromResult(ValidationResult.Invalid("Invalid JSON"));

            if (param.Operation == "divide" && param.B == 0)
                return Task.FromResult(ValidationResult.Invalid("Cannot divide by zero"));

            return Task.FromResult(ValidationResult.Valid());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ValidationResult.Invalid(ex.Message));
        }
    }

    private ToolResult CreateError(string error) => new ToolResult
    {
        ToolCallId = "",
        Success = false,
        Error = error,
        Duration = TimeSpan.Zero
    };

    private record CalculatorParams(string Operation, double A, double B);
}

// Register the tool
var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
toolRegistry.Register(new CalculatorTool());
```

## Session Management

### Listing Sessions

```csharp
// List all active sessions
var sessions = await sessionManager.ListSessionsAsync();

foreach (var session in sessions)
{
    Console.WriteLine($"{session.Name} - {session.MessageCount} messages");
}

// List with filters
var recentSessions = await sessionManager.ListSessionsAsync(new SessionQueryOptions
{
    Limit = 10,
    Provider = "anthropic",
    IncludeArchived = false,
    CreatedAfter = DateTime.UtcNow.AddDays(-7)
});
```

### Loading a Previous Session

```csharp
// Get session
var session = await sessionManager.GetSessionAsync(sessionId);
if (session == null)
{
    Console.WriteLine("Session not found");
    return;
}

// Load context
var context = await sessionManager.LoadContextAsync(sessionId);
if (context == null)
{
    Console.WriteLine("Context not found");
    return;
}

// Continue conversation
var response = await agent.ProcessMessageAsync("Continue where we left off", context);
```

### Archiving Sessions

```csharp
// Archive a session
await sessionManager.ArchiveSessionAsync(sessionId);

// Restore an archived session
await sessionManager.RestoreSessionAsync(sessionId);

// Delete permanently
await sessionManager.DeleteSessionAsync(sessionId);
```

### Import/Export

```csharp
// Export a session
await sessionManager.ExportSessionAsync(sessionId, "/path/to/export.json");

// Import a session
var importedSession = await sessionManager.ImportSessionAsync("/path/to/export.json");
```

## Error Handling

```csharp
using Goose.Core.Models;

try
{
    var response = await agent.ProcessMessageAsync(input, context);
}
catch (ProviderException ex)
{
    Console.WriteLine($"AI Provider Error: {ex.Message}");
    Console.WriteLine($"Provider: {ex.ProviderName}");
    Console.WriteLine($"Status Code: {ex.StatusCode}");

    // Handle specific status codes
    if (ex.StatusCode == 429)
    {
        Console.WriteLine("Rate limit exceeded. Waiting before retry...");
        await Task.Delay(TimeSpan.FromSeconds(60));
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Configuration Best Practices

### 1. Use Configuration Hierarchy

Configuration is loaded in this order (later sources override earlier):
1. appsettings.json
2. appsettings.{Environment}.json
3. User Secrets (development only)
4. Environment Variables
5. Command-line arguments

### 2. Environment-Specific Settings

```json
// appsettings.Development.json
{
  "Goose": {
    "Temperature": 0.9,  // More creative in dev
    "MaxTokens": 8192
  },
  "Logging": {
    "LogLevel": {
      "Goose": "Debug"  // Verbose logging in dev
    }
  }
}

// appsettings.Production.json
{
  "Goose": {
    "Temperature": 0.7,  // More consistent in prod
    "MaxTokens": 4096
  },
  "Logging": {
    "LogLevel": {
      "Goose": "Information"  // Less verbose in prod
    }
  }
}
```

### 3. Environment Variables

Use double underscores for nested configuration:

```bash
# Set API key
export GOOSE__PROVIDERS__ANTHROPIC__APIKEY="sk-ant-..."

# Set temperature
export GOOSE__TEMPERATURE="0.8"

# Set session directory
export GOOSE__SESSIONDIRECTORY="/app/data/sessions"
```

## Performance Optimization

### Use OptimizedFileSystemSessionManager

For better performance with many sessions:

```csharp
services.AddSingleton<ISessionManager, OptimizedFileSystemSessionManager>();
```

Benefits:
- 10-50x faster cached reads
- 10x faster session listing
- 500x faster session counting
- Better concurrency with per-file locking

See [PERFORMANCE_OPTIMIZATIONS.md](PERFORMANCE_OPTIMIZATIONS.md) for details.

## Telemetry and Monitoring

### Enable Telemetry

Telemetry is enabled by default with `InMemoryTelemetry`. For production, use a custom implementation:

```csharp
// Application Insights
services.AddSingleton<ITelemetry, AppInsightsTelemetry>();

// Prometheus
services.AddSingleton<ITelemetry, PrometheusTelemetry>();

// Disable telemetry
services.AddSingleton<ITelemetry>(NullTelemetry.Instance);
```

### Query Metrics (Development)

```csharp
var telemetry = host.Services.GetRequiredService<ITelemetry>() as InMemoryTelemetry;

// Get metrics
var metrics = telemetry.GetMetrics();
Console.WriteLine($"Average message processing: {metrics["agent.process_message.duration_ms"].Average}ms");

// Get counters
var counters = telemetry.GetCounters();
Console.WriteLine($"Total tool calls: {counters["agent.tool_calls"]}");

// Get events
var events = telemetry.GetEvents();
foreach (var evt in events.Where(e => e.Name.StartsWith("session.")))
{
    Console.WriteLine($"{evt.Timestamp}: {evt.Name}");
}
```

See [LOGGING_AND_TELEMETRY.md](LOGGING_AND_TELEMETRY.md) for comprehensive guide.

## Common Patterns

### Request Timeout

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var response = await agent.ProcessMessageAsync(input, context, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request timed out after 30 seconds");
}
```

### Retry Logic

```csharp
int maxRetries = 3;
int retryCount = 0;

while (retryCount < maxRetries)
{
    try
    {
        var response = await agent.ProcessMessageAsync(input, context);
        return response;
    }
    catch (ProviderException ex) when (ex.StatusCode == 429)
    {
        retryCount++;
        if (retryCount >= maxRetries)
            throw;

        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
        Console.WriteLine($"Rate limited. Retrying in {delay.TotalSeconds}s...");
        await Task.Delay(delay);
    }
}
```

### System Messages

```csharp
// Add a system message to set behavior
context.Messages.Add(new Message
{
    Role = MessageRole.System,
    Content = "You are a helpful coding assistant. Always provide code examples in C#."
});

// Then process user messages normally
var response = await agent.ProcessMessageAsync("How do I read a file?", context);
```

### Conversation Memory Management

```csharp
// Limit conversation history to prevent token limits
const int maxMessages = 50;

if (context.Messages.Count > maxMessages)
{
    // Keep system message and recent messages
    var systemMessages = context.Messages.Where(m => m.Role == MessageRole.System).ToList();
    var recentMessages = context.Messages.TakeLast(maxMessages - systemMessages.Count).ToList();

    context.Messages.Clear();
    context.Messages.AddRange(systemMessages);
    context.Messages.AddRange(recentMessages);
}
```

## Testing Your Application

### Unit Testing with Mocks

```csharp
using Moq;
using Xunit;

public class MyServiceTests
{
    [Fact]
    public async Task ProcessUserInput_ReturnsResponse()
    {
        // Arrange
        var mockAgent = new Mock<IConversationAgent>();
        mockAgent
            .Setup(a => a.ProcessMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse
            {
                Content = "Test response"
            });

        var service = new MyService(mockAgent.Object);

        // Act
        var result = await service.ProcessUserInput("test input");

        // Assert
        Assert.Equal("Test response", result);
    }
}
```

### Integration Testing

```csharp
public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Agent_ProcessesMessage_Successfully()
    {
        // Arrange
        var agent = _factory.Services.GetRequiredService<IConversationAgent>();
        var context = new ConversationContext
        {
            SessionId = "test",
            WorkingDirectory = "/tmp",
            Messages = new List<Message>()
        };

        // Act
        var response = await agent.ProcessMessageAsync("Hello", context);

        // Assert
        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content);
    }
}
```

## Troubleshooting

### "Provider API key not configured"

**Solution:** Set your API key in configuration or environment variables.

```bash
dotnet user-secrets set "Goose:Providers:anthropic:ApiKey" "your-key"
```

### "Session directory not found"

**Solution:** The session directory is created automatically, but ensure parent directory exists and is writable.

```csharp
// Check permissions
var sessionDir = Environment.ExpandEnvironmentVariables("~/.goose/sessions");
Directory.CreateDirectory(sessionDir);
```

### "Tool not found"

**Solution:** Ensure tools are registered before use.

```csharp
// Check if tool is registered
var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
if (!toolRegistry.IsToolRegistered("file_read"))
{
    Console.WriteLine("File tool not registered");
}

// List all registered tools
var tools = toolRegistry.GetAllTools();
foreach (var tool in tools)
{
    Console.WriteLine($"- {tool.Name}: {tool.Description}");
}
```

### Rate Limiting

**Solution:** Implement exponential backoff and respect provider limits.

```csharp
// Anthropic: 5 requests/min for free tier
// OpenAI: Varies by tier

// Add delays between requests
await Task.Delay(TimeSpan.FromSeconds(12)); // ~5 req/min
```

## Next Steps

- **[API_REFERENCE.md](API_REFERENCE.md)** - Complete API documentation
- **[INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md)** - Integration patterns
- **[TOOL_DEVELOPMENT_GUIDE.md](TOOL_DEVELOPMENT_GUIDE.md)** - Build custom tools
- **[CLI_USER_GUIDE.md](CLI_USER_GUIDE.md)** - Use the CLI application
- **[LOGGING_AND_TELEMETRY.md](LOGGING_AND_TELEMETRY.md)** - Observability
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System design

## Support

- **Documentation:** https://github.com/yourusername/goose.net/docs
- **Issues:** https://github.com/yourusername/goose.net/issues
- **Discussions:** https://github.com/yourusername/goose.net/discussions

## Examples

Find more examples in the repository:
- `/examples/BasicConsole` - Simple console application
- `/examples/WebApi` - ASP.NET Core integration
- `/examples/CustomTools` - Custom tool development
- `/examples/MultiProvider` - Using multiple AI providers

---

**Happy Coding! 🦢**
