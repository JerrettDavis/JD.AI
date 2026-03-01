using JD.AI.Channels.OpenClaw;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Channels;

public sealed class OpenClawAgentRegistrarTests
{
    private readonly OpenClawRpcClient _rpc;
    private readonly OpenClawAgentRegistrar _registrar;

    public OpenClawAgentRegistrarTests()
    {
        // Real RPC client, unconnected — IsConnected = false
        _rpc = new OpenClawRpcClient(new OpenClawConfig(), NullLogger<OpenClawRpcClient>.Instance);
        _registrar = new OpenClawAgentRegistrar(_rpc, NullLogger<OpenClawAgentRegistrar>.Instance);
    }

    [Fact]
    public async Task RegisterAgentsAsync_WhenNotConnected_LogsWarningAndReturns()
    {
        var agents = new List<JdAiAgentDefinition>
        {
            new() { Id = "test-agent", Name = "Test Agent" },
        };

        await _registrar.RegisterAgentsAsync(agents);

        Assert.Empty(_registrar.RegisteredAgentIds);
    }

    [Fact]
    public async Task UnregisterAgentsAsync_WhenNotConnected_ReturnsGracefully()
    {
        await _registrar.UnregisterAgentsAsync();
        Assert.Empty(_registrar.RegisteredAgentIds);
    }

    [Fact]
    public async Task UnregisterAgentsAsync_WhenNoAgentsRegistered_ReturnsGracefully()
    {
        await _registrar.UnregisterAgentsAsync();
        Assert.Empty(_registrar.RegisteredAgentIds);
    }

    [Fact]
    public void RegisteredAgentIds_InitiallyEmpty()
    {
        Assert.Empty(_registrar.RegisteredAgentIds);
    }

    [Fact]
    public void JdAiAgentDefinition_DefaultValues()
    {
        var def = new JdAiAgentDefinition { Id = "test" };

        Assert.Equal("test", def.Id);
        Assert.Equal("", def.Name);
        Assert.Equal("🤖", def.Emoji);
        Assert.Equal("JD.AI agent", def.Theme);
        Assert.Null(def.SystemPrompt);
        Assert.Null(def.Model);
        Assert.Empty(def.Tools);
        Assert.Empty(def.Bindings);
    }

    [Fact]
    public void JdAiAgentDefinition_FullyConfigured()
    {
        var def = new JdAiAgentDefinition
        {
            Id = "coder",
            Name = "JD.AI Coder",
            Emoji = "💻",
            Theme = "Expert coder",
            SystemPrompt = "You are a coding expert.",
            Model = "ollama/llama3.2",
            Tools = ["read", "write", "exec"],
            Bindings =
            [
                new AgentBinding
                {
                    Channel = "discord",
                    AccountId = "default",
                    GuildId = "123456",
                    Peer = new AgentBindingPeer { Kind = "direct", Id = "user1" },
                },
            ],
        };

        Assert.Equal("coder", def.Id);
        Assert.Equal("JD.AI Coder", def.Name);
        Assert.Equal("💻", def.Emoji);
        Assert.Equal("Expert coder", def.Theme);
        Assert.Equal("You are a coding expert.", def.SystemPrompt);
        Assert.Equal("ollama/llama3.2", def.Model);
        Assert.Equal(3, def.Tools.Count);
        Assert.Single(def.Bindings);
        Assert.Equal("discord", def.Bindings[0].Channel);
        Assert.Equal("default", def.Bindings[0].AccountId);
        Assert.Equal("123456", def.Bindings[0].GuildId);
        Assert.NotNull(def.Bindings[0].Peer);
        Assert.Equal("direct", def.Bindings[0].Peer!.Kind);
        Assert.Equal("user1", def.Bindings[0].Peer!.Id);
    }

    [Fact]
    public void AgentBinding_DefaultValues()
    {
        var binding = new AgentBinding { Channel = "signal" };

        Assert.Equal("signal", binding.Channel);
        Assert.Null(binding.AccountId);
        Assert.Null(binding.Peer);
        Assert.Null(binding.GuildId);
    }

    [Fact]
    public void AgentBindingPeer_DefaultKind()
    {
        var peer = new AgentBindingPeer { Id = "user123" };

        Assert.Equal("direct", peer.Kind);
        Assert.Equal("user123", peer.Id);
    }
}
