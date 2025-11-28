using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using PhotinoNET;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Goose.GUI;

/// <summary>
/// Permission prompt implementation for Photino-based GUI
/// </summary>
public class PhotinoPermissionPrompt : IPermissionPrompt
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PermissionResponse>> _pendingRequests = new();
    private PhotinoWindow? _window;

    /// <summary>
    /// Sets the Photino window instance for sending messages
    /// </summary>
    public void SetWindow(PhotinoWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>
    /// Handles permission response from the frontend
    /// </summary>
    public void HandlePermissionResponse(string requestId, PermissionDecision decision, bool rememberDecision)
    {
        if (_pendingRequests.TryRemove(requestId, out var tcs))
        {
            tcs.SetResult(new PermissionResponse
            {
                Decision = decision,
                RememberDecision = rememberDecision
            });
        }
    }

    /// <summary>
    /// Prompts the user to approve or deny a tool execution
    /// </summary>
    public async Task<(PermissionDecision Decision, bool RememberDecision)> PromptUserAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        InspectionResult inspectionResult,
        CancellationToken cancellationToken = default)
    {
        if (_window == null)
        {
            // Fallback to auto-allow if window is not set (shouldn't happen in production)
            return (PermissionDecision.Allow, false);
        }

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<PermissionResponse>();
        _pendingRequests[requestId] = tcs;

        try
        {
            // Build permission request data
            var requestData = new
            {
                requestId,
                toolName = toolCall.Name,
                parameters = toolCall.Parameters,
                riskLevel = riskLevel.ToString(),
                threats = inspectionResult.Threats.Select(t => new
                {
                    type = t.Type.ToString(),
                    level = t.Level.ToString(),
                    description = t.Description,
                    recommendation = t.Recommendation
                }).ToArray(),
                threatLevel = inspectionResult.ThreatLevel.ToString(),
                isSafe = inspectionResult.IsSafe
            };

            // Send message to frontend
            var json = JsonSerializer.Serialize(requestData);
            var script = $"showPermissionDialog({json});";
            _window.SendWebMessage(script);

            // Wait for response with cancellation support
            using var registration = cancellationToken.Register(() =>
            {
                if (_pendingRequests.TryRemove(requestId, out var cancelledTcs))
                {
                    cancelledTcs.SetCanceled();
                }
            });

            var response = await tcs.Task;
            return (response.Decision, response.RememberDecision);
        }
        catch (OperationCanceledException)
        {
            // If cancelled, deny by default
            return (PermissionDecision.Deny, false);
        }
        catch (Exception)
        {
            // On any error, deny by default for safety
            _pendingRequests.TryRemove(requestId, out _);
            return (PermissionDecision.Deny, false);
        }
    }

    /// <summary>
    /// Internal response structure
    /// </summary>
    private class PermissionResponse
    {
        public PermissionDecision Decision { get; set; }
        public bool RememberDecision { get; set; }
    }
}
