using Goose.Core.Abstractions;
using Goose.Core.Models.Permissions;
using Goose.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goose.Core.Services;

/// <summary>
/// Makes decisions about whether to allow tool executions
/// </summary>
public class PermissionJudge : IPermissionJudge
{
    private readonly ILogger<PermissionJudge> _logger;
    private readonly PermissionOptions _options;

    /// <summary>
    /// Creates a new permission judge instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="options">Permission configuration options</param>
    public PermissionJudge(
        ILogger<PermissionJudge> logger,
        IOptions<PermissionOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PermissionOptions();
    }

    /// <summary>
    /// Evaluates a permission request and decides whether to allow it
    /// </summary>
    /// <param name="request">The permission request to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decision on whether to allow, deny, or ask the user</returns>
    public Task<PermissionDecision> EvaluateAsync(
        PermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = _options.Mode;

        _logger.LogDebug(
            "Evaluating permission for tool '{ToolName}' with mode '{Mode}', risk level '{RiskLevel}', and threat level '{ThreatLevel}'",
            request.ToolCall.Name,
            mode,
            request.RiskLevel,
            request.InspectionResult.ThreatLevel);

        // If inspection found critical threats, escalate to Ask (unless mode is Deny)
        if (request.InspectionResult.ThreatLevel >= ThreatLevel.Critical && mode != PermissionMode.Deny)
        {
            _logger.LogWarning(
                "Critical threat detected for tool '{ToolName}', escalating to user approval",
                request.ToolCall.Name);
            return Task.FromResult(PermissionDecision.Ask);
        }

        // If inspection found high threats, escalate to Ask (unless mode is Deny or Auto)
        if (request.InspectionResult.ThreatLevel >= ThreatLevel.High &&
            mode != PermissionMode.Deny &&
            mode != PermissionMode.Auto)
        {
            _logger.LogWarning(
                "High threat detected for tool '{ToolName}', escalating to user approval",
                request.ToolCall.Name);
            return Task.FromResult(PermissionDecision.Ask);
        }

        // Evaluate based on configured mode
        var decision = mode switch
        {
            PermissionMode.Auto => EvaluateAutoMode(request),
            PermissionMode.Deny => EvaluateDenyMode(request),
            PermissionMode.Ask => EvaluateAskMode(request),
            PermissionMode.SmartApprove => EvaluateSmartApproveMode(request),
            _ => PermissionDecision.Ask // Default to asking user
        };

        _logger.LogInformation(
            "Permission decision for tool '{ToolName}': {Decision} (mode: {Mode}, risk: {Risk}, threats: {Threats})",
            request.ToolCall.Name,
            decision,
            mode,
            request.RiskLevel,
            request.InspectionResult.Threats.Count);

        return Task.FromResult(decision);
    }

    /// <summary>
    /// Evaluates request in Auto mode - allow everything
    /// </summary>
    /// <param name="request">The permission request</param>
    /// <returns>Permission decision</returns>
    private PermissionDecision EvaluateAutoMode(PermissionRequest request)
    {
        _logger.LogDebug("Auto mode: Allowing tool '{ToolName}'", request.ToolCall.Name);
        return PermissionDecision.Allow;
    }

    /// <summary>
    /// Evaluates request in Deny mode - deny everything
    /// </summary>
    /// <param name="request">The permission request</param>
    /// <returns>Permission decision</returns>
    private PermissionDecision EvaluateDenyMode(PermissionRequest request)
    {
        _logger.LogDebug("Deny mode: Denying tool '{ToolName}'", request.ToolCall.Name);
        return PermissionDecision.Deny;
    }

    /// <summary>
    /// Evaluates request in Ask mode - ask user for everything
    /// </summary>
    /// <param name="request">The permission request</param>
    /// <returns>Permission decision</returns>
    private PermissionDecision EvaluateAskMode(PermissionRequest request)
    {
        _logger.LogDebug("Ask mode: Requesting user approval for tool '{ToolName}'", request.ToolCall.Name);
        return PermissionDecision.Ask;
    }

    /// <summary>
    /// Evaluates request in SmartApprove mode - auto-approve safe operations, ask for risky ones
    /// </summary>
    /// <param name="request">The permission request</param>
    /// <returns>Permission decision</returns>
    private PermissionDecision EvaluateSmartApproveMode(PermissionRequest request)
    {
        // Auto-approve if:
        // 1. Risk level is ReadOnly AND
        // 2. No security threats detected
        if (request.RiskLevel == ToolRiskLevel.ReadOnly && request.InspectionResult.IsSafe)
        {
            _logger.LogDebug(
                "SmartApprove mode: Auto-approving safe ReadOnly tool '{ToolName}'",
                request.ToolCall.Name);
            return PermissionDecision.Allow;
        }

        // For ReadWrite operations with no threats, allow if configured
        if (request.RiskLevel == ToolRiskLevel.ReadWrite &&
            request.InspectionResult.IsSafe &&
            _options.AutoApproveReadWrite)
        {
            _logger.LogDebug(
                "SmartApprove mode: Auto-approving safe ReadWrite tool '{ToolName}'",
                request.ToolCall.Name);
            return PermissionDecision.Allow;
        }

        // Ask for everything else (Destructive, Critical, or if threats detected)
        _logger.LogDebug(
            "SmartApprove mode: Requesting user approval for tool '{ToolName}' (Risk: {Risk}, Safe: {Safe})",
            request.ToolCall.Name,
            request.RiskLevel,
            request.InspectionResult.IsSafe);
        return PermissionDecision.Ask;
    }
}
