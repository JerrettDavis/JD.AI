namespace JD.AI.Core.Agents;

/// <summary>Metrics collected during a single agent turn.</summary>
public sealed record TurnMetrics(
    long ElapsedMs,
    int TokensOut,
    long BytesReceived,
    long? TimeToFirstTokenMs = null,
    string? ModelName = null);

/// <summary>
/// Abstraction for agent output rendering — allows Core agent logic to emit
/// UI messages without depending on Spectre.Console or any specific TUI framework.
/// </summary>
public interface IAgentOutput
{
    void RenderInfo(string message);
    void RenderWarning(string message);
    void RenderError(string message);
    void BeginThinking();
    void WriteThinkingChunk(string text);
    void EndThinking();
    void BeginStreaming();
    void WriteStreamingChunk(string text);
    void EndStreaming();

    /// <summary>Called when a turn starts (before first LLM call). Show a spinner.</summary>
    void BeginTurn() { }

    /// <summary>Called when a turn completes. Render elapsed time, tokens, data size.</summary>
    void EndTurn(TurnMetrics metrics) { }

    /// <summary>
    /// Render a tool invocation with optional arguments summary and result.
    /// Pauses any active spinner/progress indicator to avoid interleaved output.
    /// </summary>
    void RenderToolCall(string toolName, string? args, string result) { }

    /// <summary>
    /// Render a tool confirmation prompt. Returns true if the user approves.
    /// Pauses any active spinner/progress indicator to avoid interleaved output.
    /// </summary>
    bool ConfirmToolCall(string toolName, string? args) => true;
}

/// <summary>
/// Global accessor for the agent output renderer.
/// The TUI (or host) sets <see cref="Current"/> at startup;
/// Core code calls it without a direct Spectre.Console dependency.
/// </summary>
public static class AgentOutput
{
    /// <summary>
    /// The active output renderer. Defaults to <see cref="NullAgentOutput"/> (no-op).
    /// </summary>
    public static IAgentOutput Current { get; set; } = NullAgentOutput.Instance;
}

/// <summary>No-op implementation used when no TUI is wired up.</summary>
public sealed class NullAgentOutput : IAgentOutput
{
    public static readonly NullAgentOutput Instance = new();
    public void RenderInfo(string message) { }
    public void RenderWarning(string message) { }
    public void RenderError(string message) { }
    public void BeginThinking() { }
    public void WriteThinkingChunk(string text) { }
    public void EndThinking() { }
    public void BeginStreaming() { }
    public void WriteStreamingChunk(string text) { }
    public void EndStreaming() { }
    public void BeginTurn() { }
    public void EndTurn(TurnMetrics metrics) { }
}
