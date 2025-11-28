using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Microsoft.Extensions.Logging;

namespace Goose.Core.Services;

/// <summary>
/// Implementation of the conversation agent that orchestrates interactions between AI providers and tools
/// </summary>
public class ConversationAgent : IConversationAgent
{
    private readonly IProvider _provider;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ConversationAgent> _logger;
    private readonly ITelemetry _telemetry;
    private readonly IPermissionSystem _permissionSystem;
    private readonly IPermissionPrompt _permissionPrompt;
    private readonly IPermissionStore _permissionStore;
    private readonly IPermissionInspector _permissionInspector;

    /// <summary>
    /// Creates a new ConversationAgent instance
    /// </summary>
    /// <param name="provider">The AI provider to use for completions</param>
    /// <param name="toolRegistry">The registry of available tools</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="permissionSystem">Permission system for tool execution approval</param>
    /// <param name="permissionStore">Permission store for saving user decisions</param>
    /// <param name="permissionInspector">Permission inspector for security analysis</param>
    /// <param name="permissionPrompt">Permission prompt for user interaction (optional)</param>
    /// <param name="telemetry">Telemetry instance for metrics tracking (optional)</param>
    public ConversationAgent(
        IProvider provider,
        IToolRegistry toolRegistry,
        ILogger<ConversationAgent> logger,
        IPermissionSystem permissionSystem,
        IPermissionStore permissionStore,
        IPermissionInspector permissionInspector,
        IPermissionPrompt? permissionPrompt = null,
        ITelemetry? telemetry = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionSystem = permissionSystem ?? throw new ArgumentNullException(nameof(permissionSystem));
        _permissionStore = permissionStore ?? throw new ArgumentNullException(nameof(permissionStore));
        _permissionInspector = permissionInspector ?? throw new ArgumentNullException(nameof(permissionInspector));
        _permissionPrompt = permissionPrompt ?? NullPermissionPrompt.Instance;
        _telemetry = telemetry ?? NullTelemetry.Instance;
    }

