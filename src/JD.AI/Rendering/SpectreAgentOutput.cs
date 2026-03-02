using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>
/// Bridges <see cref="IAgentOutput"/> to the Spectre.Console-based
/// <see cref="ChatRenderer"/> so streaming output appears in the TUI.
/// </summary>
internal sealed class SpectreAgentOutput : IAgentOutput
{
    public void RenderInfo(string message) => ChatRenderer.RenderInfo(message);
    public void RenderWarning(string message) => ChatRenderer.RenderWarning(message);
    public void RenderError(string message) => ChatRenderer.RenderError(message);
    public void BeginThinking() => ChatRenderer.BeginThinking();
    public void WriteThinkingChunk(string text) => ChatRenderer.WriteThinkingChunk(text);
    public void EndThinking() => ChatRenderer.EndThinking();
    public void BeginStreaming() => ChatRenderer.BeginStreaming();
    public void WriteStreamingChunk(string text) => ChatRenderer.WriteStreamingChunk(text);
    public void EndStreaming() => ChatRenderer.EndStreaming();
}
