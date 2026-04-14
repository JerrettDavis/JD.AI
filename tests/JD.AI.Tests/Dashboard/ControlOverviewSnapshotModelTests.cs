using FluentAssertions;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Tests.Dashboard;

public sealed class ControlOverviewSnapshotModelTests
{
    [Fact]
    public void FromGatewayStatus_WhenRunning_ReturnsOkStatusAndFormattedUptime()
    {
        var status = new GatewayStatus
        {
            Status = "running",
            Uptime = DateTimeOffset.UtcNow.AddHours(-19),
            Channels = [],
            Agents = [],
        };

        var snapshot = ControlOverviewSnapshotModel.From(status, sessions: []);

        snapshot.StatusText.Should().Be("OK");
        snapshot.StatusColor.Should().Be("success");
        snapshot.UptimeDisplay.Should().MatchRegex(@"^\d+h$|^\d+d \d+h$");
    }

    [Fact]
    public void FromGatewayStatus_WhenNull_ReturnsDisconnectedDefaults()
    {
        var snapshot = ControlOverviewSnapshotModel.From(null, sessions: []);

        snapshot.StatusText.Should().Be("Disconnected");
        snapshot.StatusColor.Should().Be("error");
        snapshot.UptimeDisplay.Should().Be("—");
    }

    [Fact]
    public void FromGatewayStatus_SessionCount_MatchesActiveSessionsInList()
    {
        var sessions = new[]
        {
            new SessionInfo { Id = "heartbeat", ModelId = "qwen", UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-58) },
            new SessionInfo { Id = "discord:xyz", ModelId = "gpt-5", UpdatedAt = DateTimeOffset.UtcNow.AddHours(-17) },
        };

        var snapshot = ControlOverviewSnapshotModel.From(new GatewayStatus { Status = "running" }, sessions);

        snapshot.SessionCount.Should().Be(2);
    }
}
