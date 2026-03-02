using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>
/// Bridges <see cref="IAgentOutput"/> to the Spectre.Console-based
/// <see cref="ChatRenderer"/> so streaming output appears in the TUI.
/// Manages a spinner during the thinking phase and renders metrics on completion.
/// </summary>
internal sealed class SpectreAgentOutput : IAgentOutput, IDisposable
{
    private TurnSpinner? _spinner;

    public void RenderInfo(string message) => ChatRenderer.RenderInfo(message);
    public void RenderWarning(string message) => ChatRenderer.RenderWarning(message);
    public void RenderError(string message) => ChatRenderer.RenderError(message);

    public void BeginTurn()
    {
        _spinner = new TurnSpinner();
    }

    public void EndTurn(TurnMetrics metrics)
    {
        StopSpinner();
        ChatRenderer.RenderTurnMetrics(metrics.ElapsedMs, metrics.TokensOut, metrics.BytesReceived);
    }

    public void BeginThinking()
    {
        StopSpinner();
        ChatRenderer.BeginThinking();
    }

    public void WriteThinkingChunk(string text) => ChatRenderer.WriteThinkingChunk(text);
    public void EndThinking() => ChatRenderer.EndThinking();

    public void BeginStreaming()
    {
        StopSpinner();
        ChatRenderer.BeginStreaming();
    }

    public void WriteStreamingChunk(string text) => ChatRenderer.WriteStreamingChunk(text);
    public void EndStreaming() => ChatRenderer.EndStreaming();

    public void Dispose() => StopSpinner();

    private void StopSpinner()
    {
        if (_spinner is null) return;
        _spinner.Dispose();
        _spinner = null;
    }
}
