using System.Net.Http;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Verifies that the streaming-to-non-streaming fallback detection works
/// for Foundry Local and similar providers that silently drop SSE connections.
/// </summary>
public sealed class StreamingFallbackTests
{
    [Fact]
    public void IsStreamingPrematureEnd_DetectsResponseEndedMessage()
    {
        var ex = new HttpRequestException(
            "The response ended prematurely. (ResponseEnded)");
        Assert.True(AgentLoop.IsStreamingPrematureEnd(ex));
    }

    [Fact]
    public void IsStreamingPrematureEnd_DetectsNestedResponseEnded()
    {
        var inner = new InvalidOperationException("The response ended prematurely. (ResponseEnded)");
        var outer = new HttpRequestException("An error occurred", inner);
        Assert.True(AgentLoop.IsStreamingPrematureEnd(outer));
    }

    [Fact]
    public void IsStreamingPrematureEnd_ReturnsFalseForUnrelatedErrors()
    {
        var ex = new HttpRequestException("Connection refused");
        Assert.False(AgentLoop.IsStreamingPrematureEnd(ex));
    }

    [Fact]
    public void IsStreamingPrematureEnd_ReturnsFalseForNull()
    {
        var ex = new InvalidOperationException("Something went wrong");
        Assert.False(AgentLoop.IsStreamingPrematureEnd(ex));
    }

    [Fact]
    public void IsStreamingPrematureEnd_Detects500WithResponseEnded()
    {
        var inner = new InvalidOperationException("ResponseEnded");
        var outer = new HttpRequestException(
            "Response status code does not indicate success: 500",
            inner);
        Assert.True(AgentLoop.IsStreamingPrematureEnd(outer));
    }
}
