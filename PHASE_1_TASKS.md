# Phase 1: Core Architecture Translation

## Phase Overview

**Duration:** Weeks 2-4 (3 weeks)  
**Goal:** Establish foundational architecture and core abstractions  
**Success Criteria:** All core interfaces defined, base implementations created, unit tests passing

## Prerequisites

Before starting Phase 1, ensure:
- [ ] .NET 8.0 SDK installed
- [ ] Original Goose repository cloned and reviewed
- [ ] Target commit/version documented
- [ ] Project structure created
- [ ] CI/CD pipeline configured
- [ ] Documentation structure established

## Task Breakdown

### Task 1.1: Solution and Project Setup

**Priority:** Critical  
**Estimated Effort:** 4 hours  
**AI Suitability:** High (with human review)

#### Description
Create the solution structure and initial projects with proper configuration.

#### Subtasks
1. Create solution file: `Goose.sln`
2. Create projects:
   - `src/Goose.Core/Goose.Core.csproj`
   - `src/Goose.Providers/Goose.Providers.csproj`
   - `src/Goose.Tools/Goose.Tools.csproj`
   - `src/Goose.CLI/Goose.CLI.csproj`
   - `tests/Goose.Core.Tests/Goose.Core.Tests.csproj`
3. Configure project settings:
   - Target framework: `net8.0`
   - Enable nullable reference types
   - Set language version to C# 12
   - Configure project references

#### Deliverables
- [ ] Solution builds successfully
- [ ] All projects compile
- [ ] Project references correct
- [ ] .editorconfig in place

#### Acceptance Criteria
```bash
dotnet build
# Expected: Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test
# Expected: No tests found (but no errors)
```

#### Reference
- Original Python: N/A (project structure)
- .NET Docs: https://learn.microsoft.com/dotnet/core/tools/dotnet-new

---

### Task 1.2: Core Abstractions - IProvider Interface

**Priority:** Critical  
**Estimated Effort:** 3 hours  
**AI Suitability:** High

#### Description
Define the IProvider interface that all AI provider implementations will implement.

#### Python Source Reference
```python
# From goose/provider.py or similar
class Provider:
    async def generate(self, messages: list[Message]) -> Response:
        pass
```

#### C# Implementation

**File:** `src/Goose.Core/Abstractions/IProvider.cs`

```csharp
namespace Goose.Core.Abstractions;

/// <summary>
/// Defines the contract for AI model providers (Anthropic, OpenAI, etc.)
/// </summary>
public interface IProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "anthropic", "openai")
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Generates a completion from the given messages
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The provider's response</returns>
    Task<ProviderResponse> GenerateAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Streams a completion with incremental updates
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the tool definitions supported by this provider
    /// </summary>
    IReadOnlyList<ToolDefinition> GetToolDefinitions();
}
```

#### Subtasks
1. Create interface file
2. Add XML documentation
3. Define related types (ProviderResponse, ProviderOptions, StreamChunk)
4. Add unit tests for any default implementations

#### Deliverables
- [ ] IProvider.cs created
- [ ] Related model classes defined
- [ ] XML documentation complete
- [ ] Interface compiles without warnings

#### Acceptance Criteria
- Interface is public and in correct namespace
- All methods have XML documentation
- Async methods follow naming convention (Async suffix)
- CancellationToken support included

---

### Task 1.3: Core Models - Message Types

**Priority:** Critical  
**Estimated Effort:** 3 hours  
**AI Suitability:** High

#### Description
Create the core message models used throughout the system.

#### Python Source Reference
```python
@dataclass
class Message:
    role: str
    content: str
    timestamp: Optional[float] = None
    tool_calls: Optional[list[dict]] = None
```

#### C# Implementation

**File:** `src/Goose.Core/Models/Message.cs`

