using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Commands;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using NSubstitute;
using System.Reflection;

namespace JD.AI.Gateway.Tests.Commands;

public class GatewayCommandTests
{
    private readonly IProviderRegistry _providers;
    private readonly AgentPoolService _pool;
    private readonly ChannelRegistry _channels;
    private readonly AgentRouter _router;
    private readonly GatewayConfig _config;
    private static readonly Microsoft.Extensions.Logging.ILogger<ProviderCommand> ProviderCommandLogger =
        NullLogger<ProviderCommand>.Instance;

    public GatewayCommandTests()
    {
        _providers = Substitute.For<IProviderRegistry>();

        var ollamaModel = new ProviderModelInfo("llama3.2:latest", "llama3.2:latest", "Ollama");
        var copilotModel = new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "GitHub Copilot");

        var detectedProviders = new List<ProviderInfo>
        {
            new("Ollama", true, null, [ollamaModel]),
            new("GitHub Copilot", true, null, [copilotModel]),
            new("Claude", false, "offline", [])
        };

        var detector = Substitute.For<IProviderDetector>();
        detector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(_ => Kernel.CreateBuilder().Build());

        _providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ProviderInfo>>(detectedProviders));
        _providers.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ProviderModelInfo>>([ollamaModel, copilotModel]));
        _providers.GetDetector(Arg.Any<string>()).Returns(detector);

        var eventBus = Substitute.For<IEventBus>();
        _pool = new AgentPoolService(_providers, new ChannelRegistry(), eventBus, NullLogger<AgentPoolService>.Instance);
        _channels = new ChannelRegistry();
        _router = new AgentRouter(_pool, _channels, eventBus, NullLogger<AgentRouter>.Instance);
        _config = new GatewayConfig
        {
            Providers =
            [
                new ProviderConfig { Name = "Ollama", Enabled = true },
                new ProviderConfig { Name = "Claude", Enabled = false }
            ],
            Agents =
            [
                new AgentDefinition { Id = "default", Provider = "Ollama", Model = "llama3.2" }
            ]
        };
    }

    private static CommandContext MakeContext(string name, Dictionary<string, string>? args = null) => new()
    {
        CommandName = name,
        InvokerId = "user123",
        ChannelId = "ch456",
        ChannelType = "discord",
        Arguments = args ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    private ProviderCommand CreateProviderCommand() => new(_router, _pool, _providers, ProviderCommandLogger);

    // --- Route commands ---

    [Fact]
    public async Task RouteCommand_RemapsCurrentChannel_WhenAgentMatchExists()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "ollama"
        }));

        result.Success.Should().BeTrue();
        _router.GetAgentForChannel("ch456").Should().Be(agentId);
        _router.GetAgentForChannel("discord").Should().BeNull();
        result.Content.Should().Contain("now routes");
    }

    [Fact]
    public async Task RouteCommand_WhenAgentMatchIsAmbiguous_ReturnsCandidates()
    {
        await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "ollama"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Ambiguous agent");
        result.Content.Should().Contain("Ollama/llama3.2:latest");
    }

    [Fact]
    public async Task RouteCommand_WhenAgentMatchIsMissing_ReturnsAvailableAgents()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "missing-agent"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("No agent matching **missing-agent** found");
        result.Content.Should().Contain(agentId[..8]);
        result.Content.Should().Contain("Ollama/llama3.2:latest");
    }

    [Fact]
    public async Task RouteCommand_WithoutArgument_ShowsCurrentRoute()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("📡 **ch456** → Ollama/llama3.2:latest");
        result.Content.Should().Contain(agentId[..8]);
    }

    [Fact]
    public async Task RouteCommand_WithoutArgumentAndStaleMapping_ShowsMappedAgentIdPrefix()
    {
        _router.MapChannel("ch456", "deadbeef1234");
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("📡 **ch456** → `deadbeef`");
    }

    [Fact]
    public async Task RouteCommand_WithoutArgumentAndNoMapping_ShowsUnmappedHint()
    {
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("📡 **ch456** — No agent mapped.");
        result.Content.Should().Contain("/jdai-route <agent>");
    }

    [Fact]
    public async Task RouteCommand_WhenChannelIdIsBlank_UsesChannelTypeFallback()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(
            MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["agent"] = "ollama"
            }) with { ChannelId = "" });

        result.Success.Should().BeTrue();
        _router.GetAgentForChannel("discord").Should().Be(agentId);
        result.Content.Should().Contain("**discord** now routes");
    }

    [Fact]
    public async Task RouteCommand_WhenPartialAgentMatchIsUnique_RoutesChannel()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        await _pool.SpawnAgentAsync("GitHub Copilot", "gpt-5.3-codex", null, CancellationToken.None);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "3.2:lat"
        }));

        result.Success.Should().BeTrue();
        _router.GetAgentForChannel("ch456").Should().Be(agentId);
        result.Content.Should().Contain("Ollama/llama3.2:latest");
    }

    [Fact]
    public async Task RoutesCommand_ListsMappedChannels()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        var cmd = new RoutesCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("routes"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("ch456");
        result.Content.Should().Contain("Ollama/llama3.2:latest");
    }

    [Fact]
    public async Task ProviderCommand_SwitchesProvider_ForMappedChannel()
    {
        var initialAgentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", initialAgentId);
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "copilot"
        }));

        result.Success.Should().BeTrue();
        var mappedAgentId = _router.GetAgentForChannel("ch456");
        mappedAgentId.Should().NotBeNull();
        mappedAgentId.Should().NotBe(initialAgentId);

        var mappedAgent = _pool.ListAgents().Single(a =>
            string.Equals(a.Id, mappedAgentId, StringComparison.Ordinal));
        mappedAgent.Provider.Should().Be("GitHub Copilot");
        mappedAgent.Model.Should().Be("gpt-5.3-codex");
        _pool.ListAgents().Should().NotContain(a =>
            string.Equals(a.Id, initialAgentId, StringComparison.Ordinal));

        result.Content.Should().Contain("Switched to **GitHub Copilot**");
    }

    [Fact]
    public async Task ProviderCommand_WithoutMappedAgent_ShowsRouteHint()
    {
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agent mapped to this channel");
        result.Content.Should().Contain("/jdai-route");
    }

    [Fact]
    public async Task ProviderCommand_WithoutName_ShowsCurrentProviderDetails()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**Ollama**");
        result.Content.Should().Contain("`llama3.2:latest`");
    }

    [Fact]
    public async Task ProviderCommand_WithoutNameAndStaleMapping_ShowsUnknownProviderFallback()
    {
        _router.MapChannel("ch456", "deadbeef1234");
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Agent `deadbeef`");
        result.Content.Should().Contain("provider unknown");
    }

    [Fact]
    public async Task RouteCommand_RemapsOnlyTheCurrentChannel()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var otherAgentId = await _pool.SpawnAgentAsync("GitHub Copilot", "gpt-5.3-codex", null, CancellationToken.None);
        var otherChannelContext = MakeContext("route") with { ChannelId = "ch999" };
        _router.MapChannel(otherChannelContext.ChannelId, otherAgentId);
        var cmd = new RouteCommand(_router, _pool);

        var result = await cmd.ExecuteAsync(MakeContext("route", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "ollama"
        }));

        result.Success.Should().BeTrue();
        _router.GetAgentForChannel("ch456").Should().Be(agentId);
        _router.GetAgentForChannel(otherChannelContext.ChannelId).Should().Be(otherAgentId);
    }

    [Fact]
    public async Task ProviderCommand_WhenProviderIsOffline_ReturnsAvailableProviders()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "Claude"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Provider **Claude** not found or offline");
        result.Content.Should().Contain("**Ollama**");
        result.Content.Should().Contain("**GitHub Copilot**");
    }

    [Fact]
    public async Task ProviderCommand_WhenMatchedProviderHasNoModels_ReturnsError()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        _providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo("TestProvider", true, null, [])
            ]));
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "Test"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("**TestProvider** has no available models");
    }

    [Fact]
    public async Task ProviderCommand_WithNameAndNoMappedAgent_ReturnsRouteHint()
    {
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "copilot"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("No agent mapped to this channel");
        result.Content.Should().Contain("/jdai-route <agent>");
    }

    [Fact]
    public async Task ProviderCommand_WhenProviderMatchIsAmbiguous_ReturnsCandidates()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        _providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo("GitHub Copilot", true, null,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "GitHub Copilot")
                ]),
                new ProviderInfo("GitHub Copilot Enterprise", true, null,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "GitHub Copilot Enterprise")
                ])
            ]));
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "copilot"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Ambiguous provider");
        result.Content.Should().Contain("**GitHub Copilot**");
        result.Content.Should().Contain("**GitHub Copilot Enterprise**");
    }

    [Fact]
    public async Task ProviderCommand_WhenExactProviderMatchExists_PrefersItOverPartialMatches()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        _providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo("GitHub Copilot", true, null,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "GitHub Copilot")
                ]),
                new ProviderInfo("GitHub Copilot Enterprise", true, null,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "GitHub Copilot Enterprise")
                ])
            ]));
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "GitHub Copilot"
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Switched to **GitHub Copilot**");
        result.Content.Should().NotContain("Enterprise");
    }

    [Fact]
    public async Task ProviderCommand_WhenChannelIdIsBlank_UsesChannelTypeFallback()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("discord", agentId);
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(
            MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "copilot"
            }) with { ChannelId = "" });

        result.Success.Should().BeTrue();
        _router.GetAgentForChannel("discord").Should().NotBe(agentId);
        result.Content.Should().Contain("Channel **discord** now routes");
    }

    [Fact]
    public async Task ProviderCommand_WhenSpawnFails_HidesInternalExceptionDetails()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("ch456", agentId);
        _providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo("BrokenProvider", true, null,
                [
                    new ProviderModelInfo("broken-model", "broken-model", "BrokenProvider")
                ])
            ]));
        _providers.GetDetector("BrokenProvider").Returns((IProviderDetector?)null);
        var cmd = CreateProviderCommand();

        var result = await cmd.ExecuteAsync(MakeContext("provider", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "BrokenProvider"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Be("❌ Failed to switch provider.");
    }

    // --- StatusCommand ---

    [Fact]
    public async Task StatusCommand_ShowsChannelAndAgentInfo()
    {
        var cmd = new StatusCommand(_pool, _channels);

        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("System Status");
        result.Content.Should().Contain("Channels:");
        result.Content.Should().Contain("Agents:");
    }

    [Fact]
    public async Task StatusCommand_ShowsConnectedChannels()
    {
        var mockChannel = Substitute.For<IChannel>();
        mockChannel.ChannelType.Returns("discord");
        mockChannel.DisplayName.Returns("Discord");
        mockChannel.IsConnected.Returns(true);
        _channels.Register(mockChannel);

        var cmd = new StatusCommand(_pool, _channels);
        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Content.Should().Contain("Discord");
        result.Content.Should().Contain("Connected");
    }

    [Fact]
    public async Task StatusCommand_ShowsDisconnectedChannels()
    {
        var mockChannel = Substitute.For<IChannel>();
        mockChannel.ChannelType.Returns("slack");
        mockChannel.DisplayName.Returns("Slack");
        mockChannel.IsConnected.Returns(false);
        _channels.Register(mockChannel);

        var cmd = new StatusCommand(_pool, _channels);
        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Slack");
        result.Content.Should().Contain("Disconnected");
    }

    [Fact]
    public async Task StatusCommand_WithRunningAgent_ShowsAgentDetails()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new StatusCommand(_pool, _channels);

        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain($"• `{agentId[..8]}` — Ollama/llama3.2:latest — 0 turns — up ");
        result.Content.Should().NotContain("No agents running.");
    }

    [Theory]
    [InlineData(45, "45m")]
    [InlineData(120, "2h")]
    public void StatusCommand_FormatAge_FormatsMinutesAndHours(double minutes, string expected)
    {
        var method = typeof(StatusCommand).GetMethod("FormatAge", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var formatted = method!.Invoke(null, [TimeSpan.FromMinutes(minutes)]);

        formatted.Should().Be(expected);
    }

    // --- UsageCommand ---

    [Fact]
    public async Task UsageCommand_ShowsUptimeAndAgentCount()
    {
        var cmd = new UsageCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("usage"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Usage Statistics");
        result.Content.Should().Contain("🕐 **Uptime:**");
        result.Content.Should().Contain("🤖 **Active Agents:** 0");
        result.Content.Should().Contain("💬 **Total Turns:** 0");
        result.Content.Should().NotContain("**Per-Agent Breakdown:**");
    }

    [Fact]
    public async Task UsageCommand_WithRunningAgent_ShowsPerAgentBreakdown()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new UsageCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("usage"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**Per-Agent Breakdown:**");
        result.Content.Should().Contain($"• `{agentId[..8]}` (Ollama/llama3.2:latest) — 0 turns");
        result.Content.Should().Contain("🤖 **Active Agents:** 1");
        result.Content.Should().Contain("💬 **Total Turns:** 0");
    }

    [Theory]
    [InlineData(0, 0, 45, "45m 0s")]
    [InlineData(0, 2, 0, "2h 0m")]
    [InlineData(1, 2, 3, "1d 2h 3m")]
    public void UsageCommand_FormatUptime_FormatsMinuteHourAndDayRanges(int days, int hours, int minutes, string expected)
    {
        var formatted = UsageCommand.FormatUptime(new TimeSpan(days, hours, minutes, 0));
        formatted.Should().Be(expected);
    }

    // --- ModelsCommand ---

    [Fact]
    public async Task ModelsCommand_ShowsProvidersAndAgentDefs()
    {
        var cmd = new ModelsCommand(_pool, _config);

        var result = await cmd.ExecuteAsync(MakeContext("models"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Ollama");
        result.Content.Should().Contain("default");
        result.Content.Should().Contain("llama3.2");
    }

    [Fact]
    public async Task ModelsCommand_WithRunningAgent_ShowsActiveAgentSection()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new ModelsCommand(_pool, _config);

        var result = await cmd.ExecuteAsync(MakeContext("models"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**Running Agents:**");
        result.Content.Should().Contain($"• `{agentId[..8]}` — Ollama/llama3.2:latest (active)");
        result.Content.Should().NotContain("📦 **Claude**");
    }

    [Fact]
    public async Task ModelsCommand_WithNullProviderAndAgentConfig_UsesEmptySections()
    {
        var cmd = new ModelsCommand(_pool, new GatewayConfig
        {
            Providers = null!,
            Agents = null!
        });

        var result = await cmd.ExecuteAsync(MakeContext("models"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**Available Models**");
        result.Content.Should().Contain("**Configured Agent Models:**");
        result.Content.Should().NotContain("📦 **");
        result.Content.Should().NotContain("• `default`");
        result.Content.Should().NotContain("**Running Agents:**");
    }

    // --- AgentsCommand ---

    [Fact]
    public async Task AgentsCommand_WhenNoAgents_ShowsMessage()
    {
        var cmd = new AgentsCommand(_pool, _router);

        var result = await cmd.ExecuteAsync(MakeContext("agents"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agents are running");
    }

    [Fact]
    public async Task AgentsCommand_WithRunningAgentAndRoutes_ShowsAgentDetailsAndRoutingTable()
    {
        var agentId = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        _router.MapChannel("discord", agentId);
        _router.MapChannel("slack", agentId);
        var cmd = new AgentsCommand(_pool, _router);

        var result = await cmd.ExecuteAsync(MakeContext("agents"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain($"🤖 **`{agentId[..8]}`** — Ollama/llama3.2:latest");
        result.Content.Should().Contain("Turns: 0 | Up:");
        result.Content.Should().Contain("Routes: discord, slack");
        result.Content.Should().Contain("**Routing Table:**");
        result.Content.Should().Contain($"• discord → `{agentId[..8]}`");
        result.Content.Should().Contain($"• slack → `{agentId[..8]}`");
    }

    [Fact]
    public async Task AgentsCommand_WithShortMappedAgentId_ShowsFullIdInRoutingTable()
    {
        _router.MapChannel("discord", "abc");
        var cmd = new AgentsCommand(_pool, _router);

        var result = await cmd.ExecuteAsync(MakeContext("agents"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agents are running.");
        result.Content.Should().Contain("**Routing Table:**");
        result.Content.Should().Contain("• discord → `abc`");
    }

    [Theory]
    [InlineData(45, "45m")]
    [InlineData(120, "2h")]
    public void AgentsCommand_FormatAge_FormatsMinutesAndHours(double minutes, string expected)
    {
        var method = typeof(AgentsCommand).GetMethod("FormatAge", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var formatted = method!.Invoke(null, [TimeSpan.FromMinutes(minutes)]);

        formatted.Should().Be(expected);
    }

    // --- ClearCommand ---

    [Fact]
    public async Task ClearCommand_WhenNoAgents_ShowsInfo()
    {
        var cmd = new ClearCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("clear"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agents are running");
    }

    [Fact]
    public async Task ClearCommand_WithoutFilter_ClearsAllAgents()
    {
        await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        await _pool.SpawnAgentAsync("GitHub Copilot", "gpt-5.3-codex", null, CancellationToken.None);
        var cmd = new ClearCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("clear"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Cleared conversation history for 2 agent(s).");
        _pool.ListAgents().Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearCommand_WithMatchingPrefix_ClearsOnlyMatchingAgents()
    {
        var matchingAgent = await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        await _pool.SpawnAgentAsync("GitHub Copilot", "gpt-5.3-codex", null, CancellationToken.None);
        var cmd = new ClearCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("clear", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = matchingAgent[..6].ToUpperInvariant()
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Cleared conversation history for 1 agent(s).");
        _pool.ListAgents().Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearCommand_WithUnknownPrefix_ReturnsNotFound()
    {
        await _pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        var cmd = new ClearCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("clear", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent"] = "missing"
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agent found matching `missing`.");
        _pool.ListAgents().Should().ContainSingle();
    }

    // --- SwitchCommand ---

    [Fact]
    public async Task SwitchCommand_WithoutModel_ReturnsError()
    {
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch"));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("specify a model");
    }

    [Fact]
    public async Task SwitchCommand_WithoutProviderAndNoAgents_DefaultsToOllama()
    {
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "llama3.2:latest"
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**Ollama/llama3.2:latest**");
        _pool.ListAgents().Should().ContainSingle(a =>
            a.Provider == "Ollama" &&
            a.Model == "llama3.2:latest");
    }

    [Fact]
    public async Task SwitchCommand_WithoutProvider_UsesFirstRunningAgentProvider()
    {
        await _pool.SpawnAgentAsync("GitHub Copilot", "gpt-5.3-codex", null, CancellationToken.None);
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "gpt-5.3-codex"
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**GitHub Copilot/gpt-5.3-codex**");
        _pool.ListAgents().Should().HaveCount(2);
        _pool.ListAgents().Should().Contain(a =>
            a.Provider == "GitHub Copilot" &&
            a.Model == "gpt-5.3-codex");
    }

    [Fact]
    public async Task SwitchCommand_WithExplicitProvider_UsesRequestedProvider()
    {
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "gpt-5.3-codex",
            ["provider"] = "GitHub Copilot"
        }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**GitHub Copilot/gpt-5.3-codex**");
        _pool.ListAgents().Should().ContainSingle(a =>
            a.Provider == "GitHub Copilot" &&
            a.Model == "gpt-5.3-codex");
    }

    [Fact]
    public async Task SwitchCommand_WhenSpawnFails_ReturnsError()
    {
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "missing-model",
            ["provider"] = "GitHub Copilot"
        }));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Failed to spawn agent");
        result.Content.Should().Contain("missing-model");
        _pool.ListAgents().Should().BeEmpty();
    }

    // --- HelpCommand integration ---

    [Fact]
    public async Task HelpCommand_ListsAllCommands()
    {
        var registry = new CommandRegistry();
        var helpCmd = new HelpCommand(registry);
        registry.Register(helpCmd);
        registry.Register(new UsageCommand(_pool));
        registry.Register(new StatusCommand(_pool, _channels));
        registry.Register(new RouteCommand(_router, _pool));
        registry.Register(new RoutesCommand(_router, _pool));
        registry.Register(new ProvidersCommand(_providers));
        registry.Register(CreateProviderCommand());

        var result = await helpCmd.ExecuteAsync(MakeContext("help"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("jdai-help");
        result.Content.Should().Contain("jdai-usage");
        result.Content.Should().Contain("jdai-status");
        result.Content.Should().Contain("jdai-route");
        result.Content.Should().Contain("jdai-routes");
        result.Content.Should().Contain("jdai-provider");
        result.Content.Should().Contain("jdai-providers");
    }
}
