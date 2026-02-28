using System.Collections.Concurrent;

namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Shared context for a team of agents — provides a thread-safe scratchpad,
/// chronological event stream, and per-agent results collection.
/// </summary>
public sealed class TeamContext
{
    private readonly ConcurrentDictionary<string, string> _scratchpad = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<AgentEvent> _events = new();
    private readonly ConcurrentDictionary<string, AgentResult> _results = new(StringComparer.Ordinal);

    /// <summary>The high-level goal this team is working toward.</summary>
    public string Goal { get; }

    /// <summary>Maximum subagent nesting depth (default 2).</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>Current nesting depth (0 = top-level team).</summary>
    public int CurrentDepth { get; init; }

    public TeamContext(string goal)
    {
        Goal = goal;
    }

    // ── Scratchpad ────────────────────────────────────────

    /// <summary>Read a value from the shared scratchpad.</summary>
    public string? ReadScratchpad(string key) =>
        _scratchpad.TryGetValue(key, out var value) ? value : null;

    /// <summary>Write a value to the shared scratchpad.</summary>
    public void WriteScratchpad(string key, string value) =>
        _scratchpad[key] = value;

    /// <summary>Get all scratchpad entries as a read-only snapshot.</summary>
    public IReadOnlyDictionary<string, string> GetScratchpadSnapshot() =>
        new Dictionary<string, string>(_scratchpad, StringComparer.Ordinal);

    /// <summary>Remove a scratchpad entry.</summary>
    public bool RemoveScratchpad(string key) =>
        _scratchpad.TryRemove(key, out _);

    // ── Event Stream ──────────────────────────────────────

    /// <summary>Record an event in the chronological event stream.</summary>
    public void RecordEvent(AgentEvent agentEvent) =>
        _events.Enqueue(agentEvent);

    /// <summary>Record an event with auto-timestamp.</summary>
    public void RecordEvent(string agentName, AgentEventType type, string content) =>
        _events.Enqueue(new AgentEvent(agentName, type, content));

    /// <summary>Get all events as a chronological snapshot.</summary>
    public IReadOnlyList<AgentEvent> GetEventsSnapshot() =>
        [.. _events];

    /// <summary>Get events filtered by agent name.</summary>
    public IReadOnlyList<AgentEvent> GetEventsFor(string agentName) =>
        [.. _events.Where(e => string.Equals(e.AgentName, agentName, StringComparison.Ordinal))];

    /// <summary>Get the count of events recorded.</summary>
    public int EventCount => _events.Count;

    // ── Results ───────────────────────────────────────────

    /// <summary>Store a completed agent's result.</summary>
    public void SetResult(AgentResult result) =>
        _results[result.AgentName] = result;

    /// <summary>Get a specific agent's result.</summary>
    public AgentResult? GetResult(string agentName) =>
        _results.TryGetValue(agentName, out var result) ? result : null;

    /// <summary>Get all results as a read-only snapshot.</summary>
    public IReadOnlyDictionary<string, AgentResult> GetResultsSnapshot() =>
        new Dictionary<string, AgentResult>(_results, StringComparer.Ordinal);

    /// <summary>Check if all expected agents have reported results.</summary>
    public bool AllCompleted(IEnumerable<string> expectedAgents) =>
        expectedAgents.All(name => _results.ContainsKey(name));

    // ── Nesting ───────────────────────────────────────────

    /// <summary>Check if we can spawn a deeper subagent.</summary>
    public bool CanNest => CurrentDepth < MaxDepth;

    /// <summary>Create a child context for a nested team with incremented depth.</summary>
    public TeamContext CreateChildContext(string childGoal) => new(childGoal)
    {
        MaxDepth = MaxDepth,
        CurrentDepth = CurrentDepth + 1,
    };

    // ── Summary ───────────────────────────────────────────

    /// <summary>Produce a text summary of the team context for injection into prompts.</summary>
    public string ToPromptSummary()
    {
        var parts = new List<string> { $"Team Goal: {Goal}" };

        var scratchpad = GetScratchpadSnapshot();
        if (scratchpad.Count > 0)
        {
            parts.Add("Shared scratchpad:");
            foreach (var (key, value) in scratchpad)
            {
                var preview = value.Length > 200 ? string.Concat(value.AsSpan(0, 200), "...") : value;
                parts.Add($"  {key}: {preview}");
            }
        }

        var events = GetEventsSnapshot();
        if (events.Count > 0)
        {
            var recent = events.TakeLast(10).ToList();
            parts.Add($"Recent events ({events.Count} total, showing last {recent.Count}):");
            foreach (var evt in recent)
            {
                parts.Add($"  [{evt.AgentName}] {evt.EventType}: {evt.Content}");
            }
        }

        return string.Join('\n', parts);
    }
}
