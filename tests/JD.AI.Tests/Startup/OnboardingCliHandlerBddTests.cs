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

[Feature("Onboarding CLI")]
[Collection("DataDirectories")]
public sealed class OnboardingCliHandlerBddTests : TinyBddXunitBase
{
    private static readonly string[] ProjectDefaultsArgs =
        ["--provider", "OpenAI Codex", "--model", "gpt-5.3-codex"];

    private static readonly string[] GlobalDefaultsArgs =
        ["--provider", "OpenAI Codex", "--model", "gpt-5.2-codex", "--global"];

    private static readonly string[] UnknownProviderArgs =
        ["--provider", "Unknown Provider", "--model", "gpt-5.3-codex"];

    private static readonly string[] UnknownModelArgs =
        ["--provider", "OpenAI Codex", "--model", "non-existent-model"];

    public OnboardingCliHandlerBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Onboarding saves project defaults when provider/model are supplied"), Fact]
    public async Task RunAsync_SavesProjectDefaults()
    {
        int exitCode = -1;
        string? projectDefaultProvider = null;
        string? projectDefaultModel = null;

        await WithTempProjectAsync(async (projectDir, configPath) =>
        {
            await Given("an available provider and model from discovery", () =>
                {
                    ConfigureFactoryWithProviders(new ProviderInfo(
                        "OpenAI Codex",
                        IsAvailable: true,
                        StatusMessage: "ready",
                        Models: [new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "OpenAI Codex")]));

                    OnboardingCliHandler.ConfigStoreFactory = () => new AtomicConfigStore(configPath);

                    return ProjectDefaultsArgs;
                })
                .When("running onboarding", async Task (args) =>
                {
                    exitCode = await OnboardingCliHandler.RunAsync(args).ConfigureAwait(false);
                    using var store = new AtomicConfigStore(configPath);
                    projectDefaultProvider = await store.GetDefaultProviderAsync(projectDir).ConfigureAwait(false);
                    projectDefaultModel = await store.GetDefaultModelAsync(projectDir).ConfigureAwait(false);
                })
                .Then("project defaults are persisted", _ =>
                {
                    exitCode.Should().Be(0);
                    projectDefaultProvider.Should().Be("OpenAI Codex");
                    projectDefaultModel.Should().Be("gpt-5.3-codex");
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("Onboarding --global persists both global and project defaults"), Fact]
    public async Task RunAsync_GlobalFlag_SavesGlobalDefaults()
    {
        int exitCode = -1;
        string? projectDefaultProvider = null;
        string? projectDefaultModel = null;
        string? globalDefaultProvider = null;
        string? globalDefaultModel = null;

        await WithTempProjectAsync(async (projectDir, configPath) =>
        {
            await Given("an available provider and model", () =>
                {
                    ConfigureFactoryWithProviders(new ProviderInfo(
                        "OpenAI Codex",
                        IsAvailable: true,
                        StatusMessage: "ready",
                        Models: [new ProviderModelInfo("gpt-5.2-codex", "gpt-5.2-codex", "OpenAI Codex")]));

                    OnboardingCliHandler.ConfigStoreFactory = () => new AtomicConfigStore(configPath);

                    return GlobalDefaultsArgs;
                })
                .When("running onboarding in global mode", async Task (args) =>
                {
                    exitCode = await OnboardingCliHandler.RunAsync(args).ConfigureAwait(false);

                    using var store = new AtomicConfigStore(configPath);
                    projectDefaultProvider = await store.GetDefaultProviderAsync(projectDir).ConfigureAwait(false);
                    projectDefaultModel = await store.GetDefaultModelAsync(projectDir).ConfigureAwait(false);
                    globalDefaultProvider = await store.GetDefaultProviderAsync().ConfigureAwait(false);
                    globalDefaultModel = await store.GetDefaultModelAsync().ConfigureAwait(false);
                })
                .Then("project and global defaults are both saved", _ =>
                {
                    exitCode.Should().Be(0);
                    projectDefaultProvider.Should().Be("OpenAI Codex");
                    projectDefaultModel.Should().Be("gpt-5.2-codex");
                    globalDefaultProvider.Should().Be("OpenAI Codex");
                    globalDefaultModel.Should().Be("gpt-5.2-codex");
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("Unknown provider returns an error and does not write defaults"), Fact]
    public async Task RunAsync_UnknownProvider_ReturnsOne()
    {
        int exitCode = -1;
        string? defaultProvider = "placeholder";

        await WithTempProjectAsync(async (_, configPath) =>
        {
            await Given("onboarding discovery with one known provider", () =>
                {
                    ConfigureFactoryWithProviders(new ProviderInfo(
                        "OpenAI Codex",
                        IsAvailable: true,
                        StatusMessage: "ready",
                        Models: [new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "OpenAI Codex")]));

                    OnboardingCliHandler.ConfigStoreFactory = () => new AtomicConfigStore(configPath);
                    return UnknownProviderArgs;
                })
                .When("running onboarding with a non-existent provider", async Task (args) =>
                {
                    exitCode = await OnboardingCliHandler.RunAsync(args).ConfigureAwait(false);
                    using var store = new AtomicConfigStore(configPath);
                    defaultProvider = await store.GetDefaultProviderAsync().ConfigureAwait(false);
                })
                .Then("it returns failure and leaves defaults unset", _ =>
                {
                    exitCode.Should().Be(1);
                    defaultProvider.Should().BeNull();
                    return true;
                })
                .AssertPassed();
        });
    }

    [Scenario("Unknown model for a known provider returns an error"), Fact]
    public async Task RunAsync_UnknownModel_ReturnsOne()
    {
        int exitCode = -1;

        await WithTempProjectAsync(async (_, configPath) =>
        {
            await Given("onboarding discovery with one provider/model", () =>
                {
                    ConfigureFactoryWithProviders(new ProviderInfo(
                        "OpenAI Codex",
                        IsAvailable: true,
                        StatusMessage: "ready",
                        Models: [new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", "OpenAI Codex")]));

                    OnboardingCliHandler.ConfigStoreFactory = () => new AtomicConfigStore(configPath);
                    return UnknownModelArgs;
                })
                .When("running onboarding with an unknown model", async Task (args) =>
                {
                    exitCode = await OnboardingCliHandler.RunAsync(args).ConfigureAwait(false);
                })
                .Then("it fails fast", _ => exitCode == 1)
                .AssertPassed();
        });
    }

    private static async Task WithTempProjectAsync(Func<string, string, Task> action)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"jdai-onboard-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tempRoot, "project");
        var configPath = Path.Combine(tempRoot, "config.json");
        var originalCwd = Directory.GetCurrentDirectory();

        Directory.CreateDirectory(projectDir);

        Directory.SetCurrentDirectory(projectDir);
        try
        {
            await action(projectDir, configPath).ConfigureAwait(false);
        }
        finally
        {
            OnboardingCliHandler.ResetFactoriesForTests();
            Directory.SetCurrentDirectory(originalCwd);
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static void ConfigureFactoryWithProviders(params ProviderInfo[] providers)
    {
        var detectors = providers.Select(p => new StubDetector(p.Name, p)).ToArray();
        OnboardingCliHandler.RegistryFactory = () =>
        {
            var providerConfig = new ProviderConfigurationManager(new EncryptedFileStore());
            var metadataProvider = new ModelMetadataProvider();
            var registry = new ProviderRegistry(detectors, metadataProvider);
            return (registry, providerConfig, metadataProvider);
        };
    }

    private sealed class StubDetector : IProviderDetector
    {
        private readonly ProviderInfo _provider;

        public StubDetector(string providerName, ProviderInfo provider)
        {
            ProviderName = providerName;
            _provider = provider;
        }

        public string ProviderName { get; }

        public Task<ProviderInfo> DetectAsync(CancellationToken ct = default) => Task.FromResult(_provider);

        public Kernel BuildKernel(ProviderModelInfo model) => Kernel.CreateBuilder().Build();
    }

}
