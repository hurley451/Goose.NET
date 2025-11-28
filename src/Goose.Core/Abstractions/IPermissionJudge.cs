using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Makes decisions about whether to allow tool executions
/// </summary>
public interface IPermissionJudge
{
    /// <summary>
    /// Evaluates a permission request and decides whether to allow it
    /// </summary>
    /// <param name="request">The permission request to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decision on whether to allow, deny, or ask the user</returns>
    Task<PermissionDecision> EvaluateAsync(
        PermissionRequest request,
        CancellationToken cancellationToken = default);
}
