using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Tests.Dashboard;

public sealed class DashboardModelsTests
{
    [Fact]
    public void ChannelInfo_AliasesReflectUnderlyingFields()
    {
        var info = new ChannelInfo
        {
            ChannelType = "discord",
            DisplayName = "Discord",
            IsConnected = true,
        };

        Assert.Equal("discord", info.Type);
        Assert.Equal("Discord", info.Name);
        Assert.True(info.Connected);
    }

    [Fact]
    public void GatewayStatus_ComputedPropertiesReflectState()
    {
        var status = new GatewayStatus
        {
            Status = "running",
            Channels =
            [
                new GatewayChannelStatus { ChannelType = "discord", IsConnected = true },
                new GatewayChannelStatus { ChannelType = "signal", IsConnected = false },
            ],
            Agents =
            [
                new GatewayAgentStatus { Id = "a1", Provider = "openai", Model = "gpt-5.3-codex" },
                new GatewayAgentStatus { Id = "a2", Provider = "ollama", Model = "qwen3.5:27b" },
            ],
        };

        Assert.True(status.IsRunning);
        Assert.Equal(2, status.ActiveAgents);
        Assert.Equal(1, status.ActiveChannels);
        Assert.Equal(0, status.ActiveSessions);
    }

    [Fact]
    public void OpenClawStatus_ComputedPropertiesPreferEnabledAndRegisteredCount()
    {
        var status = new OpenClawStatus
        {
            Enabled = true,
            ConnectedRaw = false,
            RegisteredAgents = ["jdai-default", "jdai-research"],
        };

        Assert.True(status.Connected);
        Assert.Equal(2, status.RegisteredAgentCount);
    }
}
