using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using System.Text.Json;

namespace Goose.CLI;

/// <summary>
/// Console-based permission prompt for CLI applications
/// </summary>
public class ConsolePermissionPrompt : IPermissionPrompt
{
    /// <summary>
    /// Prompts the user to approve or deny a tool execution via console
    /// </summary>
    public Task<(PermissionDecision Decision, bool RememberDecision)> PromptUserAsync(
        ToolCall toolCall,
        ToolRiskLevel riskLevel,
        InspectionResult inspectionResult,
        CancellationToken cancellationToken = default)
    {
        // Display header
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  PERMISSION REQUIRED");
        Console.ResetColor();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine();

        // Display tool information
        Console.WriteLine($"Tool:       {toolCall.Name}");
        Console.WriteLine($"Risk Level: {GetRiskLevelDisplay(riskLevel)}");
        Console.WriteLine();

        // Display parameters if not empty
        if (!string.IsNullOrWhiteSpace(toolCall.Parameters) && toolCall.Parameters != "{}")
        {
            Console.WriteLine("Parameters:");
            try
            {
                var formatted = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<JsonElement>(toolCall.Parameters),
                    new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(formatted);
            }
            catch
            {
                Console.WriteLine(toolCall.Parameters);
            }
            Console.WriteLine();
        }

        // Display threats if any
        if (inspectionResult.Threats.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"⚠️  {inspectionResult.Threats.Count} Security Threat(s) Detected:");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var threat in inspectionResult.Threats)
            {
                Console.ForegroundColor = GetThreatLevelColor(threat.Level);
                Console.WriteLine($"  [{threat.Level}] {threat.Type}");
                Console.ResetColor();
                Console.WriteLine($"  Description: {threat.Description}");

                if (!string.IsNullOrWhiteSpace(threat.Recommendation))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  💡 {threat.Recommendation}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
        }

        // Display overall threat level
        if (inspectionResult.ThreatLevel != ThreatLevel.None)
        {
            Console.ForegroundColor = GetThreatLevelColor(inspectionResult.ThreatLevel);
            Console.WriteLine($"Overall Threat Level: {inspectionResult.ThreatLevel}");
            Console.ResetColor();
            Console.WriteLine();
        }

        // Prompt for decision
        Console.WriteLine(new string('-', 70));
        Console.WriteLine();
        Console.Write("Allow this tool to execute? [y/N]: ");

        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        var decision = response == "y" || response == "yes"
            ? PermissionDecision.Allow
            : PermissionDecision.Deny;

        bool rememberDecision = false;
        if (decision == PermissionDecision.Allow || decision == PermissionDecision.Deny)
        {
            Console.Write("Remember this decision for this tool? [y/N]: ");
            var rememberResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
            rememberDecision = rememberResponse == "y" || rememberResponse == "yes";
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine();

        // Display decision
        if (decision == PermissionDecision.Allow)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Permission granted");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Permission denied");
        }
        Console.ResetColor();
        Console.WriteLine();

        return Task.FromResult((decision, rememberDecision));
    }

    private static string GetRiskLevelDisplay(ToolRiskLevel riskLevel)
    {
        var originalColor = Console.ForegroundColor;

        var display = riskLevel switch
        {
            ToolRiskLevel.ReadOnly => SetColorAndReturn(ConsoleColor.Green, "ReadOnly (Safe)"),
            ToolRiskLevel.ReadWrite => SetColorAndReturn(ConsoleColor.Yellow, "ReadWrite (Moderate)"),
            ToolRiskLevel.Destructive => SetColorAndReturn(ConsoleColor.Red, "Destructive (High Risk)"),
            ToolRiskLevel.Critical => SetColorAndReturn(ConsoleColor.Magenta, "Critical (VERY HIGH RISK)"),
            _ => SetColorAndReturn(originalColor, riskLevel.ToString())
        };

        Console.ForegroundColor = originalColor;
        return display;

        static string SetColorAndReturn(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            return text;
        }
    }

    private static ConsoleColor GetThreatLevelColor(ThreatLevel level)
    {
        return level switch
        {
            ThreatLevel.None => ConsoleColor.Gray,
            ThreatLevel.Low => ConsoleColor.Green,
            ThreatLevel.Medium => ConsoleColor.Yellow,
            ThreatLevel.High => ConsoleColor.Red,
            ThreatLevel.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };
    }
}
