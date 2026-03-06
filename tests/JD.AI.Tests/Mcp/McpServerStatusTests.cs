using FluentAssertions;
using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class McpServerStatusTests
{
    [Fact]
    public void Default_HasUnknownState()
    {
        var status = McpServerStatus.Default;
        status.State.Should().Be(McpConnectionState.Unknown);
        status.LastErrorSummary.Should().BeNull();
        status.LastErrorDetails.Should().BeNull();
    }

    [Theory]
    [InlineData(McpConnectionState.Connected, "\u2714")]
    [InlineData(McpConnectionState.Failed, "\u2718")]
    [InlineData(McpConnectionState.Connecting, "\u2026")]
    [InlineData(McpConnectionState.Disabled, "\u25CB")]
    [InlineData(McpConnectionState.Unknown, "?")]
    public void Icon_MatchesState(McpConnectionState state, string expectedIcon)
    {
        var status = new McpServerStatus { State = state };
        status.Icon.Should().Be(expectedIcon);
    }

    [Fact]
    public void ErrorProperties_Roundtrip()
    {
        var status = new McpServerStatus
        {
            State = McpConnectionState.Failed,
            LastErrorSummary = "Connection refused",
            LastErrorDetails = "ECONNREFUSED 127.0.0.1:8080",
        };
        status.State.Should().Be(McpConnectionState.Failed);
        status.LastErrorSummary.Should().Be("Connection refused");
        status.LastErrorDetails.Should().Contain("ECONNREFUSED");
    }

    [Fact]
    public void LastCheckedUtc_DefaultsToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var status = new McpServerStatus();
        var after = DateTimeOffset.UtcNow;
        status.LastCheckedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
