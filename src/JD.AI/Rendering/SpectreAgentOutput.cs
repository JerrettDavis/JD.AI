using JD.AI.Core.Agents;
using JD.AI.Core.Config;

namespace JD.AI.Rendering;

/// <summary>
/// Bridges <see cref="IAgentOutput"/> to the Spectre.Console-based
/// <see cref="ChatRenderer"/> so streaming output appears in the TUI.
/// Manages a styled progress indicator during the thinking phase
/// and renders metrics on completion.
/// </summary>
internal sealed class SpectreAgentOutput : IAgentOutput, IDisposable
{
    private TurnProgress? _progress;

    public SpectreAgentOutput(SpinnerStyle style = SpinnerStyle.Normal, string? modelName = null)
    {
        Style = style;
        ModelName = modelName;
    }

    /// <summary>The active spinner/progress style. Can be changed at runtime via /spinner.</summary>
    public SpinnerStyle Style { get; set; }

    /// <summary>Update the model name (e.g. after a /model switch).</summary>
    public string? ModelName { get; set; }

    public void RenderInfo(string message) => ChatRenderer.RenderInfo(message);
    public void RenderWarning(string message) => ChatRenderer.RenderWarning(message);
    public void RenderError(string message) => ChatRenderer.RenderError(message);

    public void BeginTurn()
    {
        _progress = new TurnProgress(Style, ModelName);
    }

    public void EndTurn(TurnMetrics metrics)
    {
        var ttft = _progress?.TimeToFirstTokenMs;
        StopProgress();
        ChatRenderer.RenderTurnMetrics(
            metrics.ElapsedMs, metrics.TokensOut, metrics.BytesReceived,
            Style, ttft, metrics.ModelName ?? ModelName);
    }

    public void BeginThinking()
    {
        StopProgress();
        ChatRenderer.BeginThinking();
    }

    public void WriteThinkingChunk(string text) => ChatRenderer.WriteThinkingChunk(text);
    public void EndThinking() => ChatRenderer.EndThinking();

    public void BeginStreaming()
    {
        StopProgress();
        ChatRenderer.BeginStreaming();
    }

    public void WriteStreamingChunk(string text) => ChatRenderer.WriteStreamingChunk(text);
    public void EndStreaming() => ChatRenderer.EndStreaming();

    public void Dispose() => StopProgress();

    private void StopProgress()
    {
        if (_progress is null) return;
        _progress.Dispose();
        _progress = null;
    }
}