```csharp
namespace Goose.Core.Models;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public record Message
{
    /// <summary>
    /// The role of the message sender
    /// </summary>
    public required MessageRole Role { get; init; }
    
    /// <summary>
    /// The content of the message
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Tool calls requested in this message (if any)
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    
    /// <summary>
    /// ID of the tool call this message responds to (for tool result messages)
    /// </summary>
    public string? ToolCallId { get; init; }
}

/// <summary>
/// The role of a message in a conversation
/// </summary>
public enum MessageRole
{
    /// <summary>System message (instructions)</summary>
    System,
    
    /// <summary>User message (human input)</summary>
    User,
    
    /// <summary>Assistant message (AI response)</summary>
    Assistant,
    
    /// <summary>Tool result message</summary>
    Tool
}
```

#### Subtasks
1. Create Message record
2. Create MessageRole enum
3. Create ToolCall record
4. Add validation logic if needed
5. Create unit tests

#### Deliverables
- [ ] Message.cs created
- [ ] MessageRole.cs created
- [ ] ToolCall.cs created
- [ ] Tests for model validation

#### Test Examples
```csharp
[Fact]
public void Message_WithRequiredProperties_CreatesSuccessfully()
{
    var message = new Message
    {
        Role = MessageRole.User,
        Content = "Hello"
    };
    
    Assert.Equal(MessageRole.User, message.Role);
    Assert.Equal("Hello", message.Content);
    Assert.NotEqual(default, message.Timestamp);
}

[Fact]
public void Message_MissingRequiredProperty_ThrowsException()
{
    Assert.Throws<InvalidOperationException>(() => 
        new Message { Role = MessageRole.User });
}
```

---

### Task 1.4: Core Abstractions - ITool Interface

**Priority:** Critical  
**Estimated Effort:** 3 hours  
**AI Suitability:** High

#### Description
Define the ITool interface for extensible tool/capability system.

#### Python Source Reference
```python
class Tool(ABC):
    @property
    @abstractmethod
    def name(self) -> str:
        pass
    
    @property
    @abstractmethod
    def description(self) -> str:
        pass
    
    @abstractmethod
    async def execute(self, params: dict) -> ToolResult:
        pass
```

#### C# Implementation

**File:** `src/Goose.Core/Abstractions/ITool.cs`

```csharp
namespace Goose.Core.Abstractions;

/// <summary>
/// Defines a tool that can be invoked by the AI agent
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique identifier for the tool
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON schema defining the tool's parameters
    /// </summary>
    JsonSchema ParameterSchema { get; }
    
    /// <summary>
    /// Executes the tool with the given parameters
    /// </summary>
    /// <param name="parameters">Tool parameters as JSON</param>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of tool execution</returns>
    Task<ToolResult> ExecuteAsync(
        JsonDocument parameters,
        ToolContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that the tool can execute in the current context
    /// </summary>
    /// <param name="parameters">Tool parameters to validate</param>
    /// <param name="context">Execution context</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateAsync(
        JsonDocument parameters,
        ToolContext context);
}
```

#### Related Types

**File:** `src/Goose.Core/Models/ToolResult.cs`

```csharp
public record ToolResult
{
    public required string ToolCallId { get; init; }
    public required bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

**File:** `src/Goose.Core/Models/ToolContext.cs`

```csharp
public class ToolContext
{
    public required string WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
    public Dictionary<string, object> Metadata { get; } = new();
}
```

#### Deliverables
- [ ] ITool.cs created
- [ ] ToolResult.cs created
- [ ] ToolContext.cs created
- [ ] ValidationResult.cs created
- [ ] JsonSchema helper type created

---

### Task 1.5: Core Service - ToolRegistry

**Priority:** High  
**Estimated Effort:** 4 hours  
**AI Suitability:** Medium (requires careful design review)

#### Description
Implement the tool registry that manages available tools.

#### Python Source Reference
```python
class ToolRegistry:
    def __init__(self):
        self._tools: dict[str, Tool] = {}
    
    def register(self, tool: Tool) -> None:
        self._tools[tool.name] = tool
    
    def get(self, name: str) -> Optional[Tool]:
        return self._tools.get(name)
