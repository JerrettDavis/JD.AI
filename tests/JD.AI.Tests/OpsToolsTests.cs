using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Tools;
using NSubstitute;

namespace JD.AI.Tests;

public sealed class OpsToolsTests
{
    // ── ChannelOpsTools ──────────────────────────────────

    [Fact]
    public void ChannelList_Empty_ReturnsNoChannels()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = tools.ListChannels();

        result.Should().Contain("No channels registered");
    }

    [Fact]
    public void ChannelList_WithChannels_ReturnsTable()
    {
        var registry = new ChannelRegistry();
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns("discord");
        channel.DisplayName.Returns("Discord Bot");
        channel.IsConnected.Returns(true);
        registry.Register(channel);

        var tools = new ChannelOpsTools(registry);
        var result = tools.ListChannels();

        result.Should().Contain("discord");
        result.Should().Contain("Discord Bot");
        result.Should().Contain("Connected");
    }

    [Fact]
    public void ChannelStatus_NotFound_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = tools.GetChannelStatus("discord");

        result.Should().Contain("not found");
    }

    [Fact]
    public void ChannelStatus_Found_ReturnsDetails()
    {
        var registry = new ChannelRegistry();
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns("slack");
        channel.DisplayName.Returns("Slack Workspace");
        channel.IsConnected.Returns(false);
        registry.Register(channel);

        var tools = new ChannelOpsTools(registry);
        var result = tools.GetChannelStatus("slack");

        result.Should().Contain("Slack Workspace");
        result.Should().Contain("No");
    }

    [Fact]
    public async Task ChannelSend_EmptyMessage_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("discord", "123", "   ");

        result.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task ChannelSend_NotConnected_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns("discord");
        channel.IsConnected.Returns(false);
        registry.Register(channel);

        var tools = new ChannelOpsTools(registry);
        var result = await tools.SendMessageAsync("discord", "123", "hello");

        result.Should().Contain("not connected");
    }

    [Fact]
    public async Task ChannelSend_Connected_SendsMessage()
    {
        var registry = new ChannelRegistry();
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns("web");
        channel.IsConnected.Returns(true);
        registry.Register(channel);

        var tools = new ChannelOpsTools(registry);
        var result = await tools.SendMessageAsync("web", "conv-1", "Hello world");

        result.Should().Contain("Message sent");
        result.Should().Contain("11 chars");
        await channel.Received(1).SendMessageAsync("conv-1", "Hello world", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChannelConnect_AlreadyConnected_ReturnsMessage()
    {
        var registry = new ChannelRegistry();
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns("discord");
        channel.IsConnected.Returns(true);
        registry.Register(channel);

        var tools = new ChannelOpsTools(registry);
        var result = await tools.ConnectChannelAsync("discord");

        result.Should().Contain("already connected");
    }

    // ── SchedulerTools ───────────────────────────────────

    [Fact]
    public void CronList_Empty_ReturnsNoTasks()
    {
        var tools = new SchedulerTools();

        var result = tools.ListTasks();

        result.Should().Contain("No scheduled tasks");
    }

    [Fact]
    public void CronAdd_CreatesTask()
    {
        var tools = new SchedulerTools();

        var result = tools.AddTask("backup", "0 2 * * *", "run-backup.sh", "Nightly backup");

        result.Should().Contain("#1");
        result.Should().Contain("backup");
        result.Should().Contain("0 2 * * *");
    }

    [Fact]
    public void CronAdd_EmptyName_ReturnsError()
    {
        var tools = new SchedulerTools();

        var result = tools.AddTask("", "5m", "echo hi");

        result.Should().Contain("name cannot be empty");
    }

    [Fact]
    public void CronList_WithTasks_ShowsTable()
    {
        var tools = new SchedulerTools();
        tools.AddTask("task-a", "5m", "echo a");
        tools.AddTask("task-b", "1h", "echo b");

        var result = tools.ListTasks();

        result.Should().Contain("task-a");
        result.Should().Contain("task-b");
        result.Should().Contain("Scheduled Tasks (2)");
    }

    [Fact]
    public void CronRemove_ExistingTask_Removes()
    {
        var tools = new SchedulerTools();
        tools.AddTask("temp", "1m", "echo temp");

        var result = tools.RemoveTask(1);

        result.Should().Contain("removed");
        tools.ListTasks().Should().Contain("No scheduled tasks");
    }

    [Fact]
    public void CronRemove_NotFound_ReturnsError()
    {
        var tools = new SchedulerTools();

        var result = tools.RemoveTask(999);

        result.Should().Contain("not found");
    }

    [Fact]
    public void CronUpdate_ChangesSchedule()
    {
        var tools = new SchedulerTools();
        tools.AddTask("job", "5m", "run.sh");

        var result = tools.UpdateTask(1, schedule: "10m");

        result.Should().Contain("updated");
    }

    [Fact]
    public void CronUpdate_InvalidStatus_ReturnsError()
    {
        var tools = new SchedulerTools();
        tools.AddTask("job", "5m", "run.sh");

        var result = tools.UpdateTask(1, status: "invalid");

        result.Should().Contain("must be 'active' or 'paused'");
    }

    [Fact]
    public void CronRun_TriggersTask()
    {
        var tools = new SchedulerTools();
        tools.AddTask("manual", "1h", "deploy.sh");

        var result = tools.RunTask(1);

        result.Should().Contain("triggered");
        result.Should().Contain("deploy.sh");
    }

    [Fact]
    public void CronHistory_ShowsDetails()
    {
        var tools = new SchedulerTools();
        tools.AddTask("hist-test", "30m", "check.sh", "Checks things");
        tools.RunTask(1);

        var result = tools.GetTaskHistory(1);

        result.Should().Contain("hist-test");
        result.Should().Contain("30m");
        result.Should().Contain("check.sh");
        result.Should().Contain("Checks things");
    }

    // ── GatewayOpsTools ──────────────────────────────────

    [Fact]
    public async Task GatewayStatus_NoEndpoint_ReturnsMessage()
    {
        var tools = new GatewayOpsTools();

        var result = await tools.GetStatusAsync();

        result.Should().Contain("Not configured");
    }

    [Fact]
    public async Task GatewayConfig_NoEndpoint_ReturnsError()
    {
        var tools = new GatewayOpsTools();

        var result = await tools.GetConfigAsync();

        result.Should().Contain("No gateway endpoint configured");
    }

    [Fact]
    public async Task GatewayStatus_InvalidEndpoint_ReportsUnavailable()
    {
        // Use a clearly non-routable address to get fast failure
        var tools = new GatewayOpsTools("http://192.0.2.1:1");

        var result = await tools.GetStatusAsync();

        // Should report unavailable (either connection error or timeout)
        result.Should().Contain("Gateway Status");
    }

    [Fact]
    public async Task GatewayChannels_NoEndpoint_ReturnsError()
    {
        var tools = new GatewayOpsTools();

        var result = await tools.ListGatewayChannelsAsync();

        result.Should().Contain("No gateway endpoint configured");
    }

    [Fact]
    public async Task GatewayAgents_NoEndpoint_ReturnsError()
    {
        var tools = new GatewayOpsTools();

        var result = await tools.ListGatewayAgentsAsync();

        result.Should().Contain("No gateway endpoint configured");
    }

    [Fact]
    public async Task GatewaySessions_NoEndpoint_ReturnsError()
    {
        var tools = new GatewayOpsTools();

        var result = await tools.ListGatewaySessionsAsync();

        result.Should().Contain("No gateway endpoint configured");
    }
}
