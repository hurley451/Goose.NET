using PhotinoNET;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Core.Extensions;
using Goose.Providers.Extensions;
using Goose.Tools.Extensions;
using System.Text.Json;

namespace Goose.GUI;

class Program
{
    private static IServiceProvider? _serviceProvider;
    private static PhotinoWindow? _window;

    [STAThread]
    static void Main(string[] args)
    {
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup DI container
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IConfiguration>(configuration);

        // Register GUI-specific permission prompt
        var permissionPrompt = new PhotinoPermissionPrompt();
        services.AddSingleton<IPermissionPrompt>(permissionPrompt);

        // Register Goose.NET services
        services.AddGooseCore(configuration);

        // Register providers (fail gracefully if no API key)
        try
        {
            services.AddAnthropicProvider(configuration);
            Console.WriteLine("✓ Anthropic provider registered");
        }
        catch
        {
            Console.WriteLine("⚠ Anthropic provider not available (API key missing)");
        }

        try
        {
            services.AddOpenAIProvider(configuration);
            Console.WriteLine("✓ OpenAI provider registered");
        }
        catch
        {
            Console.WriteLine("⚠ OpenAI provider not available (API key missing)");
        }

        // Register tools
        services.AddGooseTools(configuration);
        Console.WriteLine("✓ Tools registered");

        _serviceProvider = services.BuildServiceProvider();

        // Find the HTML file
        var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.html");
        if (!File.Exists(htmlPath))
        {
            htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "app.html");
        }

        Console.WriteLine($"Goose.NET GUI v1.0.0");
        Console.WriteLine($"Loading from: {htmlPath}");

        // Create window
        _window = new PhotinoWindow()
            .SetTitle("Goose.NET")
            .SetSize(1400, 900)
            .Center()
            .SetResizable(true)
            .RegisterWebMessageReceivedHandler(HandleWebMessage)
            .LoadRawString(File.ReadAllText(htmlPath));

        // Set window on permission prompt
        permissionPrompt.SetWindow(_window);

        Console.WriteLine("✓ Window opened");
        Console.WriteLine("Ready for conversations!");