```

#### C# Implementation

**File:** `src/Goose.Core/Services/ToolRegistry.cs`

```csharp
namespace Goose.Core.Services;

/// <summary>
/// Registry for managing available tools
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;
    
    public ToolRegistry(
        IEnumerable<ITool> tools,
        ILogger<ToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }
    
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        
        if (_tools.ContainsKey(tool.Name))
        {
            _logger.LogWarning(
                "Tool {ToolName} is already registered, replacing",
                tool.Name);
        }
        
        _tools[tool.Name] = tool;
        _logger.LogDebug("Registered tool: {ToolName}", tool.Name);
    }
    
    public ITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }
    
    public IReadOnlyList<ITool> GetAllTools()
    {
        return _tools.Values.ToList().AsReadOnly();
    }
    
    public bool TryGetTool(string name, [NotNullWhen(true)] out ITool? tool)
    {
        return _tools.TryGetValue(name, out tool);
    }
}
```

#### Interface

**File:** `src/Goose.Core/Abstractions/IToolRegistry.cs`

```csharp
public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    bool TryGetTool(string name, [NotNullWhen(true)] out ITool? tool);
}
```

#### Unit Tests

**File:** `tests/Goose.Core.Tests/Services/ToolRegistryTests.cs`

```csharp
public class ToolRegistryTests
{
    [Fact]
    public void Register_ValidTool_Success()
    {
        var tool = CreateMockTool("test-tool");
        var registry = CreateRegistry();
        
        registry.Register(tool);
        
        var retrieved = registry.GetTool("test-tool");
        Assert.NotNull(retrieved);
        Assert.Equal("test-tool", retrieved.Name);
    }
    
    [Fact]
    public void GetTool_NonExistent_ReturnsNull()
    {
        var registry = CreateRegistry();
        
        var tool = registry.GetTool("non-existent");
        
        Assert.Null(tool);
    }
    
    // More tests...
}
```

#### Deliverables
- [ ] IToolRegistry.cs created
- [ ] ToolRegistry.cs implemented
- [ ] Unit tests written and passing
- [ ] DI registration extension method

---

### Task 1.6: Core Service - ConversationAgent

**Priority:** Critical  
**Estimated Effort:** 6 hours  
**AI Suitability:** Medium (complex business logic)

#### Description
Implement the main conversation agent that orchestrates the interaction loop.

#### Python Source Reference
```python
class ConversationAgent:
    async def process_message(self, message: str) -> Response:
        # Add user message
        self.messages.append(Message(role="user", content=message))
        
        # Get AI response
        response = await self.provider.generate(self.messages)
        
        # Handle tool calls if any
        while response.tool_calls:
            for tool_call in response.tool_calls:
                result = await self.execute_tool(tool_call)
                self.messages.append(result)
            
            response = await self.provider.generate(self.messages)
        
        return response
```

#### C# Implementation

**File:** `src/Goose.Core/Services/ConversationAgent.cs`

```csharp
namespace Goose.Core.Services;

public class ConversationAgent : IConversationAgent
{
    private readonly IProvider _provider;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ConversationAgent> _logger;
    
    public ConversationAgent(
        IProvider provider,
        IToolRegistry toolRegistry,
        ILogger<ConversationAgent> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<AgentResponse> ProcessMessageAsync(
        string message,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogInformation("Processing message in conversation");
        
        // Add user message to context
        context.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = message
        });
        
        // Generate initial response
        var response = await _provider.GenerateAsync(
            context.Messages,
            context.ProviderOptions,
            cancellationToken);
        
