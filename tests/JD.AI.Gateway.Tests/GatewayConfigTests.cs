using FluentAssertions;
using JD.AI.Gateway.Config;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayConfigTests
{
    [Fact]
    public void DefaultConfig_HasReasonableDefaults()
    {
        var config = new GatewayConfig();

        config.Server.Port.Should().Be(18789);
        config.Server.Host.Should().Be("localhost");
        config.Auth.Enabled.Should().BeFalse();
        config.RateLimit.Enabled.Should().BeTrue();
        config.RateLimit.MaxRequestsPerMinute.Should().Be(60);
        config.Channels.Should().BeEmpty();
        config.Agents.Should().BeEmpty();
        config.Routing.Rules.Should().BeEmpty();
        config.OpenClaw.Enabled.Should().BeFalse();
    }

    [Fact]
    public void AgentDefinition_Defaults()
    {
        var def = new AgentDefinition();

        def.AutoSpawn.Should().BeTrue();
        def.MaxTurns.Should().Be(0);
        def.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ChannelConfig_Defaults()
    {
        var cfg = new ChannelConfig();

        cfg.Enabled.Should().BeTrue();
        cfg.AutoConnect.Should().BeFalse();
        cfg.Settings.Should().BeEmpty();
    }

    [Fact]
    public void RoutingConfig_Defaults()
    {
        var cfg = new RoutingConfig();

        cfg.DefaultAgentId.Should().BeEmpty();
        cfg.Rules.Should().BeEmpty();
    }

    [Fact]
    public void OpenClawGatewayConfig_Defaults()
    {
        var cfg = new OpenClawGatewayConfig();

        cfg.Enabled.Should().BeFalse();
        cfg.AutoConnect.Should().BeTrue();
        cfg.DefaultMode.Should().Be("Passthrough");
        cfg.WebSocketUrl.Should().Contain("18789");
        cfg.Channels.Should().BeEmpty();
        cfg.RegisterAgents.Should().BeEmpty();
    }

    [Fact]
    public void OpenClawAgentRegistration_Defaults()
    {
        var reg = new OpenClawAgentRegistration();

        reg.Id.Should().BeEmpty();
        reg.Name.Should().BeEmpty();
        reg.Emoji.Should().Be("🤖");
        reg.Theme.Should().Be("JD.AI agent");
        reg.Model.Should().BeNull();
        reg.GatewayAgentId.Should().BeNull();
        reg.Bindings.Should().BeEmpty();
    }

    [Fact]
    public void OpenClawAgentRegistration_FullyConfigured()
    {
        var reg = new OpenClawAgentRegistration
        {
            Id = "jdai-coder",
            Name = "JD.AI Coder",
            Emoji = "💻",
            Theme = "Expert coder",
            Model = "ollama/llama3.2",
            GatewayAgentId = "default",
            Bindings =
            [
                new OpenClawBindingConfig
                {
                    Channel = "discord",
                    AccountId = "default",
                    GuildId = "123456",
                    PeerKind = "direct",
                    PeerId = "user1",
                },
            ],
        };

        reg.Id.Should().Be("jdai-coder");
        reg.Name.Should().Be("JD.AI Coder");
        reg.Bindings.Should().HaveCount(1);
        reg.Bindings[0].Channel.Should().Be("discord");
        reg.Bindings[0].GuildId.Should().Be("123456");
        reg.Bindings[0].PeerKind.Should().Be("direct");
        reg.Bindings[0].PeerId.Should().Be("user1");
    }

    [Fact]
    public void OpenClawBindingConfig_Defaults()
    {
        var binding = new OpenClawBindingConfig();

        binding.Channel.Should().BeEmpty();
        binding.AccountId.Should().BeNull();
        binding.PeerKind.Should().BeNull();
        binding.PeerId.Should().BeNull();
        binding.GuildId.Should().BeNull();
    }

    [Fact]
    public void RoutingRule_CanConfigureChannelToAgent()
    {
        var rule = new RoutingRule
        {
            ChannelType = "discord",
            AgentId = "my-agent",
            ConversationPattern = "general-*"
        };

        rule.ChannelType.Should().Be("discord");
        rule.AgentId.Should().Be("my-agent");
        rule.ConversationPattern.Should().Be("general-*");
    }

    [Fact]
    public void OpenClawChannelConfig_CanConfigureSidecar()
    {
        var cfg = new OpenClawChannelConfig
        {
            Mode = "Sidecar",
            CommandPrefix = "/jdai",
            TriggerPattern = @"^(hey jd|@jdai)",
            SystemPrompt = "You are a helpful assistant."
        };

        cfg.Mode.Should().Be("Sidecar");
        cfg.CommandPrefix.Should().Be("/jdai");
        cfg.TriggerPattern.Should().Contain("jdai");
    }

    [Fact]
    public void FullConfig_CanBeConstructedProgrammatically()
    {
        var config = new GatewayConfig
        {
            Server = new ServerConfig { Port = 8080, Verbose = true },
            Auth = new AuthConfig
            {
                Enabled = true,
                ApiKeys = [new ApiKeyEntry { Key = "test-key", Name = "Test", Role = "Admin" }]
            },
            Agents =
            [
                new AgentDefinition
                {
                    Id = "assistant",
                    Provider = "ollama",
                    Model = "llama3.2",
                    SystemPrompt = "You are a helpful assistant.",
                    AutoSpawn = true
                }
            ],
            Channels =
            [
                new ChannelConfig
                {
                    Type = "discord",
                    Name = "My Discord",
                    Enabled = true,
                    AutoConnect = true,
                    Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["BotToken"] = "env:DISCORD_TOKEN" }
                }
            ],
            Routing = new RoutingConfig
            {
                DefaultAgentId = "assistant",
                Rules = [new RoutingRule { ChannelType = "discord", AgentId = "assistant" }]
            },
            OpenClaw = new OpenClawGatewayConfig
            {
                Enabled = true,
                Channels = new Dictionary<string, OpenClawChannelConfig>(StringComparer.Ordinal)
                {
                    ["discord"] = new OpenClawChannelConfig { Mode = "Intercept" }
                }
            }
        };

        config.Agents.Should().HaveCount(1);
        config.Channels.Should().HaveCount(1);
        config.Routing.Rules.Should().HaveCount(1);
        config.OpenClaw.Enabled.Should().BeTrue();
        config.OpenClaw.Channels.Should().ContainKey("discord");
    }
}
