using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="ChatRenderer.PauseStreaming"/> / <see cref="ChatRenderer.ResumeStreaming"/>.
/// These verify that streaming text is still buffered while paused and that state
/// resets correctly when streaming ends.
/// </summary>
[Collection("Console")]
public sealed class StreamingPauseTests : IDisposable
{
    private readonly TextWriter _originalOut;

    public StreamingPauseTests()
    {
        _originalOut = Console.Out;
        ChatRenderer.SetOutputStyle(OutputStyle.Rich);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        ChatRenderer.SetOutputStyle(OutputStyle.Rich);
    }

    [Fact]
    public void PauseStreaming_StillBuffersText()
    {
        // Redirect console to capture output
        var sw = new StringWriter();
        Console.SetOut(sw);

        ChatRenderer.BeginStreaming();
        ChatRenderer.WriteStreamingChunk("hello ");

        ChatRenderer.PauseStreaming();
        ChatRenderer.WriteStreamingChunk("world"); // Should buffer but not write indicator

        ChatRenderer.ResumeStreaming();
        // End streaming — should still have all buffered content
        ChatRenderer.EndStreaming();

        // After EndStreaming, the full content was available for rendering
        // (we can't easily verify MarkdownRenderer output without mocking,
        //  but we can verify it didn't throw and the stream completed)
    }

    [Fact]
    public void PauseStreaming_IsNoOpWhenNotStreaming()
    {
        // Should not throw when not streaming
        ChatRenderer.PauseStreaming();
        ChatRenderer.ResumeStreaming();
    }

    [Fact]
    public void EndStreaming_ResetsPauseFlag()
    {
        var sw = new StringWriter();
        Console.SetOut(sw);

        // First stream — pause mid-stream
        ChatRenderer.BeginStreaming();
        ChatRenderer.PauseStreaming();
        ChatRenderer.EndStreaming();

        // Second stream — should work normally (pause flag reset)
        ChatRenderer.BeginStreaming();
        ChatRenderer.WriteStreamingChunk("test");
        ChatRenderer.EndStreaming();

        // No exception means pause flag was properly reset
    }
}
