using Goose.Core.Models;
using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Analyzes tool capabilities and classifies their risk levels
/// </summary>
public interface IToolClassifier
{
    /// <summary>
    /// Analyzes a tool call and determines its effective risk level
    /// </summary>
    /// <param name="toolCall">The tool call to analyze</param>
    /// <param name="tool">The tool being invoked</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The classified risk level for this specific invocation</returns>
    Task<ToolRiskLevel> ClassifyAsync(
        ToolCall toolCall,
        ITool tool,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
