using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using System.Text;
using System.Text.RegularExpressions;

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
    private readonly StringBuilder _thinkingBuffer = new();
    private readonly List<string> _thinkingSteps = [];
    private string? _lastThinkingDetail;

    public SpectreAgentOutput(SpinnerStyle style = SpinnerStyle.Normal, string? modelName = null)
    {
        Style = style;
        ModelName = modelName;
    }

    /// <summary>The active spinner/progress style. Can be changed at runtime via /spinner.</summary>
    public SpinnerStyle Style { get; set; }

    /// <summary>Update the model name (e.g. after a /model switch).</summary>
    public string? ModelName { get; set; }

    public void RenderInfo(string message)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderInfo(message);
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
    }

    public void RenderWarning(string message)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderWarning(message);
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
    }

    public void RenderError(string message)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderError(message);
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
    }

    public void RenderToolCall(string toolName, string? args, string result)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderToolCall(toolName, args, result);
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
    }

    public bool ConfirmToolCall(string toolName, string? args)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderWarning($"Tool: {toolName}({args})");
        var confirmed = ChatRenderer.Confirm("Allow this tool to run?");
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
        return confirmed;
    }

    public bool ConfirmWorkflowPrompt(string triggeringTool)
    {
        PauseProgress();
        ChatRenderer.PauseStreaming();
        ChatRenderer.RenderInfo($"  \ud83d\udccb Tool '{triggeringTool}' requested \u2014 this looks like multi-step work.");
        var accepted = ChatRenderer.ConfirmWorkflow("Start a workflow?");
        ChatRenderer.ResumeStreaming();
        ResumeProgress();
        return accepted;
    }

    public void BeginTurn()
    {
        _thinkingBuffer.Clear();
        _thinkingSteps.Clear();
        _lastThinkingDetail = null;
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
        if (ChatRenderer.CurrentOutputStyle == OutputStyle.Json)
            ChatRenderer.BeginThinking();
    }

    public void WriteThinkingChunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (ChatRenderer.CurrentOutputStyle == OutputStyle.Json)
        {
            ChatRenderer.WriteThinkingChunk(text);
            return;
        }

        _thinkingBuffer.Append(text);
        UpdateThinkingSignals(text);
        _progress?.SetThinkingPreview(GetLiveThinkingPreview());
    }

    public void EndThinking()
    {
        if (ChatRenderer.CurrentOutputStyle == OutputStyle.Json)
            ChatRenderer.EndThinking();
    }

    public void BeginStreaming()
    {
        var summary = BuildThinkingSummary();
        StopProgress();

        if (!string.IsNullOrWhiteSpace(summary) &&
            Style is SpinnerStyle.Normal or SpinnerStyle.Rich or SpinnerStyle.Nerdy)
        {
            ChatRenderer.RenderInfo($"💭 Thought: {summary}");
        }

        ChatRenderer.BeginStreaming();
    }

    public void WriteStreamingChunk(string text) => ChatRenderer.WriteStreamingChunk(text);
    public void EndStreaming() => ChatRenderer.EndStreaming();

    public void Dispose() => StopProgress();

    /// <summary>Temporarily pause the spinner (clear the line) without disposing it.</summary>
    private void PauseProgress() => _progress?.Pause();

    /// <summary>Resume the spinner after a pause.</summary>
    private void ResumeProgress() => _progress?.Resume();

    private void StopProgress()
    {
        if (_progress is null) return;
        _progress.SetThinkingPreview(null);
        _progress.Dispose();
        _progress = null;
    }

    private void UpdateThinkingSignals(string chunk)
    {
        var normalized = chunk
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var compact = CollapseWhitespace(line);
            if (IsStepCandidate(compact))
            {
                var step = CompactStepText(compact);
                if (_thinkingSteps.Count == 0 ||
                    !string.Equals(_thinkingSteps[^1], step, StringComparison.OrdinalIgnoreCase))
                {
                    _thinkingSteps.Add(step);
                }

                _lastThinkingDetail = step;
            }
            else
            {
                _lastThinkingDetail = compact;
            }
        }
    }

    private string? GetLiveThinkingPreview()
    {
        if (Style == SpinnerStyle.Minimal || Style == SpinnerStyle.None)
            return null;

        if (Style == SpinnerStyle.Normal)
            return _lastThinkingDetail;

        if (_thinkingSteps.Count == 0)
            return _lastThinkingDetail;

        var step = _thinkingSteps[^1];
        if (string.IsNullOrWhiteSpace(_lastThinkingDetail) ||
            string.Equals(step, _lastThinkingDetail, StringComparison.OrdinalIgnoreCase))
        {
            return $"▶ {step}";
        }

        return $"▶ {step} | {_lastThinkingDetail}";
    }

    private string? BuildThinkingSummary()
    {
        if (_thinkingSteps.Count > 0)
        {
            var joined = string.Join(" -> ", _thinkingSteps.Take(4));
            return TrimForSummary(joined, 140);
        }

        if (!string.IsNullOrWhiteSpace(_lastThinkingDetail))
            return TrimForSummary(_lastThinkingDetail, 140);

        if (_thinkingBuffer.Length == 0)
            return null;

        return TrimForSummary(CollapseWhitespace(_thinkingBuffer.ToString()), 140);
    }

    private static bool IsStepCandidate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (Regex.IsMatch(text, @"^\d+\.\s+", RegexOptions.CultureInvariant))
            return true;

        var lower = text.ToLowerInvariant();
        return lower.StartsWith("step ", StringComparison.Ordinal) ||
               lower.StartsWith("next,", StringComparison.Ordinal) ||
               lower.StartsWith("next ", StringComparison.Ordinal) ||
               lower.StartsWith("now ", StringComparison.Ordinal) ||
               lower.StartsWith("let's ", StringComparison.Ordinal) ||
               lower.StartsWith("lets ", StringComparison.Ordinal) ||
               lower.StartsWith("finally", StringComparison.Ordinal);
    }

    private static string CompactStepText(string text)
    {
        var compact = CollapseWhitespace(text);
        return TrimForSummary(compact, 80);
    }

    private static string CollapseWhitespace(string text) =>
        Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string TrimForSummary(string text, int maxChars)
    {
        if (text.Length <= maxChars)
            return text;

        return string.Concat(text.AsSpan(0, maxChars - 3), "...");
    }
}
