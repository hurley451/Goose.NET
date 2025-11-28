using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Microsoft.Extensions.Logging;

namespace Goose.Core.Services;

/// <summary>
/// Coordinates the permission system components
/// </summary>
public class PermissionSystem : IPermissionSystem
{
    private readonly ILogger<PermissionSystem> _logger;
    private readonly IPermissionInspector _inspector;
    private readonly IPermissionJudge _judge;
    private readonly IPermissionStore _store;
    private readonly IToolClassifier _classifier;

    /// <summary>
    /// Creates a new permission system instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="inspector">Security inspector</param>
    /// <param name="judge">Permission judge</param>
    /// <param name="store">Permission store</param>
    /// <param name="classifier">Tool classifier</param>
    public PermissionSystem(
        ILogger<PermissionSystem> logger,
        IPermissionInspector inspector,
        IPermissionJudge judge,
        IPermissionStore store,
        IToolClassifier classifier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _judge = judge ?? throw new ArgumentNullException(nameof(judge));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    }

    /// <summary>
    /// Requests permission to execute a tool
    /// </summary>
    /// <param name="toolCall">The tool call requesting execution</param>
    /// <param name="riskLevel">The tool's risk level</param>
    /// <param name="context">The execution context</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Permission response indicating whether to allow execution</returns>
    public async Task<PermissionResponse> RequestPermissionAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        ToolContext context,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (toolCall == null)
            throw new ArgumentNullException(nameof(toolCall));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        _logger.LogInformation(
            "Processing permission request for tool '{ToolName}' in session '{SessionId}'",
            toolCall.Name,
            sessionId);

        // Step 1: Check if we have a saved permission for this tool
        var savedDecision = await _store.GetPermissionAsync(sessionId, toolCall.Name, cancellationToken);
        if (savedDecision.HasValue)
        {
            _logger.LogInformation(
                "Using saved permission for tool '{ToolName}': {Decision}",
                toolCall.Name,
                savedDecision.Value);

            return new PermissionResponse
            {
                Decision = savedDecision.Value,
                RememberDecision = false // Already saved
            };
        }

        // Step 2: Inspect the tool call for security threats
        var inspectionResult = await _inspector.InspectAsync(toolCall, context, cancellationToken);

        // Step 3: Create permission request
        var request = new PermissionRequest
        {
            ToolCall = toolCall,
            RiskLevel = riskLevel,
            InspectionResult = inspectionResult,
            SessionId = sessionId
        };

        // Step 4: Let the judge decide
        var decision = await _judge.EvaluateAsync(request, cancellationToken);

        _logger.LogInformation(
            "Permission decision for tool '{ToolName}': {Decision} (Risk: {Risk}, Threats: {ThreatCount})",
            toolCall.Name,
            decision,
            riskLevel,
            inspectionResult.Threats.Count);

        // Step 5: Return response
        // Note: Actual "Ask" decision handling will be done by the caller
        // (ConversationAgent will prompt user and call SavePermissionAsync if needed)
        return new PermissionResponse
        {
            Decision = decision,
            RememberDecision = false // Caller will decide whether to remember
        };
    }

    /// <summary>
    /// Checks if a tool has been previously approved
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if previously approved, false otherwise</returns>
    public async Task<bool> IsApprovedAsync(
        string toolName,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        var savedDecision = await _store.GetPermissionAsync(sessionId, toolName, cancellationToken);

        var isApproved = savedDecision == PermissionDecision.Allow;

        _logger.LogDebug(
            "Checked approval status for tool '{ToolName}' in session '{SessionId}': {IsApproved}",
            toolName,
            sessionId,
            isApproved);

        return isApproved;
    }
}