        // Tool execution loop
        while (response.ToolCalls?.Count > 0)
        {
            _logger.LogDebug(
                "Processing {ToolCallCount} tool calls",
                response.ToolCalls.Count);
            
            foreach (var toolCall in response.ToolCalls)
            {
                var result = await ExecuteToolAsync(
                    toolCall,
                    context,
                    cancellationToken);
                
                context.Messages.Add(new Message
                {
                    Role = MessageRole.Tool,
                    Content = result.Output ?? result.Error ?? "",
                    ToolCallId = toolCall.Id
                });
            }
            
            // Get next response
            response = await _provider.GenerateAsync(
                context.Messages,
                context.ProviderOptions,
                cancellationToken);
        }
        
        // Add assistant response to context
        context.Messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Content = response.Content
        });
        
        return new AgentResponse
        {
            Content = response.Content,
            ToolResults = GetToolResults(context)
        };
    }
    
    private async Task<ToolResult> ExecuteToolAsync(
        ToolCall toolCall,
        ConversationContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!_toolRegistry.TryGetTool(toolCall.Name, out var tool))
            {
                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    Success = false,
                    Error = $"Tool '{toolCall.Name}' not found"
                };
            }
            
            _logger.LogDebug("Executing tool: {ToolName}", toolCall.Name);
            
            var result = await tool.ExecuteAsync(
                toolCall.Parameters,
                CreateToolContext(context),
                cancellationToken);
            
            return result with 
            { 
                Duration = DateTime.UtcNow - startTime 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolCall.Name);
            
            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                Success = false,
                Error = $"Tool execution failed: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
    
    // Helper methods...
}
```

#### Deliverables
- [ ] IConversationAgent.cs interface
- [ ] ConversationAgent.cs implementation
- [ ] ConversationContext.cs model
- [ ] AgentResponse.cs model
- [ ] Comprehensive unit tests
- [ ] Integration tests with mock provider

---

### Task 1.7: Configuration System

**Priority:** High  
**Estimated Effort:** 4 hours  
**AI Suitability:** High

#### Description
Implement configuration system with options pattern.

#### Files to Create

1. `src/Goose.Core/Configuration/GooseOptions.cs`
2. `src/Goose.Core/Configuration/ProviderOptions.cs`
3. `appsettings.json` example

#### Implementation

```csharp
public class GooseOptions
{
    public const string SectionName = "Goose";
    
    public string DefaultProvider { get; set; } = "anthropic";
    public string SessionDirectory { get; set; } = "~/.goose/sessions";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public Dictionary<string, ProviderOptions> Providers { get; set; } = new();
}
```

#### Deliverables
- [ ] Options classes created
- [ ] Validation attributes added
- [ ] Configuration loading tested
- [ ] Example appsettings.json

---

## Phase 1 Completion Checklist

### Code Quality
- [ ] All code follows coding standards
- [ ] XML documentation complete
- [ ] No compiler warnings
- [ ] Static analysis passing

### Testing
- [ ] Unit test coverage >80%
- [ ] All tests passing
- [ ] Integration tests for main flows
- [ ] Test naming conventions followed

### Documentation
- [ ] Architecture decisions documented
- [ ] API documentation generated
- [ ] README updated with progress
- [ ] Blog post drafted

### Git
- [ ] All commits have meaningful messages
- [ ] Commits tagged with AI/Human attribution
- [ ] No sensitive data in commits
- [ ] Branch ready for PR

## Success Metrics

**Code Metrics:**
- Lines of code: ~1,500-2,000
- Test coverage: ≥80%
- Cyclomatic complexity: <10 per method
- Maintainability index: >70

**Functional Metrics:**
- All core interfaces defined ✅
- Basic conversation flow works ✅
- Tool registry functional ✅
- Configuration loading works ✅

## Next Steps

After Phase 1 completion:
1. Review with human (Matthew)
2. Publish Phase 1 retrospective
3. Demo core functionality
4. Begin Phase 2: Provider Implementations

---

**Remember: Quality over speed. Each component should be production-ready before moving forward.**
