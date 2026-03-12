using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Startup;
using Microsoft.SemanticKernel;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Provider Orchestrator")]
[Collection("DataDirectories")]
public sealed class ProviderOrchestratorBddTests : TinyBddXunitBase
{
    public ProviderOrchestratorBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Startup uses saved defaults without probing providers"), Fact]
    public async Task DetectAndSelectAsync_UsesSavedDefaultFastPath()
    {
        ProviderSetup? setup = null;
        StubDetector? preferredDetector = null;
        StubDetector? secondaryDetector = null;

        await WithTempProjectAsync(async (projectDir, configStore) =>
        {
            await Given("saved provider and model defaults for this project", async Task<CliOptions> () =>
                {
                    await configStore.SetDefaultProviderAsync("Preferred", projectDir).ConfigureAwait(false);
                    await configStore.SetDefaultModelAsync("preferred-model", projectDir).ConfigureAwait(false);

                    preferredDetector = new StubDetector(
                        "Preferred",
                        new ProviderInfo(
                            "Preferred",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models:
                            [
                                new ProviderModelInfo("preferred-model", "Preferred Model", "Preferred"),
                                new ProviderModelInfo("preferred-fallback", "Preferred Fallback", "Preferred"),
                            ]));

                    secondaryDetector = new StubDetector(
                        "Secondary",
                        new ProviderInfo(
                            "Secondary",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("secondary-model", "Secondary Model", "Secondary")]));

                    ProviderOrchestrator.RegistryFactory = () => BuildRegistry(preferredDetector, secondaryDetector);
                    return new CliOptions { PrintMode = true };
                })
                .When("provider detection runs at startup", async Task (opts) =>
                {
                    setup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
                })
                .Then("the saved provider/model are reused without probing providers", _ =>
                {
                    setup.Should().NotBeNull();
                    setup!.SelectedModel.ProviderName.Should().Be("Preferred");
                    setup.SelectedModel.Id.Should().Be("preferred-model");

                    preferredDetector!.DetectCount.Should().Be(0);
                    secondaryDetector!.DetectCount.Should().Be(0);
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("Without defaults startup refreshes all providers and selects the first available model in print mode"), Fact]
    public async Task DetectAndSelectAsync_WithoutDefaults_RefreshesAllProviders()
    {
        ProviderSetup? setup = null;
        StubDetector? firstDetector = null;
        StubDetector? secondDetector = null;

        await WithTempProjectAsync(async (_, configStore) =>
        {
            await Given("a registry with two available providers", () =>
                {
                    firstDetector = new StubDetector(
                        "First",
                        new ProviderInfo(
                            "First",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("first-model", "First Model", "First")]));

                    secondDetector = new StubDetector(
                        "Second",
                        new ProviderInfo(
                            "Second",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("second-model", "Second Model", "Second")]));

                    ProviderOrchestrator.RegistryFactory = () => BuildRegistry(firstDetector, secondDetector);
                    return new CliOptions { PrintMode = true };
                })
                .When("startup selection runs", async Task (opts) =>
                {
                    setup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
                })
                .Then("all providers are probed and a model is selected", _ =>
                {
                    setup.Should().NotBeNull();
                    setup!.SelectedModel.Id.Should().Be("first-model");
                    // Full refresh currently probes providers once for status and once for model list.
                    firstDetector!.DetectCount.Should().Be(2);
                    secondDetector!.DetectCount.Should().Be(2);
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("CLI model/provider filters select the matching provider model"), Fact]
    public async Task DetectAndSelectAsync_UsesCliFilters()
    {
        ProviderSetup? setup = null;

        await WithTempProjectAsync(async (_, configStore) =>
        {
            await Given("multiple providers with distinct model IDs", () =>
                {
                    var codex = new StubDetector(
                        "OpenAI Codex",
                        new ProviderInfo(
                            "OpenAI Codex",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "OpenAI Codex")]));

                    var local = new StubDetector(
                        "Local",
                        new ProviderInfo(
                            "Local",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("llama3.2", "Llama 3.2", "Local")]));

                    ProviderOrchestrator.RegistryFactory = () => BuildRegistry(codex, local);

                    return new CliOptions
                    {
                        PrintMode = true,
                        CliProvider = "codex",
                        CliModel = "5.3",
                    };
                })
                .When("selection runs with filters", async Task (opts) =>
                {
                    setup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
                })
                .Then("the matching codex model is selected", _ =>
                {
                    setup.Should().NotBeNull();
                    setup!.SelectedModel.ProviderName.Should().Be("OpenAI Codex");
                    setup.SelectedModel.Id.Should().Be("gpt-5.3-codex");
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("Unknown model filter returns null to force explicit correction"), Fact]
    public async Task DetectAndSelectAsync_UnknownModel_ReturnsNull()
    {
        ProviderSetup? setup = new(
            new ProviderRegistry([]),
            new ProviderConfigurationManager(new EncryptedFileStore()),
            new ModelMetadataProvider(),
            [],
            new ProviderModelInfo("placeholder", "Placeholder", "None"),
            [],
            Kernel.CreateBuilder().Build());

        await WithTempProjectAsync(async (_, configStore) =>
        {
            await Given("a registry with one available model", () =>
                {
                    var detector = new StubDetector(
                        "OpenAI Codex",
                        new ProviderInfo(
                            "OpenAI Codex",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models: [new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "OpenAI Codex")]));
                    ProviderOrchestrator.RegistryFactory = () => BuildRegistry(detector);
                    return new CliOptions
                    {
                        PrintMode = true,
                        CliModel = "does-not-exist",
                    };
                })
                .When("selection runs with a non-matching model filter", async Task (opts) =>
                {
                    setup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
                })
                .Then("selection fails and returns null", _ => setup is null)
                .AssertPassed();
        });
    }

    [Scenario("When no defaults exist selection prefers capability-compatible models"), Fact]
    public async Task DetectAndSelectAsync_PrefersToolCapableModels()
    {
        ProviderSetup? setup = null;

        await WithTempProjectAsync(async (_, configStore) =>
        {
            await Given("a provider list where only one model supports tool calling", () =>
                {
                    var basic = new StubDetector(
                        "Basic",
                        new ProviderInfo(
                            "Basic",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models:
                            [
                                new ProviderModelInfo(
                                    "chat-only",
                                    "Chat Only",
                                    "Basic",
                                    ContextWindowTokens: 16_384,
                                    Capabilities: ModelCapabilities.Chat),
                            ]));

                    var advanced = new StubDetector(
                        "Advanced",
                        new ProviderInfo(
                            "Advanced",
                            IsAvailable: true,
                            StatusMessage: "ready",
                            Models:
                            [
                                new ProviderModelInfo(
                                    "tool-capable",
                                    "Tool Capable",
                                    "Advanced",
                                    ContextWindowTokens: 128_000,
                                    Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling),
                            ]));

                    ProviderOrchestrator.RegistryFactory = () => BuildRegistry(basic, advanced);
                    return new CliOptions { PrintMode = true };
                })
                .When("startup selection runs without explicit provider/model", async Task (opts) =>
                {
                    setup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
                })
                .Then("the tool-capable model is selected", _ =>
                {
                    setup.Should().NotBeNull();
                    setup!.SelectedModel.ProviderName.Should().Be("Advanced");
                    setup.SelectedModel.Id.Should().Be("tool-capable");
                    return true;
                })
                .AssertPassed();
        });
    }

    private static async Task WithTempProjectAsync(Func<string, AtomicConfigStore, Task> action)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"jdai-provider-orch-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tempRoot, "project");
        var configPath = Path.Combine(tempRoot, "config.json");
        var originalCwd = Directory.GetCurrentDirectory();
        var previousRegistryFactory = ProviderOrchestrator.RegistryFactory;
        var previousRouterFactory = ProviderOrchestrator.RouterFactory;

        Directory.CreateDirectory(projectDir);
        using var configStore = new AtomicConfigStore(configPath);

        Directory.SetCurrentDirectory(projectDir);
        try
        {
            await action(projectDir, configStore).ConfigureAwait(false);
        }
        finally
        {
            ProviderOrchestrator.RegistryFactory = previousRegistryFactory;
            ProviderOrchestrator.RouterFactory = previousRouterFactory;
            Directory.SetCurrentDirectory(originalCwd);
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static (ProviderRegistry Registry, ProviderConfigurationManager ProviderConfig, ModelMetadataProvider MetadataProvider)
        BuildRegistry(params StubDetector[] detectors)
    {
        var providerConfig = new ProviderConfigurationManager(new EncryptedFileStore());
        var metadataProvider = new ModelMetadataProvider();
        var registry = new ProviderRegistry(detectors, metadataProvider);
        return (registry, providerConfig, metadataProvider);
    }

    private sealed class StubDetector : IProviderDetector
    {
        private readonly ProviderInfo _info;

        public StubDetector(string providerName, ProviderInfo info)
        {
            ProviderName = providerName;
            _info = info;
        }

        public string ProviderName { get; }

        public int DetectCount { get; private set; }

        public Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
        {
            DetectCount++;
            return Task.FromResult(_info);
        }

        public Kernel BuildKernel(ProviderModelInfo model)
        {
            return Kernel.CreateBuilder().Build();
        }
    }
}
