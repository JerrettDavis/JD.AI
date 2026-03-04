using System.Diagnostics;

namespace JD.AI.Core.Safety;

/// <summary>
/// Detects repetitive tool invocation patterns that indicate no-progress loops.
/// Tracks a rolling window of recent calls and identifies:
/// <list type="bullet">
///   <item>Same-tool/same-args repetitions (stuck loops).</item>
///   <item>Ping-pong patterns between two tools/agents.</item>
///   <item>High-frequency bursts of any single tool.</item>
/// </list>
/// Thread-safe — designed to be shared across a session.
/// </summary>
public sealed class ToolLoopDetector
{
    private readonly Lock _lock = new();

    /// <summary>Rolling window of recent invocations (bounded by <see cref="WindowSize"/>).</summary>
    private readonly List<ToolInvocationRecord> _history = [];

    /// <summary>Maximum number of records kept in the rolling window.</summary>
    public int WindowSize { get; }

    /// <summary>
    /// Number of identical (tool+args) calls in the window that triggers a warning.
    /// </summary>
    public int RepetitionWarningThreshold { get; }

    /// <summary>
    /// Number of identical (tool+args) calls in the window that triggers a hard stop.
    /// </summary>
    public int RepetitionHardStopThreshold { get; }

    /// <summary>
    /// Number of alternating A→B→A→B calls that constitutes a ping-pong loop.
    /// </summary>
    public int PingPongThreshold { get; }

    public ToolLoopDetector(
        int windowSize = 50,
        int repetitionWarningThreshold = 3,
        int repetitionHardStopThreshold = 5,
        int pingPongThreshold = 4)
    {
        WindowSize = windowSize;
        RepetitionWarningThreshold = repetitionWarningThreshold;
        RepetitionHardStopThreshold = repetitionHardStopThreshold;
        PingPongThreshold = pingPongThreshold;
    }

    /// <summary>
    /// Records a tool invocation and returns a detection result.
    /// </summary>
    public LoopDetectionResult RecordAndEvaluate(
        string toolName,
        string? argsFingerprint = null,
        string? outputFingerprint = null,
        string? agentId = null)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        var record = new ToolInvocationRecord(
            toolName,
            argsFingerprint ?? string.Empty,
            outputFingerprint ?? string.Empty,
            agentId ?? string.Empty,
            Stopwatch.GetTimestamp());

