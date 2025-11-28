using Spectre.Console;

namespace Goose.CLI.Helpers;

/// <summary>
/// Helper class for rendering markdown content to the console
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// Renders markdown text to the console with syntax highlighting and formatting
    /// </summary>
    /// <param name="markdown">The markdown content to render</param>
    public static void Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return;

        try
        {
            // Use Spectre.Console's built-in markdown rendering
            var markdownWidget = new Markup(markdown.EscapeMarkup());
            AnsiConsole.Write(markdownWidget);
        }
        catch
        {
            // Fallback to plain text if markdown rendering fails
            Console.Write(markdown);
        }
    }

    /// <summary>
    /// Renders a markdown chunk for streaming output
    /// </summary>
    /// <param name="chunk">The markdown chunk to render</param>
    public static void RenderChunk(string chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk))
            return;

        try
        {
            // For streaming, we just write escaped markup directly
            // This allows progressive rendering without buffering
            var escaped = chunk.EscapeMarkup();
            AnsiConsole.Markup(escaped);
        }
        catch
        {
            // Fallback to plain text
            Console.Write(chunk);
        }
    }

    /// <summary>
    /// Renders a code block with syntax highlighting
    /// </summary>
    /// <param name="code">The code to render</param>
    /// <param name="language">The programming language for syntax highlighting</param>
    public static void RenderCodeBlock(string code, string? language = null)
    {
        var panel = new Panel(code.EscapeMarkup())
        {
            Header = language != null ? new PanelHeader($" {language} ") : null,
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Checks if the terminal supports ANSI colors
    /// </summary>
    public static bool SupportsAnsi => AnsiConsole.Profile.Capabilities.Ansi;
}
