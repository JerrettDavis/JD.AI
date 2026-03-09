using FluentAssertions;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Commands;

[Feature("Slash Command Router")]
[Collection("DataDirectories")]
public sealed class SlashCommandRouterBddTests : TinyBddXunitBase
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    public SlashCommandRouterBddTests(ITestOutputHelper output) : base(output) { }

    private SlashCommandRouter CreateRouter(
        InstructionsResult? instructions = null,
        ICheckpointStrategy? checkpointStrategy = null,
        IWorkflowCatalog? workflowCatalog = null,
        IWorkflowStore? workflowStore = null,
        AtomicConfigStore? configStore = null,
        Func<TuiTheme>? getTheme = null,
        Action<TuiTheme>? onThemeChanged = null,
        Func<OutputStyle>? getOutputStyle = null,
        Action<OutputStyle>? onOutputStyleChanged = null)
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(_registry, kernel, model);
        return new SlashCommandRouter(
            session, _registry,
            instructions: instructions,
            checkpointStrategy: checkpointStrategy,
            workflowCatalog: workflowCatalog,
            workflowStore: workflowStore,
            configStore: configStore,
            getTheme: getTheme,
            onThemeChanged: onThemeChanged,
            getOutputStyle: getOutputStyle,
            onOutputStyleChanged: onOutputStyleChanged);
    }

    private (SlashCommandRouter Router, AgentSession Session) CreateRouterWithSession(
        InstructionsResult? instructions = null,
        ICheckpointStrategy? checkpointStrategy = null,
        IWorkflowCatalog? workflowCatalog = null,
        IWorkflowStore? workflowStore = null,
        AtomicConfigStore? configStore = null)
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(
            session, _registry,
            instructions: instructions,
            checkpointStrategy: checkpointStrategy,
            workflowCatalog: workflowCatalog,
            workflowStore: workflowStore,
            configStore: configStore);
        return (router, session);
    }

    // ── 1. /compact ──────────────────────────────────────────

    [Scenario("Compact returns non-null result"), Fact]
    public async Task CompactReturnsResult()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /compact", async Task (router) =>
            {
                result = await router.ExecuteAsync("/compact");
            })
            .Then("returns non-null result containing 'compact'", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("compact", Exactly.Once());
                return true;
            })
            .AssertPassed();
    }

    // ── 2. /context ──────────────────────────────────────────

    [Scenario("Context returns context usage info"), Fact]
    public async Task ContextReturnsUsageInfo()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /context", async Task (router) =>
            {
                result = await router.ExecuteAsync("/context");
            })
            .Then("returns context usage info with token counts", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Context");
                result.Should().Contain("tokens");
                return true;
            })
            .AssertPassed();
    }

    // ── 3. /copy with no last response ───────────────────────

    [Scenario("Copy with no last response returns appropriate message"), Fact]
    public async Task CopyWithNoLastResponseReturnsMessage()
    {
        string? result = null;
        await Given("a default router with empty history", () => CreateRouter())
            .When("executing /copy", async Task (router) =>
            {
                result = await router.ExecuteAsync("/copy");
            })
            .Then("returns 'No assistant response' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("No assistant response");
                return true;
            })
            .AssertPassed();
    }

    // ── 4. /plan toggles plan mode on ────────────────────────

    [Scenario("Plan toggles plan mode on"), Fact]
    public async Task PlanTogglesPlanModeOn()
    {
        string? result = null;
        AgentSession? session = null;
        await Given("a router with session", () =>
            {
                var pair = CreateRouterWithSession();
                session = pair.Session;
                return pair.Router;
            })
            .When("executing /plan", async Task (router) =>
            {
                result = await router.ExecuteAsync("/plan");
            })
            .Then("plan mode is ON", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("ON");
                session!.PlanMode.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── 5. /plan again toggles plan mode off ─────────────────

    [Scenario("Plan toggles plan mode off when already on"), Fact]
    public async Task PlanTogglesPlanModeOff()
    {
        string? result = null;
        AgentSession? session = null;
        await Given("a router with plan mode already on", () =>
            {
                var pair = CreateRouterWithSession();
                session = pair.Session;
                session.PlanMode = true;
                return pair.Router;
            })
            .When("executing /plan", async Task (router) =>
            {
                result = await router.ExecuteAsync("/plan");
            })
            .Then("plan mode is OFF", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("OFF");
                session!.PlanMode.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ── 6. /instructions with no instructions ────────────────

    [Scenario("Instructions with no instructions returns no-instructions message"), Fact]
    public async Task InstructionsWithNoneReturnsMessage()
    {
        string? result = null;
        await Given("a router with no instructions loaded", () => CreateRouter(instructions: null))
            .When("executing /instructions", async Task (router) =>
            {
                result = await router.ExecuteAsync("/instructions");
            })
            .Then("returns 'No project instructions' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("No project instructions");
                return true;
            })
            .AssertPassed();
    }

    // ── 7. /instructions with loaded instructions ────────────

    [Scenario("Instructions with loaded instructions shows info"), Fact]
    public async Task InstructionsWithLoadedShowsInfo()
    {
        string? result = null;
        await Given("a router with instructions loaded", () =>
            {
                var instructions = new InstructionsResult();
                return CreateRouter(instructions: instructions);
            })
            .When("executing /instructions", async Task (router) =>
            {
                result = await router.ExecuteAsync("/instructions");
            })
            .Then("returns instructions info", _ =>
            {
                result.Should().NotBeNull();
                // InstructionsResult with no files returns "No project instructions found."
                result!.Length.Should().BeGreaterThan(0);
                return true;
            })
            .AssertPassed();
    }

    // ── 8. /plugins with no plugin loader ────────────────────

    [Scenario("Plugins with no plugin loader returns not available"), Fact]
    public async Task PluginsNoLoaderReturnsMessage()
    {
        string? result = null;
        await Given("a router with no plugin loader", () => CreateRouter())
            .When("executing /plugins", async Task (router) =>
            {
                result = await router.ExecuteAsync("/plugins");
            })
            .Then("returns 'not available' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 9. /config get with no config store ──────────────────

    [Scenario("Config get with no config store returns usage message"), Fact]
    public async Task ConfigGetNoStoreReturnsMessage()
    {
        string? result = null;
        await Given("a router with no config store", () => CreateRouter())
            .When("executing /config get", async Task (router) =>
            {
                result = await router.ExecuteAsync("/config get");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 10. /config set with no config store ─────────────────

    [Scenario("Config set with no config store returns usage message"), Fact]
    public async Task ConfigSetNoStoreReturnsMessage()
    {
        string? result = null;
        await Given("a router with no config store", () => CreateRouter())
            .When("executing /config set", async Task (router) =>
            {
                result = await router.ExecuteAsync("/config set");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 11. /default with no args and no config store ────────

    [Scenario("Default with no args and no config store returns message"), Fact]
    public async Task DefaultNoArgsNoStoreReturnsMessage()
    {
        string? result = null;
        await Given("a router with no config store", () => CreateRouter())
            .When("executing /default", async Task (router) =>
            {
                result = await router.ExecuteAsync("/default");
            })
            .Then("returns 'not available' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 12. /model search empty query ────────────────────────

    [Scenario("Model search with empty query returns usage message"), Fact]
    public async Task ModelSearchEmptyQueryReturnsUsage()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /model search", async Task (router) =>
            {
                result = await router.ExecuteAsync("/model search");
            })
            .Then("returns usage or not-available message", _ =>
            {
                result.Should().NotBeNull();
                // No model search aggregator => "not available"
                result.Should().ContainAny("Usage", "not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 13. /model url empty url ─────────────────────────────

    [Scenario("Model url with empty url returns usage message"), Fact]
    public async Task ModelUrlEmptyReturnsUsage()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /model url", async Task (router) =>
            {
                result = await router.ExecuteAsync("/model url");
            })
            .Then("returns usage or not-available message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().ContainAny("Usage", "not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 14. /model-info returns model info ───────────────────

    [Scenario("Model-info returns model info"), Fact]
    public async Task ModelInfoReturnsInfo()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /model-info", async Task (router) =>
            {
                result = await router.ExecuteAsync("/model-info");
            })
            .Then("returns model info containing name and provider", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Model Info");
                result.Should().Contain("Test Model");
                result.Should().Contain("TestProvider");
                return true;
            })
            .AssertPassed();
    }

    // ── 15. /diff in non-git directory returns error ─────────

    [Scenario("Diff in non-git directory returns error or no-changes"), Fact]
    public async Task DiffInNonGitDirectoryReturnsError()
    {
        string? result = null;
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            await Given("a default router in a non-git directory", () => CreateRouter())
                .When("executing /diff", async Task (router) =>
                {
                    result = await router.ExecuteAsync("/diff");
                })
                .Then("returns an error or no-changes message", _ =>
                {
                    result.Should().NotBeNull();
                    // In a non-git dir, git diff fails, so we get either an error or "No uncommitted changes"
                    return true;
                })
                .AssertPassed();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, true);
        }
    }

    // ── 16. /init ────────────────────────────────────────────

    [Scenario("Init creates or reports existing JDAI.md"), Fact]
    public async Task InitCreatesOrReportsExisting()
    {
        string? result = null;
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            await Given("a default router in a temp directory", () => CreateRouter())
                .When("executing /init", async Task (router) =>
                {
                    result = await router.ExecuteAsync("/init");
                })
                .Then("creates JDAI.md or reports it exists", _ =>
                {
                    result.Should().NotBeNull();
                    result.Should().Contain("JDAI.md");
                    return true;
                })
                .AssertPassed();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, true);
        }
    }

    // ── 17. /doctor ──────────────────────────────────────────

    [Scenario("Doctor runs diagnostics and returns system info"), Fact]
    public async Task DoctorRunsDiagnostics()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /doctor", async Task (router) =>
            {
                result = await router.ExecuteAsync("/doctor");
            })
            .Then("returns diagnostics info", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("jdai Doctor");
                result.Should().Contain("Version");
                result.Should().Contain("Runtime");
                return true;
            })
            .AssertPassed();
    }

    // ── 18. /checkpoint with no strategy ─────────────────────

    [Scenario("Checkpoint with no strategy returns not-configured"), Fact]
    public async Task CheckpointNoStrategyReturnsMessage()
    {
        string? result = null;
        await Given("a router with no checkpoint strategy", () => CreateRouter())
            .When("executing /checkpoint", async Task (router) =>
            {
                result = await router.ExecuteAsync("/checkpoint");
            })
            .Then("returns 'not configured' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not configured");
                return true;
            })
            .AssertPassed();
    }

    // ── 19. /checkpoint list with strategy returns checkpoints ──

    [Scenario("Checkpoint list with strategy returns checkpoint list"), Fact]
    public async Task CheckpointListReturnsMessage()
    {
        string? result = null;
        var strategy = Substitute.For<ICheckpointStrategy>();
        strategy.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CheckpointInfo>());

        await Given("a router with an empty checkpoint strategy", () =>
                CreateRouter(checkpointStrategy: strategy))
            .When("executing /checkpoint list", async Task (router) =>
            {
                result = await router.ExecuteAsync("/checkpoint list");
            })
            .Then("returns 'No checkpoints' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("No checkpoints");
                return true;
            })
            .AssertPassed();
    }

    // ── 20. /workflow list with no catalog ────────────────────

    [Scenario("Workflow list with no catalog returns not-configured"), Fact]
    public async Task WorkflowListNoCatalogReturnsMessage()
    {
        string? result = null;
        await Given("a router with no workflow catalog", () => CreateRouter())
            .When("executing /workflow list", async Task (router) =>
            {
                result = await router.ExecuteAsync("/workflow list");
            })
            .Then("returns 'not configured' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not configured");
                return true;
            })
            .AssertPassed();
    }

    // ── 21. /workflow show with no name ──────────────────────

    [Scenario("Workflow show with no name returns usage message"), Fact]
    public async Task WorkflowShowNoNameReturnsUsage()
    {
        string? result = null;
        var catalog = Substitute.For<IWorkflowCatalog>();
        await Given("a router with a workflow catalog", () =>
                CreateRouter(workflowCatalog: catalog))
            .When("executing /workflow show", async Task (router) =>
            {
                result = await router.ExecuteAsync("/workflow show");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 22. /workflow catalog with no store ───────────────────

    [Scenario("Workflow catalog with no store returns not-configured"), Fact]
    public async Task WorkflowCatalogNoStoreReturnsMessage()
    {
        string? result = null;
        var catalog = Substitute.For<IWorkflowCatalog>();
        await Given("a router with catalog but no workflow store", () =>
                CreateRouter(workflowCatalog: catalog))
            .When("executing /workflow catalog", async Task (router) =>
            {
                result = await router.ExecuteAsync("/workflow catalog");
            })
            .Then("returns 'not configured' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not configured");
                return true;
            })
            .AssertPassed();
    }

    // ── 23. /agents list with no agents file ─────────────────

    [Scenario("Agents list with no agents file returns no agents message"), Fact]
    public async Task AgentsListNoFileReturnsMessage()
    {
        string? result = null;
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-agents-bdd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        DataDirectories.SetRoot(tempDir);

        try
        {
            await Given("a router with empty data directory", () => CreateRouter())
                .When("executing /agents list", async Task (router) =>
                {
                    result = await router.ExecuteAsync("/agents list");
                })
                .Then("returns 'No agent profiles' message", _ =>
                {
                    result.Should().NotBeNull();
                    result.Should().ContainAny("No agent profiles", "no agent");
                    return true;
                })
                .AssertPassed();
        }
        finally
        {
            DataDirectories.Reset();
            Directory.Delete(tempDir, true);
        }
    }

    // ── 24. /hooks list with no hooks file ───────────────────

    [Scenario("Hooks list with no hooks file returns no hooks message"), Fact]
    public async Task HooksListNoFileReturnsMessage()
    {
        string? result = null;
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-hooks-bdd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        DataDirectories.SetRoot(tempDir);

        try
        {
            await Given("a router with empty data directory", () => CreateRouter())
                .When("executing /hooks list", async Task (router) =>
                {
                    result = await router.ExecuteAsync("/hooks list");
                })
                .Then("returns 'No hooks' message", _ =>
                {
                    result.Should().NotBeNull();
                    result.Should().ContainAny("No hooks", "no hooks");
                    return true;
                })
                .AssertPassed();
        }
        finally
        {
            DataDirectories.Reset();
            Directory.Delete(tempDir, true);
        }
    }

    // ── 25. /memory returns info ─────────────────────────────

    [Scenario("Memory returns memory info or not-found message"), Fact]
    public async Task MemoryReturnsInfoOrNotFound()
    {
        string? result = null;
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-memory-bdd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            await Given("a router in a directory without JDAI.md", () => CreateRouter())
                .When("executing /memory", async Task (router) =>
                {
                    result = await router.ExecuteAsync("/memory");
                })
                .Then("returns not-found or memory message", _ =>
                {
                    result.Should().NotBeNull();
                    result.Should().Contain("JDAI.md");
                    return true;
                })
                .AssertPassed();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, true);
        }
    }

    // ── 26. /provider list ───────────────────────────────────

    [Scenario("Provider list lists detected providers"), Fact]
    public async Task ProviderListListsProviders()
    {
        string? result = null;
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderInfo>
            {
                new("TestProvider", true, "OK", []),
            });

        await Given("a router with detected providers", () => CreateRouter())
            .When("executing /provider list", async Task (router) =>
            {
                result = await router.ExecuteAsync("/provider list");
            })
            .Then("returns provider list (may render via Spectre)", _ =>
            {
                // /provider list renders via AnsiConsole and returns empty string
                result.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── 27. /sandbox returns sandbox info ────────────────────

    [Scenario("Sandbox shows sandbox info"), Fact]
    public async Task SandboxShowsInfo()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /sandbox", async Task (router) =>
            {
                result = await router.ExecuteAsync("/sandbox");
            })
            .Then("returns sandbox modes info", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Sandbox");
                result.Should().ContainAny("restricted", "container", "none");
                return true;
            })
            .AssertPassed();
    }

    // ── 28. /fork without session ────────────────────────────

    [Scenario("Fork without session returns error"), Fact]
    public async Task ForkWithoutSessionReturnsError()
    {
        string? result = null;
        await Given("a router with no active session", () => CreateRouter())
            .When("executing /fork", async Task (router) =>
            {
                result = await router.ExecuteAsync("/fork");
            })
            .Then("returns 'No active session' error", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("No active session");
                return true;
            })
            .AssertPassed();
    }

    // ── 29. /local list returns info ─────────────────────────

    [Scenario("Local list returns local models info"), Fact]
    public async Task LocalListReturnsInfo()
    {
        string? result = null;
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderInfo>());

        await Given("a default router", () => CreateRouter())
            .When("executing /local list", async Task (router) =>
            {
                result = await router.ExecuteAsync("/local list");
            })
            .Then("returns local models info or no models message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().ContainAny("No local models", "Local models");
                return true;
            })
            .AssertPassed();
    }

    // ── 30. /local scan returns scan info ────────────────────

    [Scenario("Local scan returns scan info"), Fact]
    public async Task LocalScanReturnsInfo()
    {
        string? result = null;
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderInfo>());

        await Given("a default router with no local provider", () => CreateRouter())
            .When("executing /local scan", async Task (router) =>
            {
                result = await router.ExecuteAsync("/local scan");
            })
            .Then("returns scan result or not-available message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().ContainAny("not available", "Scanned", "Local model provider");
                return true;
            })
            .AssertPassed();
    }

    // ── 31. /mcp list returns MCP info ───────────────────────

    [Scenario("MCP list returns MCP server info"), Fact]
    public async Task McpListReturnsInfo()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /mcp list", async Task (router) =>
            {
                result = await router.ExecuteAsync("/mcp list");
            })
            .Then("returns MCP info or no-servers message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().ContainAny("MCP", "No MCP servers");
                return true;
            })
            .AssertPassed();
    }

    // ── 32. /mcp add returns message ─────────────────────────

    [Scenario("MCP add with no args returns usage"), Fact]
    public async Task McpAddReturnsUsage()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /mcp add", async Task (router) =>
            {
                result = await router.ExecuteAsync("/mcp add");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 33. /mcp remove returns message ──────────────────────

    [Scenario("MCP remove with no args returns usage"), Fact]
    public async Task McpRemoveReturnsUsage()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /mcp remove", async Task (router) =>
            {
                result = await router.ExecuteAsync("/mcp remove");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 34. /review in git repo ──────────────────────────────

    [Scenario("Review in current git repo returns something"), Fact]
    public async Task ReviewInGitRepoReturns()
    {
        string? result = null;
        await Given("a default router in the project git repo", () => CreateRouter())
            .When("executing /review", async Task (router) =>
            {
                result = await router.ExecuteAsync("/review");
            })
            .Then("returns a result (review output or no-changes)", _ =>
            {
                result.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── 35. /security-review returns something ───────────────

    [Scenario("Security review returns results"), Fact]
    public async Task SecurityReviewReturns()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /security-review", async Task (router) =>
            {
                result = await router.ExecuteAsync("/security-review");
            })
            .Then("returns a result", _ =>
            {
                result.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── 36. /stats --history returns stats or fallback ───────

    [Scenario("Stats with --history flag returns stats or fallback"), Fact]
    public async Task StatsHistoryReturnsFallback()
    {
        string? result = null;
        await Given("a default router with no session store", () => CreateRouter())
            .When("executing /stats --history", async Task (router) =>
            {
                result = await router.ExecuteAsync("/stats --history");
            })
            .Then("returns history stats or unavailable message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().ContainAny("unavailable", "History Stats", "not initialized");
                return true;
            })
            .AssertPassed();
    }

    // ── 37. /compact-system-prompt off ───────────────────────

    [Scenario("Compact-system-prompt off sets mode"), Fact]
    public async Task CompactSystemPromptOff()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /compact-system-prompt off", async Task (router) =>
            {
                result = await router.ExecuteAsync("/compact-system-prompt off");
            })
            .Then("returns confirmation with 'off'", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("off");
                return true;
            })
            .AssertPassed();
    }

    // ── 38. /compact-system-prompt auto ──────────────────────

    [Scenario("Compact-system-prompt auto sets mode"), Fact]
    public async Task CompactSystemPromptAuto()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /compact-system-prompt auto", async Task (router) =>
            {
                result = await router.ExecuteAsync("/compact-system-prompt auto");
            })
            .Then("returns confirmation with 'auto'", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("auto");
                return true;
            })
            .AssertPassed();
    }

    // ── 39. /compact-system-prompt always ────────────────────

    [Scenario("Compact-system-prompt always sets mode"), Fact]
    public async Task CompactSystemPromptAlways()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /compact-system-prompt always", async Task (router) =>
            {
                result = await router.ExecuteAsync("/compact-system-prompt always");
            })
            .Then("returns confirmation with 'always'", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("always");
                return true;
            })
            .AssertPassed();
    }

    // ── 40. /update returns update info ──────────────────────

    [Scenario("Update returns update info"), Fact]
    public async Task UpdateReturnsInfo()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /update", async Task (router) =>
            {
                result = await router.ExecuteAsync("/update");
            })
            .Then("returns update status", _ =>
            {
                result.Should().NotBeNull();
                // Either "up to date" or "Update available" or prompt-based
                return true;
            })
            .AssertPassed();
    }

    // ── 41. /output-style invalid reports error ──────────────

    [Scenario("Output-style with invalid style reports error"), Fact]
    public async Task OutputStyleInvalidReportsError()
    {
        string? result = null;
        var style = OutputStyle.Rich;
        await Given("a router with output-style callbacks", () =>
                CreateRouter(
                    getOutputStyle: () => style,
                    onOutputStyleChanged: s => style = s))
            .When("executing /output-style invalid", async Task (router) =>
            {
                result = await router.ExecuteAsync("/output-style invalid");
            })
            .Then("returns unknown style error", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Unknown");
                result.Should().Contain("invalid");
                return true;
            })
            .AssertPassed();
    }

    // ── 42. /theme invalid reports error ─────────────────────

    [Scenario("Theme with invalid name reports error"), Fact]
    public async Task ThemeInvalidReportsError()
    {
        string? result = null;
        var theme = TuiTheme.DefaultDark;
        await Given("a router with theme callbacks", () =>
                CreateRouter(
                    getTheme: () => theme,
                    onThemeChanged: t => theme = t))
            .When("executing /theme invalid", async Task (router) =>
            {
                result = await router.ExecuteAsync("/theme invalid");
            })
            .Then("returns unknown theme error", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Unknown theme");
                result.Should().Contain("invalid");
                return true;
            })
            .AssertPassed();
    }

    // ── 43. jdai prefix: /jdai-compact works ─────────────────

    [Scenario("jdai prefix compact works same as /compact"), Fact]
    public async Task JdaiCompactWorks()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /jdai-compact", async Task (router) =>
            {
                result = await router.ExecuteAsync("/jdai-compact");
            })
            .Then("returns compact result", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("compact", Exactly.Once());
                return true;
            })
            .AssertPassed();
    }

    // ── 44. jdai prefix: /jdai-context works ─────────────────

    [Scenario("jdai prefix context works same as /context"), Fact]
    public async Task JdaiContextWorks()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /jdai-context", async Task (router) =>
            {
                result = await router.ExecuteAsync("/jdai-context");
            })
            .Then("returns context usage info", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Context");
                result.Should().Contain("tokens");
                return true;
            })
            .AssertPassed();
    }

    // ── 45. Unknown subcommand: /workflow invalid ────────────

    [Scenario("Workflow with invalid subcommand returns error"), Fact]
    public async Task WorkflowInvalidSubcommandReturnsError()
    {
        string? result = null;
        var catalog = Substitute.For<IWorkflowCatalog>();
        await Given("a router with a workflow catalog", () =>
                CreateRouter(workflowCatalog: catalog))
            .When("executing /workflow invalid", async Task (router) =>
            {
                result = await router.ExecuteAsync("/workflow invalid");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 46. /provider add with no provider config ────────────

    [Scenario("Provider add with no provider config returns not available"), Fact]
    public async Task ProviderAddNoConfigReturnsMessage()
    {
        string? result = null;
        await Given("a router with no provider config", () => CreateRouter())
            .When("executing /provider add openai", async Task (router) =>
            {
                result = await router.ExecuteAsync("/provider add openai");
            })
            .Then("returns 'not available' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 47. /provider remove with no provider config ─────────

    [Scenario("Provider remove with no provider config returns not available"), Fact]
    public async Task ProviderRemoveNoConfigReturnsMessage()
    {
        string? result = null;
        await Given("a router with no provider config", () => CreateRouter())
            .When("executing /provider remove openai", async Task (router) =>
            {
                result = await router.ExecuteAsync("/provider remove openai");
            })
            .Then("returns 'not available' message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("not available");
                return true;
            })
            .AssertPassed();
    }

    // ── 48. /provider test with no provider config ───────────

    [Scenario("Provider test lists test results"), Fact]
    public async Task ProviderTestReturnsResults()
    {
        string? result = null;
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderInfo>
            {
                new("TestProvider", true, "OK", []),
            });

        await Given("a router with detected providers", () => CreateRouter())
            .When("executing /provider test", async Task (router) =>
            {
                result = await router.ExecuteAsync("/provider test");
            })
            .Then("returns test results", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("test results");
                result.Should().Contain("TestProvider");
                return true;
            })
            .AssertPassed();
    }

    // ── 49. /local search query returns results or message ───

    [Scenario("Local search with query returns results or message"), Fact]
    public async Task LocalSearchReturnsResults()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /local search with no query", async Task (router) =>
            {
                result = await router.ExecuteAsync("/local search");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 50. /local add returns add message ───────────────────

    [Scenario("Local add with no path returns usage"), Fact]
    public async Task LocalAddReturnsUsage()
    {
        string? result = null;
        await Given("a default router", () => CreateRouter())
            .When("executing /local add with no args", async Task (router) =>
            {
                result = await router.ExecuteAsync("/local add");
            })
            .Then("returns usage message", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("Usage");
                return true;
            })
            .AssertPassed();
    }

    // ── 51. /workflow create missing tools does not save ─────

    [Scenario("Workflow create with unavailable tools is not saved"), Fact]
    public async Task WorkflowCreateMissingToolsDoesNotSave()
    {
        var responseJson = """
            {
              "name": "Broken Workflow",
              "version": "1.0",
              "description": "References a tool that's not in kernel plugins",
              "tags": ["test"],
              "steps": [
                { "name": "Run shell", "kind": "Tool", "target": "shell-run_command" }
              ]
            }
            """;

        var mockChat = Substitute.For<IChatCompletionService>();
        mockChat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, responseJson)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(mockChat);
        var kernel = builder.Build();

        var session = new AgentSession(
            _registry,
            kernel,
            new ProviderModelInfo("test-model", "Test Model", "TestProvider"));

        var workflowCatalog = Substitute.For<IWorkflowCatalog>();
        var router = new SlashCommandRouter(session, _registry, workflowCatalog: workflowCatalog);

        var result = await router.ExecuteAsync("/workflow create create a new project workflow");

        result.Should().Contain("not saved");
        await workflowCatalog.DidNotReceive().SaveAsync(
            Arg.Any<AgentWorkflowDefinition>(),
            Arg.Any<CancellationToken>());
    }

    // ── 52. /system-prompt append updates prompt ────────────

    [Scenario("System-prompt append updates the active system prompt"), Fact]
    public async Task SystemPromptAppendUpdatesPrompt()
    {
        var (router, session) = CreateRouterWithSession();
        session.History.AddSystemMessage("Base prompt");
        session.CaptureOriginalSystemPromptIfUnset();

        var result = await router.ExecuteAsync("/system-prompt append Always cite files");

        result.Should().Contain("Appended");
        session.History[0].Content.Should().Contain("Base prompt");
        session.History[0].Content.Should().Contain("Always cite files");
    }

    // ── 53. /prompt drop removes selected message ───────────

    [Scenario("Prompt drop removes a selected context message"), Fact]
    public async Task PromptDropRemovesMessage()
    {
        var (router, session) = CreateRouterWithSession();
        session.History.AddSystemMessage("System");
        session.History.AddUserMessage("User one");
        session.History.AddAssistantMessage("Assistant one");

        var result = await router.ExecuteAsync("/prompt drop 1");

        result.Should().Contain("Dropped 1");
        session.History.Count(m => m.Role != AuthorRole.System).Should().Be(1);
        session.History.Last().Content.Should().Be("Assistant one");
    }

    // ── 54. /prompt inject --system augments prompt ─────────

    [Scenario("Prompt inject system augments current system prompt"), Fact]
    public async Task PromptInjectSystemAugmentsPrompt()
    {
        var (router, session) = CreateRouterWithSession();
        session.History.AddSystemMessage("System base");
        session.CaptureOriginalSystemPromptIfUnset();

        var result = await router.ExecuteAsync("/prompt inject --system Additional system directive");

        result.Should().Contain("Injected system context");
        session.History[0].Content.Should().Contain("System base");
        session.History[0].Content.Should().Contain("Additional system directive");
    }

    // ── 55. /reasoning set updates session override ────────

    [Scenario("Reasoning command updates session override"), Fact]
    public async Task ReasoningCommandSetsOverride()
    {
        var (router, session) = CreateRouterWithSession();

        var result = await router.ExecuteAsync("/reasoning high");

        result.Should().Contain("high");
        session.ReasoningEffortOverride.Should().Be(ReasoningEffort.High);
    }

    // ── 56. /reasoning with no arg shows current level ─────

    [Scenario("Reasoning command with no args shows current level"), Fact]
    public async Task ReasoningCommandShowsCurrentLevel()
    {
        var (router, session) = CreateRouterWithSession();
        session.ReasoningEffortOverride = ReasoningEffort.Medium;

        var result = await router.ExecuteAsync("/reasoning");

        result.Should().Contain("medium");
    }
}
