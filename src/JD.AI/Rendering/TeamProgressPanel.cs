using System.Collections.Concurrent;
using JD.AI.Tui.Agent.Orchestration;
using Spectre.Console;

namespace JD.AI.Tui.Rendering;

/// <summary>
/// Live progress panel for team execution — shows per-agent status in real-time.
/// Uses Spectre.Console LiveDisplay for smooth updates.
/// </summary>
public sealed class TeamProgressPanel : IDisposable
{
    private readonly ConcurrentDictionary<string, SubagentProgress> _agentStates = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// Handle a progress event from a subagent — updates the internal state.
    /// </summary>
    public void OnProgress(SubagentProgress progress) =>
        _agentStates[progress.AgentName] = progress;

    /// <summary>
    /// Render the current state as a Spectre.Console panel.
    /// </summary>
    public Panel Render()
    {
        var rows = new List<Markup>();

        foreach (var (name, state) in _agentStates.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var icon = GetStatusIcon(state.Status);
            var detail = state.Detail ?? state.Status.ToString();
            var elapsed = state.Elapsed.HasValue ? $" ({state.Elapsed.Value.TotalSeconds:F1}s)" : "";
            var tokens = state.TokensUsed.HasValue ? $" {state.TokensUsed}tok" : "";
            var color = GetStatusColor(state.Status);

            rows.Add(new Markup($"[{color}]{icon} {Markup.Escape(name),-20} {Markup.Escape(detail)}{elapsed}{tokens}[/]"));
        }

        if (rows.Count == 0)
        {
            rows.Add(new Markup("[dim]Waiting for agents...[/]"));
        }

        var grid = new Rows(rows);
        return new Panel(grid)
            .Header("[bold]Team Execution[/]")
            .Expand()
            .BorderColor(Color.Blue);
    }

    /// <summary>
    /// Render a static summary after team completion.
    /// </summary>
    public static void RenderSummary(TeamResult result)
    {
        var table = new Table()
            .AddColumn("Agent")
            .AddColumn("Status")
            .AddColumn("Duration")
            .AddColumn("Tokens")
            .BorderColor(Color.Grey);

        foreach (var (name, agentResult) in result.AgentResults)
        {
            var status = agentResult.Success ? "[green]✅ OK[/]" : "[red]❌ Failed[/]";
            table.AddRow(
                new Markup(Markup.Escape(name)),
                new Markup(status),
                new Markup($"{agentResult.Duration.TotalSeconds:F1}s"),
                new Markup($"{agentResult.TokensUsed}"));
        }

        var panel = new Panel(table)
            .Header($"[bold]Team Complete ({result.Strategy})[/]")
            .BorderColor(result.Success ? Color.Green : Color.Red);

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine(
            $"[dim]Total: {result.TotalTokens} tokens, {result.Duration.TotalSeconds:F1}s[/]");
    }

    private static string GetStatusIcon(SubagentStatus status) => status switch
    {
        SubagentStatus.Pending => "⏸",
        SubagentStatus.Started => "🔀",
        SubagentStatus.Thinking => "💭",
        SubagentStatus.ExecutingTool => "⚙️",
        SubagentStatus.Completed => "✅",
        SubagentStatus.Failed => "❌",
        SubagentStatus.Cancelled => "🚫",
        _ => "❓",
    };

    private static string GetStatusColor(SubagentStatus status) => status switch
    {
        SubagentStatus.Completed => "green",
        SubagentStatus.Failed => "red",
        SubagentStatus.Cancelled => "yellow",
        SubagentStatus.Thinking => "blue",
        SubagentStatus.ExecutingTool => "cyan",
        _ => "dim",
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _agentStates.Clear();
    }
}
