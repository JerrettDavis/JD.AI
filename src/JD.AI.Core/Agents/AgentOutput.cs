namespace JD.AI.Core.Agents;

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
}
