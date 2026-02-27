using System.Text;

namespace JD.AI.Tui.Rendering;

/// <summary>
/// Classifies a segment of streaming output.
/// </summary>
public enum StreamSegmentKind
{
    /// <summary>Regular assistant response text.</summary>
    Content,

    /// <summary>Model thinking/reasoning text (rendered as dim gray).</summary>
    Thinking,

    /// <summary>Transition: the model entered a thinking block.</summary>
    EnterThinking,

    /// <summary>Transition: the model exited a thinking block.</summary>
    ExitThinking,
}

/// <summary>
/// A classified segment of streaming output.
/// </summary>
public readonly record struct StreamSegment(StreamSegmentKind Kind, string Text = "");

/// <summary>
/// Parses streaming LLM output to detect and separate thinking content
/// from response content. Handles &lt;think&gt;...&lt;/think&gt; tags that
/// may span chunk boundaries (used by Qwen, DeepSeek, and similar models).
/// </summary>
public sealed class StreamingContentParser
{
    private bool _isThinking;
    private readonly StringBuilder _tagBuffer = new();
    private readonly List<StreamSegment> _segments = new(8);

    private const string OpenTag = "<think>";
    private const string CloseTag = "</think>";

    /// <summary>Whether the parser is currently inside a thinking block.</summary>
    public bool IsThinking => _isThinking;

    /// <summary>
    /// Process a streaming chunk and return classified segments.
    /// The returned list is reused across calls — consume before the next call.
    /// </summary>
    public IReadOnlyList<StreamSegment> ProcessChunk(string chunk)
    {
        _segments.Clear();
        foreach (var c in chunk)
            ProcessChar(c);
        return _segments;
    }

    /// <summary>
    /// Flush any buffered content (e.g., an incomplete tag at end of stream).
    /// </summary>
    public IReadOnlyList<StreamSegment> Flush()
    {
        _segments.Clear();
        if (_tagBuffer.Length > 0)
        {
            EmitText(_tagBuffer.ToString());
            _tagBuffer.Clear();
        }
        return _segments;
    }

    /// <summary>Reset to initial state.</summary>
    public void Reset()
    {
        _isThinking = false;
        _tagBuffer.Clear();
        _segments.Clear();
    }

    private void ProcessChar(char c)
    {
        var targetTag = _isThinking ? CloseTag : OpenTag;

        // Start of a potential tag
        if (c == '<' && _tagBuffer.Length == 0)
        {
            _tagBuffer.Append(c);
            return;
        }

        // Accumulating a potential tag
        if (_tagBuffer.Length > 0)
        {
            _tagBuffer.Append(c);
            var buf = _tagBuffer.ToString();

            if (string.Equals(buf, targetTag, StringComparison.Ordinal))
            {
                // Complete tag match — toggle thinking state
                _tagBuffer.Clear();
                if (_isThinking)
                {
                    _isThinking = false;
                    _segments.Add(new StreamSegment(StreamSegmentKind.ExitThinking));
                }
                else
                {
                    _isThinking = true;
                    _segments.Add(new StreamSegment(StreamSegmentKind.EnterThinking));
                }
                return;
            }

            if (targetTag.StartsWith(buf, StringComparison.Ordinal))
            {
                return; // Still a potential match — keep buffering
            }

            // Not a match — flush accumulated chars as text
            var text = _tagBuffer.ToString();
            _tagBuffer.Clear();
            EmitText(text);
            return;
        }

        // Regular character
        EmitText(c.ToString());
    }

    private void EmitText(string text)
    {
        var kind = _isThinking
            ? StreamSegmentKind.Thinking
            : StreamSegmentKind.Content;

        // Merge with previous segment of the same kind to reduce churn
        if (_segments.Count > 0 && _segments[^1].Kind == kind)
        {
            var last = _segments[^1];
            _segments[^1] = last with { Text = last.Text + text };
        }
        else
        {
            _segments.Add(new StreamSegment(kind, text));
        }
    }
}
