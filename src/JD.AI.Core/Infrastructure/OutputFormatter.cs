// Licensed under the MIT License.

using System.Text;

namespace JD.AI.Core.Infrastructure;

/// <summary>
/// Centralized output formatting for tool responses. Provides consistent
/// error messages, code blocks, truncation, and markdown formatting.
/// </summary>
public static class OutputFormatter
{
    /// <summary>Default maximum output length before truncation.</summary>
    public const int DefaultMaxLength = 100_000;

    /// <summary>Formats a standard error message.</summary>
    public static string Error(string message) => $"Error: {message}";

    /// <summary>Formats an error with a context prefix.</summary>
    public static string Error(string context, string details) => $"Error: {context}: {details}";

    /// <summary>Formats an error from an exception.</summary>
    public static string Error(string context, Exception ex) => $"Error: {context}: {ex.Message}";

    /// <summary>Formats content as a fenced code block.</summary>
    public static string CodeBlock(string content, string? language = null) =>
        $"```{language ?? string.Empty}\n{content}\n```";

    /// <summary>Formats content as a JSON code block.</summary>
    public static string JsonBlock(string json) => CodeBlock(json, "json");

    /// <summary>Formats a success message with an emoji prefix.</summary>
    public static string Success(string message) => $"✅ {message}";

    /// <summary>Formats a warning message with an emoji prefix.</summary>
    public static string Warning(string message) => $"⚠️ {message}";

    /// <summary>Formats an informational message.</summary>
    public static string Info(string message) => $"ℹ️ {message}";

    /// <summary>
    /// Truncates text to a maximum length, appending a truncation notice.
    /// </summary>
    public static string Truncate(string text, int maxLength = DefaultMaxLength)
    {
        if (text.Length <= maxLength)
            return text;

        var remaining = text.Length - maxLength;
        return $"{text[..maxLength]}\n...[truncated, {remaining:N0} chars remaining]";
    }

    /// <summary>
    /// Formats a markdown table from rows of key-value data.
    /// </summary>
    public static string MarkdownTable(IEnumerable<(string Key, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Key | Value |");
        sb.AppendLine("| --- | --- |");
        foreach (var (key, value) in rows)
            sb.AppendLine($"| {key} | {value} |");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a markdown table with custom headers and row data.
    /// </summary>
    public static string MarkdownTable(string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"| {string.Join(" | ", headers)} |");
        sb.AppendLine($"| {string.Join(" | ", headers.Select(_ => "---"))} |");
        foreach (var row in rows)
            sb.AppendLine($"| {string.Join(" | ", row)} |");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a bullet list from items.
    /// </summary>
    public static string BulletList(IEnumerable<string> items) =>
        string.Join('\n', items.Select(i => $"- {i}"));

    /// <summary>
    /// Formats a process result into a tool-friendly string.
    /// </summary>
    public static string FromProcessResult(ProcessResult result, string? context = null)
    {
        if (result.Success)
            return result.StandardOutput;

        var prefix = context is not null ? $"{context}: " : string.Empty;
        return string.IsNullOrWhiteSpace(result.StandardError)
            ? Error($"{prefix}exit code {result.ExitCode}")
            : Error($"{prefix}exit {result.ExitCode}", result.StandardError);
    }
}
