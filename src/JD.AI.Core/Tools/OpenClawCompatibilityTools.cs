using System.ComponentModel;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// OpenClaw-compatible tool aliases and shared envelope parameters.
/// </summary>
[ToolPlugin("openclaw", RequiresInjection = true)]
public sealed class OpenClawCompatibilityTools
{
    private readonly TaskTools _tasks;
    private readonly WebSearchTools _webSearch;

    public OpenClawCompatibilityTools(TaskTools tasks, WebSearchTools webSearch)
    {
        _tasks = tasks;
        _webSearch = webSearch;
    }

    [KernelFunction("bash")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for run_command.")]
    public async Task<string> BashAsync(
        [Description("The command to execute")] string command,
        [Description("Working directory (optional)")] string? cwd = null,
        [Description("Optional timeout in milliseconds")] int? timeoutMs = null,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var timeoutSeconds = timeoutMs is > 0
            ? Math.Max(1, (int)Math.Ceiling(timeoutMs.Value / 1000d))
            : 60;

        var result = await ShellTools.RunCommandAsync(command, cwd, timeoutSeconds).ConfigureAwait(false);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("read")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for read_file.")]
    public string Read(
        [Description("Absolute or relative file path")] string path,
        [Description("Optional start line (1-based)")] int? startLine = null,
        [Description("Optional end line (1-based, -1 for EOF)")] int? endLine = null,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = FileTools.ReadFile(path, startLine, endLine);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("write")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for write_file.")]
    public string Write(
        [Description("Absolute or relative file path")] string path,
        [Description("Content to write")] string content,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = FileTools.WriteFile(path, content);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("edit")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for edit_file.")]
    public string Edit(
        [Description("Absolute or relative file path")] string path,
        [Description("The exact string to find and replace")] string oldStr,
        [Description("The replacement string")] string newStr,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = FileTools.EditFile(path, oldStr, newStr);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("ls")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for list_directory.")]
    public string Ls(
        [Description("Directory path (optional)")] string? path = null,
        [Description("Maximum depth to recurse (default 2)")] int maxDepth = 2,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = FileTools.ListDirectory(path, maxDepth);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("webfetch")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for web_fetch.")]
    public async Task<string> WebFetchAsync(
        [Description("The URL to fetch")] string url,
        [Description("Maximum characters to return from the fetch call")] int maxLength = 5000,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = await WebTools.WebFetchAsync(url, maxLength).ConfigureAwait(false);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("websearch")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for web_search.")]
    public async Task<string> WebSearchAsync(
        [Description("Search query")] string query,
        [Description("Number of results to return (default 5, max 10)")] int count = 5,
        [Description("Optional timeout in milliseconds")] int? timeoutMs = null,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false,
        CancellationToken ct = default)
    {
        using var timeoutCts = timeoutMs is > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;

        if (timeoutCts is not null)
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs!.Value));

        var token = timeoutCts?.Token ?? ct;
        string result;
        try
        {
            result = await _webSearch.SearchAsync(query, count, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts is not null && !ct.IsCancellationRequested)
        {
            result = $"Error: websearch timed out after {timeoutMs}ms.";
        }

        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("todo_read")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible alias for list_tasks.")]
    public string TodoRead(
        [Description("Optional status filter: pending, in_progress, done, blocked")] string? status = null,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = _tasks.ListTasks(status);
        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    [KernelFunction("todo_write")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("OpenClaw-compatible todo mutator. Supports create, update, and complete actions.")]
    public string TodoWrite(
        [Description("Action: create, update, complete")] string action,
        [Description("Task ID for update/complete")] string? id = null,
        [Description("Title for create/update")] string? title = null,
        [Description("Description for create/update")] string? description = null,
        [Description("Priority for create (low, medium, high)")] string priority = "medium",
        [Description("Status for update")] string? status = null,
        [Description("Return a compact summary instead of full output")] bool summary = false,
        [Description("Optional max result length in characters")] int? maxResultChars = null,
        [Description("Exclude output from model context and persistence")] bool noContext = false,
        [Description("Compatibility flag; streaming is currently not used for tool outputs")] bool noStream = false)
    {
        var result = action.Trim().ToLowerInvariant() switch
        {
            "create" => _tasks.CreateTask(
                string.IsNullOrWhiteSpace(title) ? "untitled task" : title,
                description,
                priority),
            "update" => string.IsNullOrWhiteSpace(id)
                ? "Error: todo_write action 'update' requires 'id'."
                : _tasks.UpdateTask(id, status, title, description),
            "complete" => string.IsNullOrWhiteSpace(id)
                ? "Error: todo_write action 'complete' requires 'id'."
                : _tasks.CompleteTask(id),
            _ => "Error: unsupported todo_write action. Use create, update, or complete.",
        };

        return ApplyEnvelope(result, summary, maxResultChars, noContext, noStream);
    }

    internal static string ApplyEnvelope(
        string rawResult,
        bool summary,
        int? maxResultChars,
        bool noContext,
        bool noStream)
    {
        _ = noStream;

        if (noContext)
            return "Result hidden from context because noContext=true.";

        var result = rawResult ?? string.Empty;
        if (summary)
            result = Summarize(result);

        if (maxResultChars is > 0 && result.Length > maxResultChars.Value)
        {
            var keep = maxResultChars.Value;
            result = string.Concat(
                result.AsSpan(0, keep),
                $"\n... [truncated, {result.Length - keep} more chars]");
        }

        return result;
    }

    private static string Summarize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        const int maxLines = 8;
        var lines = value
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(static l => !string.IsNullOrWhiteSpace(l))
            .Take(maxLines)
            .ToList();

        if (lines.Count == 0)
            return value.Length > 240 ? $"{value[..240]}..." : value;

        return string.Join(Environment.NewLine, lines);
    }
}
