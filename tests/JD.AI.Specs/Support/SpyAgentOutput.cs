using System.Text;
using JD.AI.Core.Agents;

namespace JD.AI.Specs.Support;

/// <summary>
/// Spy implementation of <see cref="IAgentOutput"/> that captures all output
/// for assertion in BDD scenarios.
/// </summary>
public sealed class SpyAgentOutput : IAgentOutput
{
    private readonly StringBuilder _streamingContent = new();
    private readonly StringBuilder _thinkingContent = new();
    private readonly List<string> _infoMessages = [];
    private readonly List<string> _warningMessages = [];
    private readonly List<string> _errorMessages = [];
    private readonly List<(string Name, string? Args, string Result)> _toolCalls = [];

    public string StreamingContent => _streamingContent.ToString();
    public string ThinkingContent => _thinkingContent.ToString();
    public IReadOnlyList<string> InfoMessages => _infoMessages;
    public IReadOnlyList<string> WarningMessages => _warningMessages;
    public IReadOnlyList<string> ErrorMessages => _errorMessages;
    public IReadOnlyList<(string Name, string? Args, string Result)> ToolCalls => _toolCalls;

    public bool ThinkingStarted { get; private set; }
    public bool ThinkingEnded { get; private set; }
    public bool StreamingStarted { get; private set; }
    public bool StreamingEnded { get; private set; }
    public bool TurnStarted { get; private set; }
    public bool TurnEnded { get; private set; }
    public TurnMetrics? LastTurnMetrics { get; private set; }

    public void RenderInfo(string message) => _infoMessages.Add(message);
    public void RenderWarning(string message) => _warningMessages.Add(message);
    public void RenderError(string message) => _errorMessages.Add(message);

    public void BeginThinking() => ThinkingStarted = true;
    public void WriteThinkingChunk(string text) => _thinkingContent.Append(text);
    public void EndThinking() => ThinkingEnded = true;

    public void BeginStreaming() => StreamingStarted = true;
    public void WriteStreamingChunk(string text) => _streamingContent.Append(text);
    public void EndStreaming() => StreamingEnded = true;

    public void BeginTurn() => TurnStarted = true;
    public void EndTurn(TurnMetrics metrics)
    {
        TurnEnded = true;
        LastTurnMetrics = metrics;
    }

    public void RenderToolCall(string toolName, string? args, string result)
        => _toolCalls.Add((toolName, args, result));

    public bool ConfirmToolCall(string toolName, string? args) => true;

    public void Reset()
    {
        _streamingContent.Clear();
        _thinkingContent.Clear();
        _infoMessages.Clear();
        _warningMessages.Clear();
        _errorMessages.Clear();
        _toolCalls.Clear();
        ThinkingStarted = false;
        ThinkingEnded = false;
        StreamingStarted = false;
        StreamingEnded = false;
        TurnStarted = false;
        TurnEnded = false;
        LastTurnMetrics = null;
    }
}
