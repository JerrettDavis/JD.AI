using JD.AI.Channels.OpenClaw;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json.Nodes;

namespace JD.AI.Tests.Channels;

public sealed class OpenClawAgentRegistrarTests : IDisposable
{
    private readonly OpenClawRpcClient _rpc;
    private readonly OpenClawAgentRegistrar _registrar;

    public OpenClawAgentRegistrarTests()
    {
        // Real RPC client, unconnected — IsConnected = false
        _rpc = new OpenClawRpcClient(new OpenClawConfig(), NullLogger<OpenClawRpcClient>.Instance);
        _registrar = new OpenClawAgentRegistrar(_rpc, NullLogger<OpenClawAgentRegistrar>.Instance);
    }

    public void Dispose() => (_rpc as IAsyncDisposable)?.DisposeAsync().AsTask().GetAwaiter().GetResult();

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
    public async Task RegisterAgentsAsync_WhenAgentListIsEmpty_ReturnsBeforeConfigRead()
    {
        SetRpcConnected(true);

        await _registrar.RegisterAgentsAsync([]);

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

    [Fact]
    public void AgentIdPrefix_IsJdai()
    {
        Assert.Equal("jdai-", OpenClawAgentRegistrar.AgentIdPrefix);
    }

    [Fact]
    public void BackupPath_IsInJdaiDirectory()
    {
        var backupDir = OpenClawAgentRegistrar.ConfigBackupDirectory;

        Assert.Contains(".jdai", backupDir);
        Assert.Contains("openclaw-config-backups", backupDir);
    }

    [Fact]
    public void RemoveManagedAgentsAndBindings_RemovesJdAiEntries_ByPrefix()
    {
        var config = JsonNode.Parse(
            """
            {
              "agents": {
                "list": [
                  { "id": "jdai-default", "name": "JD.AI Default" },
                  { "id": "native-assistant", "name": "Native Assistant" },
                  { "id": "jdai-research", "name": "JD.AI Research" }
                ]
              },
              "bindings": [
                { "agentId": "jdai-default", "match": { "channel": "signal" } },
                { "agentId": "native-assistant", "match": { "channel": "discord" } }
              ]
            }
            """)!;

        var (removedAgents, removedBindings) = InvokeRemoveManagedAgentsAndBindings(config);

        Assert.Equal(2, removedAgents);
        Assert.Equal(1, removedBindings);

        var remainingAgentIds = config["agents"]!["list"]!.AsArray()
            .Select(node => node?["id"]?.GetValue<string>())
            .ToArray();
        Assert.Single(remainingAgentIds);
        Assert.Equal("native-assistant", remainingAgentIds[0]);

        var remainingBindingAgentIds = config["bindings"]!.AsArray()
            .Select(node => node?["agentId"]?.GetValue<string>())
            .ToArray();
        Assert.Single(remainingBindingAgentIds);
        Assert.Equal("native-assistant", remainingBindingAgentIds[0]);
    }

    [Fact]
    public void RemoveManagedAgentsAndBindings_RemovesEmptyCollections_WhenAllManaged()
    {
        var config = JsonNode.Parse(
            """
            {
              "agents": {
                "list": [
                  { "id": "jdai-default" }
                ]
              },
              "bindings": [
                { "agentId": "jdai-default", "match": { "channel": "signal" } }
              ]
            }
            """)!;

        var (removedAgents, removedBindings) = InvokeRemoveManagedAgentsAndBindings(config);

        Assert.Equal(1, removedAgents);
        Assert.Equal(1, removedBindings);
        Assert.Null(config["agents"]?["list"]);
        Assert.Null(config["bindings"]);
    }

    [Fact]
    public void RemoveManagedAgentsAndBindings_WhenCollectionsAreMissing_ReturnsZeroCounts()
    {
        var config = JsonNode.Parse("""{"name":"untouched"}""")!;

        var (removedAgents, removedBindings) = InvokeRemoveManagedAgentsAndBindings(config);

        Assert.Equal(0, removedAgents);
        Assert.Equal(0, removedBindings);
        Assert.Equal("untouched", config["name"]!.GetValue<string>());
    }

    [Fact]
    public void RemoveManagedAgentsAndBindings_RemovesExplicitManagedIds_WhenNotPrefixed()
    {
        var config = JsonNode.Parse(
            """
            {
              "agents": {
                "list": [
                  { "id": "custom-jdai", "name": "Custom JD.AI agent" },
                  { "id": "native-assistant", "name": "Native Assistant" }
                ]
              },
              "bindings": [
                { "agentId": "custom-jdai", "match": { "channel": "signal" } },
                { "agentId": "native-assistant", "match": { "channel": "discord" } }
              ]
            }
            """)!;

        var (removedAgents, removedBindings) = InvokeRemoveManagedAgentsAndBindings(
            config,
            ["custom-jdai"]);

        Assert.Equal(1, removedAgents);
        Assert.Equal(1, removedBindings);
        Assert.Equal("native-assistant", config["agents"]!["list"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("native-assistant", config["bindings"]![0]!["agentId"]!.GetValue<string>());
    }

    [Fact]
    public void EnsureDefaultMainAgent_WhenNoAgentsExist_AddsMainDefaultAgent()
    {
        var config = JsonNode.Parse("""{ "agents": {} }""")!;

        var changed = OpenClawAgentRegistrar.EnsureDefaultMainAgent(config);

        Assert.True(changed);
        Assert.Equal("main", config["agents"]!["list"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("Assistant", config["agents"]!["list"]![0]!["name"]!.GetValue<string>());
        Assert.True(config["agents"]!["list"]![0]!["default"]!.GetValue<bool>());
    }

    [Fact]
    public void EnsureDefaultMainAgent_WhenMainExistsWithoutDefault_MarksMainAsDefault()
    {
        var config = JsonNode.Parse(
            """
            {
              "agents": {
                "list": [
                  { "id": "main", "name": "Main Assistant" },
                  { "id": "native-assistant", "name": "Native Assistant" }
                ]
              }
            }
            """)!;

        var changed = OpenClawAgentRegistrar.EnsureDefaultMainAgent(config);

        Assert.True(changed);
        Assert.True(config["agents"]!["list"]![0]!["default"]!.GetValue<bool>());
    }

    [Fact]
    public void EnsureDefaultMainAgent_WhenDefaultAlreadySet_DoesNotModifyConfig()
    {
        var config = JsonNode.Parse(
            """
            {
              "agents": {
                "list": [
                  { "id": "native-assistant", "name": "Native Assistant", "default": true }
                ]
              }
            }
            """)!;

        var changed = OpenClawAgentRegistrar.EnsureDefaultMainAgent(config);

        Assert.False(changed);
        Assert.True(config["agents"]!["list"]![0]!["default"]!.GetValue<bool>());
        Assert.Equal("native-assistant", config["agents"]!["list"]![0]!["id"]!.GetValue<string>());
    }

    private static (int RemovedAgents, int RemovedBindings) InvokeRemoveManagedAgentsAndBindings(JsonNode config)
    {
        var method = typeof(OpenClawAgentRegistrar).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, "RemoveManagedAgentsAndBindings", StringComparison.Ordinal) && m.GetParameters().Length == 1);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [config]);
        Assert.NotNull(result);
        return ((int RemovedAgents, int RemovedBindings))result!;
    }

    private static (int RemovedAgents, int RemovedBindings) InvokeRemoveManagedAgentsAndBindings(
        JsonNode config,
        IEnumerable<string> managedIds)
    {
        var method = typeof(OpenClawAgentRegistrar).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, "RemoveManagedAgentsAndBindings", StringComparison.Ordinal) && m.GetParameters().Length == 2);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [config, managedIds]);
        Assert.NotNull(result);
        return ((int RemovedAgents, int RemovedBindings))result!;
    }

    private void SetRpcConnected(bool connected)
    {
        var field = typeof(OpenClawRpcClient).GetField("<IsConnected>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(_rpc, connected);
    }
}