    /// <summary>
    /// Processes a user message and generates an AI response
    /// </summary>
    /// <param name="message">The user's input message</param>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The agent's response including any tool calls</returns>
    public async Task<AgentResponse> ProcessMessageAsync(
        string message,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        using var operation = _telemetry.BeginTimedOperation("agent.process_message");

        _logger.LogInformation("Processing message in conversation {ConversationId}", context.SessionId);
        _telemetry.TrackEvent("message.processing_started", new Dictionary<string, string>
        {
            ["session_id"] = context.SessionId,
            ["message_length"] = message.Length.ToString()
        });

        try
        {
            // Add user message to context
            var userMessage = new Message
            {
                Role = MessageRole.User,
                Content = message
            };

            context.Messages.Add(userMessage);

            // Generate initial response from provider
            var response = await _provider.GenerateAsync(
                context.Messages,
                context.ProviderOptions,
                cancellationToken);

            // Tool execution loop - process any tool calls in the response
            var toolResults = new List<ToolResult>();
            var toolExecutionRounds = 0;

            while (response.ToolCalls?.Count > 0)
            {
                toolExecutionRounds++;
                _logger.LogDebug(
                    "Processing {ToolCallCount} tool calls (round {Round})",
                    response.ToolCalls.Count, toolExecutionRounds);

                _telemetry.IncrementCounter("agent.tool_calls", response.ToolCalls.Count);
                _telemetry.RecordMetric("agent.tool_calls_per_round", response.ToolCalls.Count);

                foreach (var toolCall in response.ToolCalls)
                {
                    var result = await ExecuteToolAsync(
                        toolCall,
                        context,
                        cancellationToken);

                    toolResults.Add(result);

                    // Add the tool result as a message to the conversation
                    var toolResultMessage = new Message
                    {
                        Role = MessageRole.Tool,
                        Content = result.Output ?? result.Error ?? "",
                        ToolCallId = toolCall.Id
                    };

                    context.Messages.Add(toolResultMessage);
                }

                // Get next response from provider with updated conversation
                response = await _provider.GenerateAsync(
                    context.Messages,
                    context.ProviderOptions,
                    cancellationToken);
            }

            // Add assistant response to context
            var assistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Content = response.Content
            };

            context.Messages.Add(assistantMessage);

            _telemetry.RecordMetric("agent.tool_execution_rounds", toolExecutionRounds);
            _telemetry.RecordMetric("agent.response_length", response.Content?.Length ?? 0);
            _telemetry.TrackEvent("message.processing_completed", new Dictionary<string, string>
            {
                ["session_id"] = context.SessionId,
                ["tool_calls"] = toolResults.Count.ToString(),
                ["rounds"] = toolExecutionRounds.ToString()
            });

            return new AgentResponse
            {
                Content = response.Content,
                ToolResults = toolResults
            };
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, new Dictionary<string, string>
            {
                ["session_id"] = context.SessionId,
                ["operation"] = "process_message"
            });
            throw;
        }
    }

    /// <summary>
    /// Processes a user message with streaming response
    /// </summary>
    public async IAsyncEnumerable<object> ProcessMessageStreamAsync(
        string message,
        ConversationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation("Processing streaming message in conversation {ConversationId}", context.SessionId);

        // Add user message to context
        var userMessage = new Message
        {
            Role = MessageRole.User,
            Content = message
        };

        context.Messages.Add(userMessage);

        // Stream initial response from provider
        var contentBuilder = new System.Text.StringBuilder();
        var toolCalls = new List<ToolCall>();

        await foreach (var chunk in _provider.StreamAsync(
            context.Messages,
            context.ProviderOptions,
            cancellationToken))
        {
            // Yield the chunk to the caller
            yield return chunk;

            // Build up the complete content
            contentBuilder.Append(chunk.Content);

            // Collect tool calls if present
            if (chunk.ToolCall != null && !toolCalls.Any(tc => tc.Id == chunk.ToolCall.Id))
            {
                toolCalls.Add(chunk.ToolCall);
            }
        }

        var completeContent = contentBuilder.ToString();
        var toolResults = new List<ToolResult>();

        // Tool execution loop - process any tool calls
        while (toolCalls.Count > 0)
        {
            _logger.LogDebug("Processing {ToolCallCount} tool calls", toolCalls.Count);

            foreach (var toolCall in toolCalls)
            {
                var result = await ExecuteToolAsync(toolCall, context, cancellationToken);
                toolResults.Add(result);

                // Yield tool execution event
                yield return result;

                // Add the tool result as a message to the conversation
                var toolResultMessage = new Message
                {
                    Role = MessageRole.Tool,
                    Content = result.Output ?? result.Error ?? "",
                    ToolCallId = toolCall.Id
                };

                context.Messages.Add(toolResultMessage);
            }

            // Stream next response from provider with updated conversation
            contentBuilder.Clear();
            toolCalls.Clear();

            await foreach (var chunk in _provider.StreamAsync(
                context.Messages,
                context.ProviderOptions,
                cancellationToken))
            {
                yield return chunk;
                contentBuilder.Append(chunk.Content);

                if (chunk.ToolCall != null && !toolCalls.Any(tc => tc.Id == chunk.ToolCall.Id))
                {
                    toolCalls.Add(chunk.ToolCall);
                }
            }

            completeContent = contentBuilder.ToString();
        }

        // Add assistant response to context
        var assistantMessage = new Message
        {
            Role = MessageRole.Assistant,
            Content = completeContent
        };

        context.Messages.Add(assistantMessage);

        // Yield final response
        yield return new AgentResponse
        {
            Content = completeContent,
            ToolResults = toolResults
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
            using var operation = _telemetry.BeginTimedOperation("tool.execute", new Dictionary<string, string>
            {
                ["tool_name"] = toolCall.Name
            });

            _logger.LogDebug("Executing tool: {ToolName}", toolCall.Name);

            // Look up the tool by name in registry
            if (!_toolRegistry.TryGetTool(toolCall.Name, out var tool))
            {
                _telemetry.IncrementCounter("tool.not_found");
                _telemetry.TrackEvent("tool.execution_failed", new Dictionary<string, string>
                {
                    ["tool_name"] = toolCall.Name,
                    ["reason"] = "not_found"
                });

                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    Success = false,
                    Error = $"Tool '{toolCall.Name}' not found",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Create tool execution context from conversation context
            var toolContext = new ToolContext
            {
                WorkingDirectory = context.WorkingDirectory,
                EnvironmentVariables = new Dictionary<string, string>()
            };

            // Request permission to execute the tool
            var permissionResponse = await _permissionSystem.RequestPermissionAsync(
                toolCall,
                tool.RiskLevel,
                toolContext,
                context.SessionId,
                cancellationToken);

            // Handle permission decision
            if (permissionResponse.Decision == PermissionDecision.Deny)
            {
                _logger.LogWarning("Tool execution denied by permission system: {ToolName}", toolCall.Name);
                _telemetry.IncrementCounter("tool.executions_denied", 1, new Dictionary<string, string>
                {
                    ["tool_name"] = toolCall.Name
                });

                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    Success = false,
                    Error = $"Permission denied to execute tool '{toolCall.Name}'",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Handle Ask decision - prompt the user
            if (permissionResponse.Decision == PermissionDecision.Ask)
            {
                _logger.LogInformation("Requesting user approval for tool '{ToolName}'", toolCall.Name);

                // Get inspection result for the prompt
                var inspectionResult = await _permissionInspector.InspectAsync(
                    toolCall,
                    toolContext,
                    cancellationToken);

                // Prompt the user for their decision
                var (userDecision, rememberDecision) = await _permissionPrompt.PromptUserAsync(
                    toolCall,
                    tool.RiskLevel,
                    inspectionResult,
                    cancellationToken);

                // Save the decision if user wants to remember it
                if (rememberDecision && userDecision != PermissionDecision.Ask)
                {
                    await _permissionStore.SavePermissionAsync(
                        context.SessionId,
                        toolCall.Name,
                        userDecision,
                        cancellationToken);

                    _logger.LogInformation(
                        "Saved permission decision for tool '{ToolName}': {Decision}",
                        toolCall.Name,
                        userDecision);
                }

                // Handle user's decision
                if (userDecision == PermissionDecision.Deny)
                {
                    _logger.LogInformation("User denied permission for tool '{ToolName}'", toolCall.Name);
                    _telemetry.IncrementCounter("tool.executions_user_denied", 1, new Dictionary<string, string>
                    {
                        ["tool_name"] = toolCall.Name
                    });

                    return new ToolResult
                    {
                        ToolCallId = toolCall.Id,
                        Success = false,
                        Error = $"User denied permission to execute tool '{toolCall.Name}'",
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                _logger.LogInformation("User approved tool execution: {ToolName}", toolCall.Name);
            }

            // Execute the tool
            var result = await tool.ExecuteAsync(
                toolCall.Parameters,
                toolContext,
                cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            result = result with { Duration = duration };

            // Track metrics
            if (result.Success)
            {
                _telemetry.IncrementCounter("tool.executions_succeeded", 1, new Dictionary<string, string>
                {
                    ["tool_name"] = toolCall.Name
                });
            }
            else
            {
                _telemetry.IncrementCounter("tool.executions_failed", 1, new Dictionary<string, string>
                {
                    ["tool_name"] = toolCall.Name
                });
            }

            _telemetry.TrackEvent("tool.execution_completed", new Dictionary<string, string>
            {
                ["tool_name"] = toolCall.Name,
                ["success"] = result.Success.ToString(),
                ["duration_ms"] = duration.TotalMilliseconds.ToString("F2")
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolCall.Name);

            _telemetry.IncrementCounter("tool.executions_exception", 1, new Dictionary<string, string>
            {
                ["tool_name"] = toolCall.Name
            });
            _telemetry.TrackException(ex, new Dictionary<string, string>
            {
                ["tool_name"] = toolCall.Name,
                ["operation"] = "execute_tool"
            });

            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                Success = false,
                Error = $"Tool execution failed: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}