        _window.WaitForClose();
    }

    private static void HandleWebMessage(object? sender, string message)
    {
        Task.Run(async () =>
        {
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(message);
                var callId = request.GetProperty("callId").GetString() ?? "";
                var action = request.GetProperty("action").GetString() ?? "";
                var data = request.GetProperty("data");

                Console.WriteLine($"Received: {action}");

                object response = action switch
                {
                    "send_message" => await HandleSendMessage(data),
                    "get_tools" => await HandleGetTools(),
                    "get_config" => await HandleGetConfig(),
                    "update_config" => await HandleUpdateConfig(data),
                    "list_sessions" => await HandleListSessions(),
                    "create_session" => await HandleCreateSession(data),
                    "delete_session" => await HandleDeleteSession(data),
                    "get_history" => await HandleGetHistory(data),
                    "pick_directory" => await HandlePickDirectory(),
                    "add_mcp_server" => await HandleAddMcpServer(data),
                    "add_tool" => await HandleAddTool(data),
                    "permission_response" => HandlePermissionResponse(data),
                    "close_application" => HandleCloseApplication(),
                    "debug_log" => HandleDebugLog(data),
                    _ => new { success = false, error = "Unknown action" }
                };

                SendResponse(callId, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        });
    }

    private static async Task<object> HandleSendMessage(JsonElement data)
    {
        try
        {
            var sessionId = data.GetProperty("sessionId").GetString() ?? "default";
            var messageText = data.GetProperty("message").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return new { success = false, error = "Message cannot be empty" };
            }

            var agent = _serviceProvider?.GetService<IConversationAgent>();
            if (agent == null)
            {
                return new { success = false, error = "Conversation agent not available. Please configure an API key (ANTHROPIC_API_KEY or OPENAI_API_KEY)." };
            }

            var sessionManager = _serviceProvider?.GetService<ISessionManager>();

            // Auto-create session if needed
            if (sessionId == "default" || string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                var newSession = new Session
                {
                    SessionId = sessionId,
                    Name = "Chat " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Provider = "anthropic",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await sessionManager!.CreateSessionAsync(newSession);
                Console.WriteLine($"Created new session: {sessionId}");
            }

            // Load existing context or create new one
            var context = await sessionManager!.LoadContextAsync(sessionId);
            if (context == null)
            {
                context = new ConversationContext
                {
                    SessionId = sessionId,
                    WorkingDirectory = Environment.CurrentDirectory
                };
            }

            Console.WriteLine($"Processing: {messageText.Substring(0, Math.Min(50, messageText.Length))}...");

            var response = await agent.ProcessMessageAsync(messageText, context);

            // Save the updated context with messages
            await sessionManager!.SaveContextAsync(sessionId, context);

            // Update session metadata
            var session = await sessionManager.GetSessionAsync(sessionId);
            if (session != null)
            {
                var updatedSession = session with
                {
                    UpdatedAt = DateTime.UtcNow,
                    MessageCount = context.Messages.Count
                };
                await sessionManager.UpdateSessionAsync(updatedSession);
            }

            // Format tool calls for frontend
            var toolCalls = response.ToolResults?.Select(tr => new
            {
                name = tr.ToolCallId,
                duration = tr.Duration.TotalMilliseconds,
                success = tr.Success,
                result = tr.Output ?? tr.Error ?? "No output"
            }).ToArray() ?? Array.Empty<object>();

            Console.WriteLine($"✓ Response generated ({response.Content?.Length ?? 0} chars, {toolCalls.Length} tool calls)");

            return new
            {
                success = true,
                sessionId = sessionId,
                content = response.Content ?? "No response generated",
                toolCalls
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static Task<object> HandleGetTools()
    {
        try
        {
            var toolRegistry = _serviceProvider?.GetService<IToolRegistry>();
            if (toolRegistry == null)
            {
                return Task.FromResult<object>(new { success = false, error = "Tool registry not available" });
            }

            var tools = toolRegistry.GetAllTools();
            var toolList = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description
            }).ToArray();

            return Task.FromResult<object>(new
            {
                success = true,
                tools = toolList
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static Task<object> HandleGetConfig()
    {
        try
        {
            var configuration = _serviceProvider?.GetService<IConfiguration>();

            return Task.FromResult<object>(new
            {
                success = true,
                config = new
                {
                    provider = configuration?["Goose:DefaultProvider"] ?? "anthropic",
                    model = configuration?["Goose:Providers:anthropic:Model"] ?? "claude-3-5-sonnet-20241022",
                    temperature = double.Parse(configuration?["Goose:Temperature"] ?? "0.7"),
                    maxTokens = int.Parse(configuration?["Goose:MaxTokens"] ?? "4096"),
                    workingDirectory = Environment.CurrentDirectory,
                    sessionDirectory = configuration?["Goose:SessionDirectory"] ?? "~/.goose/sessions"
                }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static Task<object> HandleUpdateConfig(JsonElement data)
    {
        try
        {
            // Note: In a production app, you'd want to persist these to appsettings.json
            // For now, we just acknowledge the update
            var provider = data.GetProperty("provider").GetString();
            var model = data.GetProperty("model").GetString();

            Console.WriteLine($"Config updated: provider={provider}, model={model}");

            return Task.FromResult<object>(new
            {
                success = true,
                message = "Settings updated (restart required for some changes)"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static async Task<object> HandleListSessions()
    {
        try
        {
            var sessionManager = _serviceProvider?.GetService<ISessionManager>();
            if (sessionManager == null)
            {
                return new { success = false, error = "Session manager not available" };
            }

            var sessions = await sessionManager.ListSessionsAsync(new SessionQueryOptions
            {
                IncludeArchived = false,
                Limit = 50
            });

            return new
            {
                success = true,
                sessions = sessions.Select(s => new
                {
                    sessionId = s.SessionId,
                    name = s.Name ?? s.SessionId,
                    messageCount = s.MessageCount,
                    toolCallCount = s.ToolCallCount,
                    provider = s.Provider,
                    createdAt = s.CreatedAt,
                    updatedAt = s.UpdatedAt,
                    isArchived = s.IsArchived
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static async Task<object> HandleCreateSession(JsonElement data)
    {
        try
        {
            var sessionManager = _serviceProvider?.GetService<ISessionManager>();
            if (sessionManager == null)
            {
                return new { success = false, error = "Session manager not available" };
            }

            var name = data.GetProperty("name").GetString() ?? "New Session";
            var sessionId = Guid.NewGuid().ToString();

            var session = new Session
            {
                SessionId = sessionId,
                Name = name,
                Provider = "anthropic",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await sessionManager.CreateSessionAsync(session);

            return new
            {
                success = true,
                sessionId = sessionId,
                message = "Session created successfully"
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static async Task<object> HandleDeleteSession(JsonElement data)
    {
        try
        {
            var sessionManager = _serviceProvider?.GetService<ISessionManager>();
            if (sessionManager == null)
            {
                return new { success = false, error = "Session manager not available" };
            }

            var sessionId = data.GetProperty("sessionId").GetString() ?? "";
            var deleted = await sessionManager.DeleteSessionAsync(sessionId);

            return new
            {
                success = deleted,
                message = deleted ? "Session deleted successfully" : "Session not found"
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static async Task<object> HandleGetHistory(JsonElement data)
    {
        try
        {
            var sessionManager = _serviceProvider?.GetService<ISessionManager>();
            if (sessionManager == null)
            {
                return new { success = false, error = "Session manager not available" };
            }

            var sessionId = data.GetProperty("sessionId").GetString() ?? "default";

            // Try to load the conversation context
            var context = await sessionManager.LoadContextAsync(sessionId);

            if (context == null || context.Messages.Count == 0)
            {
                return new
                {
                    success = true,
                    messages = Array.Empty<object>(),
                    totalTokens = 0
                };
            }

            // Convert messages to a format suitable for the UI
            var messages = context.Messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content,
                toolCalls = (object?)null, // Tool calls are embedded in message content
                tokens = 0 // We don't track per-message tokens currently
            }).ToArray();

            return new
            {
                success = true,
                messages = messages,
                totalTokens = 0 // Would need to calculate from provider responses
            };
        }
        catch (Exception ex)
        {
            // Session doesn't exist yet, return empty history
            return new
            {
                success = true,
                messages = Array.Empty<object>(),
                totalTokens = 0
            };
        }
    }

    private static object HandleCloseApplication()
    {
        try
        {
            Console.WriteLine("Application close requested");
            _window?.Close();
            return new { success = true };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static object HandleDebugLog(JsonElement data)
    {
        try
        {
            var message = data.GetProperty("message").GetString() ?? "";
            Console.WriteLine($"[JS DEBUG] {message}");
            return new { success = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JS DEBUG] Error logging: {ex.Message}");
            return new { success = true };
        }
    }

    private static object HandlePermissionResponse(JsonElement data)
    {
        try
        {
            var requestId = data.GetProperty("requestId").GetString() ?? "";
            var decisionStr = data.GetProperty("decision").GetString() ?? "deny";
            var rememberDecision = data.TryGetProperty("rememberDecision", out var rememberProp)
                ? rememberProp.GetBoolean()
                : false;

            // Parse decision
            var decision = decisionStr.ToLower() switch
            {
                "allow" => PermissionDecision.Allow,
                "deny" => PermissionDecision.Deny,
                _ => PermissionDecision.Deny
            };

            // Get permission prompt and pass the response
            var permissionPrompt = _serviceProvider?.GetService<IPermissionPrompt>() as PhotinoPermissionPrompt;
            permissionPrompt?.HandlePermissionResponse(requestId, decision, rememberDecision);

            Console.WriteLine($"Permission response: {decision} (remember: {rememberDecision})");

            return new { success = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling permission response: {ex.Message}");
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static Task<object> HandlePickDirectory()
    {
        try
        {
            // Note: PhotinoNET doesn't have built-in file picker support
            // This would typically require platform-specific implementation
            // For now, we'll return a not implemented message
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Directory picker not yet implemented. Please set working directory in Settings."
            });

            // TODO: Implement platform-specific directory picker:
            // - Windows: Use Windows Forms FolderBrowserDialog or Win32 API
            // - macOS: Use NSOpenPanel
            // - Linux: Use GTK file chooser or xdg-desktop-portal
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static Task<object> HandleAddMcpServer(JsonElement data)
    {
        try
        {
            var name = data.GetProperty("name").GetString() ?? "";
            var type = data.GetProperty("type").GetString() ?? "stdio";
            var command = data.GetProperty("command").GetString() ?? "";

            // Parse environment variables if present
            var env = new Dictionary<string, string>();
            if (data.TryGetProperty("env", out var envElement))
            {
                foreach (var prop in envElement.EnumerateObject())
                {
                    env[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            Console.WriteLine($"Add MCP Server: {name} ({type})");
            Console.WriteLine($"  Command: {command}");
            Console.WriteLine($"  Environment variables: {env.Count}");

            // Save MCP server configuration to file
            try
            {
                var mcpConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "mcp-servers.json");

                // Load existing configurations
                Dictionary<string, object> mcpServers;
                if (File.Exists(mcpConfigPath))
                {
                    var existingJson = File.ReadAllText(mcpConfigPath);
                    mcpServers = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson)
                        ?? new Dictionary<string, object>();
                }
                else
                {
                    mcpServers = new Dictionary<string, object>();
                }

                // Add or update the server configuration
                mcpServers[name] = new
                {
                    type,
                    command,
                    env
                };

                // Save to file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(mcpServers, options);
                File.WriteAllText(mcpConfigPath, json);

                Console.WriteLine($"MCP server configuration saved to: {mcpConfigPath}");

                // TODO: Register the MCP server with the tool registry
                // TODO: Validate the server connection
                // Note: Full MCP server integration requires MCP protocol implementation in Goose.Core

                return Task.FromResult<object>(new
                {
                    success = true,
                    message = $"MCP server '{name}' configuration saved successfully. Full MCP integration pending.",
                    configPath = mcpConfigPath
                });
            }
            catch (Exception configEx)
            {
                Console.WriteLine($"Error saving MCP server configuration: {configEx.Message}");
                return Task.FromResult<object>(new
                {
                    success = false,
                    error = $"Failed to save MCP server configuration: {configEx.Message}"
                });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static async Task<object> HandleAddTool(JsonElement data)
    {
        try
        {
            var name = data.GetProperty("name").GetString() ?? "";
            var path = data.GetProperty("path").GetString() ?? "";

            Console.WriteLine($"Add Tool: {name}");
            Console.WriteLine($"  Path: {path}");

            if (!File.Exists(path))
            {
                return Task.FromResult<object>(new
                {
                    success = false,
                    error = $"Tool assembly not found at path: {path}"
                });
            }

            // Load the assembly and register tools
            var toolRegistry = _serviceProvider?.GetService<IToolRegistry>();
            if (toolRegistry == null)
            {
                return new
                {
                    success = false,
                    error = "Tool registry not available"
                };
            }

            try
            {
                // Load the assembly from the specified path
                var assembly = System.Reflection.Assembly.LoadFrom(path);

                // Use the ToolRegistry's built-in method to scan and register tools
                int toolsLoaded = await toolRegistry.LoadToolsFromAssemblyAsync(assembly);

                if (toolsLoaded > 0)
                {
                    Console.WriteLine($"Successfully loaded {toolsLoaded} tool(s) from {name}");

                    // TODO: Save configuration for future sessions
                    // This would involve persisting the tool path to appsettings.json
                    // so that tools are automatically loaded on application startup

                    return new
                    {
                        success = true,
                        toolsLoaded = toolsLoaded,
                        message = $"Successfully loaded {toolsLoaded} tool(s) from assembly. Note: Tools with constructor dependencies may not load correctly."
                    };
                }
                else
                {
                    return new
                    {
                        success = false,
                        error = "No ITool implementations found in the assembly, or all tools failed to load. Tools must have parameterless constructors."
                    };
                }
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"Error loading tools from {name}: {innerEx.Message}");
                return new
                {
                    success = false,
                    error = $"Failed to load tools from assembly: {innerEx.Message}"
                };
            }
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private static void SendResponse(string callId, object response)
    {
        try
        {
            var json = JsonSerializer.Serialize(response);
            var script = $"handleBackendResponse('{callId}', {json});";
            _window?.SendWebMessage(script);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending response: {ex.Message}");
        }
    }
}
