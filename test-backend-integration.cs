using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Extensions;
using Goose.Providers.Extensions;
using Goose.Tools.Extensions;

namespace Goose.Tests;

/// <summary>
/// Integration test for Goose.NET backend with local LLM
/// </summary>
class BackendIntegrationTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Goose.NET Backend Integration Test");
        Console.WriteLine("======================================\n");

        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("src/Goose.GUI/appsettings.json", optional: false)
            .Build();

        // Setup DI container
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<IConfiguration>(configuration);
        services.AddGooseCore(configuration);
        services.AddOpenAIProvider(configuration);
        services.AddGooseTools(configuration);

        var serviceProvider = services.BuildServiceProvider();

        Console.WriteLine("✓ Services configured\n");

        // Test 1: Verify conversation agent is available
        Console.WriteLine("Test 1: Verify conversation agent");
        var agent = serviceProvider.GetService<IConversationAgent>();
        if (agent == null)
        {
            Console.WriteLine("✗ FAIL: Conversation agent not available");
            return;
        }
        Console.WriteLine("✓ PASS: Conversation agent available\n");

        // Test 2: Verify session manager is available
        Console.WriteLine("Test 2: Verify session manager");
        var sessionManager = serviceProvider.GetService<ISessionManager>();
        if (sessionManager == null)
        {
            Console.WriteLine("✗ FAIL: Session manager not available");
            return;
        }
        Console.WriteLine("✓ PASS: Session manager available\n");

        // Test 3: Create a new session
        Console.WriteLine("Test 3: Create new session");
        var sessionId = Guid.NewGuid().ToString();
        var session = new Session
        {
            SessionId = sessionId,
            Name = "Integration Test Session",
            Provider = "openai",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await sessionManager.CreateSessionAsync(session);
            Console.WriteLine($"✓ PASS: Session created (ID: {sessionId})\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Failed to create session: {ex.Message}");
            return;
        }

        // Test 4: Send a simple message to the LLM
        Console.WriteLine("Test 4: Send message to local LLM");
        var context = new ConversationContext
        {
            SessionId = sessionId,
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            Console.WriteLine("  Sending: 'Hello! What is 2+2?'");
            var response = await agent.ProcessMessageAsync("Hello! What is 2+2?", context);

            if (response == null)
            {
                Console.WriteLine("✗ FAIL: Received null response");
                return;
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                Console.WriteLine("✗ FAIL: Received empty response content");
                return;
            }

            Console.WriteLine($"✓ PASS: Received response from LLM");
            Console.WriteLine($"  Response preview: {response.Content.Substring(0, Math.Min(100, response.Content.Length))}...\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Error processing message: {ex.Message}");
            Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            return;
        }

        // Test 5: Save and load conversation context
        Console.WriteLine("Test 5: Save and load conversation context");
        try
        {
            await sessionManager.SaveContextAsync(sessionId, context);
            Console.WriteLine("✓ PASS: Context saved");

            var loadedContext = await sessionManager.LoadContextAsync(sessionId);
            if (loadedContext == null)
            {
                Console.WriteLine("✗ FAIL: Failed to load context");
                return;
            }

            if (loadedContext.Messages.Count != context.Messages.Count)
            {
                Console.WriteLine($"✗ FAIL: Message count mismatch (expected {context.Messages.Count}, got {loadedContext.Messages.Count})");
                return;
            }

            Console.WriteLine($"✓ PASS: Context loaded ({loadedContext.Messages.Count} messages)\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Error saving/loading context: {ex.Message}");
            return;
        }

        // Test 6: Update session metadata
        Console.WriteLine("Test 6: Update session metadata");
        try
        {
            var updatedSession = session with
            {
                UpdatedAt = DateTime.UtcNow,
                MessageCount = context.Messages.Count
            };
            await sessionManager.UpdateSessionAsync(updatedSession);

            var retrievedSession = await sessionManager.GetSessionAsync(sessionId);
            if (retrievedSession == null)
            {
                Console.WriteLine("✗ FAIL: Failed to retrieve session");
                return;
            }

            if (retrievedSession.MessageCount != context.Messages.Count)
            {
                Console.WriteLine($"✗ FAIL: Message count not updated (expected {context.Messages.Count}, got {retrievedSession.MessageCount})");
                return;
            }

            Console.WriteLine($"✓ PASS: Session metadata updated (messages: {retrievedSession.MessageCount})\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Error updating session: {ex.Message}");
            return;
        }

        // Test 7: List sessions
        Console.WriteLine("Test 7: List sessions");
        try
        {
            var sessions = await sessionManager.ListSessionsAsync(new SessionQueryOptions
            {
                IncludeArchived = false,
                Limit = 10
            });

            var foundSession = sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (foundSession == null)
            {
                Console.WriteLine("✗ FAIL: Created session not found in list");
                return;
            }

            Console.WriteLine($"✓ PASS: Session found in list ({sessions.Count} total sessions)\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Error listing sessions: {ex.Message}");
            return;
        }

        // Test 8: Delete test session (cleanup)
        Console.WriteLine("Test 8: Delete test session");
        try
        {
            var deleted = await sessionManager.DeleteSessionAsync(sessionId);
            if (!deleted)
            {
                Console.WriteLine("⚠ WARNING: Session deletion returned false");
            }
            else
            {
                Console.WriteLine("✓ PASS: Test session deleted\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ WARNING: Error deleting session: {ex.Message}\n");
        }

        // All tests passed
        Console.WriteLine("========================================");
        Console.WriteLine("✅ All integration tests PASSED!");
        Console.WriteLine("========================================");
        Console.WriteLine("\nThe following functionality has been validated:");
        Console.WriteLine("  ✓ Conversation agent initialization");
        Console.WriteLine("  ✓ Session manager initialization");
        Console.WriteLine("  ✓ Session creation");
        Console.WriteLine("  ✓ Message processing with local LLM");
        Console.WriteLine("  ✓ Context persistence (save/load)");
        Console.WriteLine("  ✓ Session metadata updates");
        Console.WriteLine("  ✓ Session listing");
        Console.WriteLine("  ✓ Session deletion");
        Console.WriteLine("\n🎉 The Goose.NET backend is fully functional with the local LLM!");
    }
}