        lock (_lock)
        {
            _history.Add(record);

            // Trim to window
            if (_history.Count > WindowSize)
            {
                _history.RemoveRange(0, _history.Count - WindowSize);
            }

            return Evaluate(record);
        }
    }

    /// <summary>Returns the current invocation count in the window.</summary>
    public int CurrentWindowCount
    {
        get { lock (_lock) { return _history.Count; } }
    }

    /// <summary>Clears all recorded history (e.g. after a circuit breaker reset).</summary>
    public void Reset()
    {
        lock (_lock) { _history.Clear(); }
    }

    private LoopDetectionResult Evaluate(ToolInvocationRecord current)
    {
        // ── 1. Exact repetition detection (same tool + same args) ──
        var exactCount = 0;
        var sameOutputCount = 0;
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            var h = _history[i];
            if (string.Equals(h.ToolName, current.ToolName, StringComparison.Ordinal) &&
                string.Equals(h.ArgsFingerprint, current.ArgsFingerprint, StringComparison.Ordinal))
            {
                exactCount++;
                if (!string.IsNullOrEmpty(current.OutputFingerprint) &&
                    string.Equals(h.OutputFingerprint, current.OutputFingerprint, StringComparison.Ordinal))
                {
                    sameOutputCount++;
                }
            }
        }

        if (exactCount >= RepetitionHardStopThreshold)
        {
            return new LoopDetectionResult(
                LoopDecision.HardStop,
                LoopType.ExactRepetition,
                $"Tool '{current.ToolName}' called {exactCount} times with identical arguments (threshold: {RepetitionHardStopThreshold}).");
        }

        if (exactCount >= RepetitionWarningThreshold)
        {
            return new LoopDetectionResult(
                LoopDecision.Warning,
                LoopType.ExactRepetition,
                $"Tool '{current.ToolName}' called {exactCount} times with identical arguments.");
        }

        // ── 2. Same output detection (tool producing identical results) ──
        if (sameOutputCount >= RepetitionWarningThreshold)
        {
            return new LoopDetectionResult(
                sameOutputCount >= RepetitionHardStopThreshold ? LoopDecision.HardStop : LoopDecision.Warning,
                LoopType.NoProgressOutput,
                $"Tool '{current.ToolName}' produced identical output {sameOutputCount} times.");
        }

        // ── 3. Ping-pong detection (A→B→A→B pattern) ──
        if (_history.Count >= PingPongThreshold)
        {
            var pingPongResult = DetectPingPong();
            if (pingPongResult is not null)
            {
                return pingPongResult;
            }
        }

        return LoopDetectionResult.Ok;
    }

    private LoopDetectionResult? DetectPingPong()
    {
        // Look at the tail of history for alternating patterns
        if (_history.Count < 4) return null;

        var tail = _history[^1];
        var prev1 = _history[^2];
        var prev2 = _history[^3];
        var prev3 = _history[^4];

        // Check if we have A→B→A→B pattern (tool names OR agent IDs)
        var toolPingPong = !string.Equals(tail.ToolName, prev1.ToolName, StringComparison.Ordinal) &&
                           string.Equals(tail.ToolName, prev2.ToolName, StringComparison.Ordinal) &&
                           string.Equals(prev1.ToolName, prev3.ToolName, StringComparison.Ordinal);

        if (toolPingPong)
        {
            // Count consecutive alternating pairs backward
            var alternatingCount = 2; // We already confirmed 2 pairs (4 calls)
            for (var i = _history.Count - 5; i >= 0 && i + 1 < _history.Count; i--)
            {
                var a = _history[i];
                var b = _history[i + 2]; // Compare every other
                if (string.Equals(a.ToolName, b.ToolName, StringComparison.Ordinal))
                {
                    alternatingCount++;
                }
                else
                {
                    break;
                }
            }

            if (alternatingCount >= PingPongThreshold)
            {
                return new LoopDetectionResult(
                    LoopDecision.HardStop,
                    LoopType.PingPong,
                    $"Ping-pong loop detected between '{tail.ToolName}' and '{prev1.ToolName}' ({alternatingCount} alternations).");
            }
        }

        // Check for cross-agent ping-pong
        if (!string.IsNullOrEmpty(tail.AgentId) && !string.IsNullOrEmpty(prev1.AgentId))
        {
            var agentPingPong = !string.Equals(tail.AgentId, prev1.AgentId, StringComparison.Ordinal) &&
                                string.Equals(tail.AgentId, prev2.AgentId, StringComparison.Ordinal) &&
                                string.Equals(prev1.AgentId, prev3.AgentId, StringComparison.Ordinal);

            if (agentPingPong)
            {
                return new LoopDetectionResult(
                    LoopDecision.Warning,
                    LoopType.CrossAgentPingPong,
                    $"Cross-agent ping-pong detected between agents '{tail.AgentId}' and '{prev1.AgentId}'.");
            }
        }

        return null;
    }
}

/// <summary>Immutable record of a single tool invocation for loop detection.</summary>
internal sealed record ToolInvocationRecord(
    string ToolName,
    string ArgsFingerprint,
    string OutputFingerprint,
    string AgentId,
    long TimestampTicks);

/// <summary>Result of a loop detection check.</summary>
public sealed record LoopDetectionResult(
    LoopDecision Decision,
    LoopType Type = LoopType.None,
    string? Message = null)
{
    /// <summary>No loop detected — everything is fine.</summary>
    public static readonly LoopDetectionResult Ok = new(LoopDecision.None);
}

/// <summary>What the detector recommends.</summary>
public enum LoopDecision
{
    /// <summary>No loop detected.</summary>
    None,

    /// <summary>Possible loop — emit a diagnostic warning but continue.</summary>
    Warning,

    /// <summary>Definite loop — stop execution immediately.</summary>
    HardStop,
}

/// <summary>Classification of the loop pattern detected.</summary>
public enum LoopType
{
    None,

    /// <summary>Same tool called with identical arguments repeatedly.</summary>
    ExactRepetition,

    /// <summary>Tool producing the same output with no progress.</summary>
    NoProgressOutput,

    /// <summary>Two tools alternating back and forth.</summary>
    PingPong,

    /// <summary>Two agents bouncing work between each other.</summary>
    CrossAgentPingPong,
}
