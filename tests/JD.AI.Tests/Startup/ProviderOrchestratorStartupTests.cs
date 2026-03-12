using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Startup;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Startup;

[Collection("DataDirectories")]
public sealed class ProviderOrchestratorStartupTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();
    private readonly Func<(ProviderRegistry Registry, ProviderConfigurationManager ProviderConfig, ModelMetadataProvider
        MetadataProvider)>
        _originalRegistryFactory = ProviderOrchestrator.RegistryFactory;

    [Fact]
    public async Task DetectAndSelectAsync_UsesPreferredProviderFastPath()
    {
        var projectPath = _fixture.CreateSubdirectory("project-fast");
        Directory.SetCurrentDirectory(projectPath);

        using var configStore = new AtomicConfigStore(_fixture.GetPath("config-fast.json"));
        await configStore.SetDefaultProviderAsync("Preferred", projectPath);
        await configStore.SetDefaultModelAsync("preferred-model", projectPath);

        var preferred = new FakeDetector(
            "Preferred",
            new ProviderInfo(
                "Preferred",
                IsAvailable: true,
                StatusMessage: "ready",
                Models: [new ProviderModelInfo("preferred-model", "Preferred Model", "Preferred")]));

        var secondary = new FakeDetector(
            "Secondary",
            new ProviderInfo(
                "Secondary",
                IsAvailable: true,
                StatusMessage: "ready",
                Models: [new ProviderModelInfo("secondary-model", "Secondary Model", "Secondary")]));

        ConfigureRegistryFactory(preferred, secondary);

        var setup = await ProviderOrchestrator
            .DetectAndSelectAsync(new CliOptions { PrintMode = true }, configStore);

        Assert.NotNull(setup);
        Assert.Equal("preferred-model", setup!.SelectedModel.Id);
        Assert.Single(setup.AllModels);
        Assert.Equal("Preferred", setup.SelectedModel.ProviderName);
        Assert.Equal(0, preferred.DetectCount);
        Assert.Equal(0, secondary.DetectCount);
    }

    [Fact]
    public async Task DetectAndSelectAsync_FallsBackToFullRefreshWhenPreferredUnavailable()
    {
        var projectPath = _fixture.CreateSubdirectory("project-fallback");
        Directory.SetCurrentDirectory(projectPath);

        using var configStore = new AtomicConfigStore(_fixture.GetPath("config-fallback.json"));
        await configStore.SetDefaultProviderAsync("Preferred", projectPath);

        var preferred = new FakeDetector(
            "Preferred",
            new ProviderInfo(
                "Preferred",
                IsAvailable: false,
                StatusMessage: "auth missing",
                Models: []));

        var secondary = new FakeDetector(
            "Secondary",
            new ProviderInfo(
                "Secondary",
                IsAvailable: true,
                StatusMessage: "ready",
                Models: [new ProviderModelInfo("secondary-model", "Secondary Model", "Secondary")]));

        ConfigureRegistryFactory(preferred, secondary);

        var setup = await ProviderOrchestrator
            .DetectAndSelectAsync(new CliOptions { PrintMode = true }, configStore);

        Assert.NotNull(setup);
        Assert.Equal("secondary-model", setup!.SelectedModel.Id);
        Assert.Equal(3, preferred.DetectCount);
        Assert.Equal(2, secondary.DetectCount);
    }

    [Fact]
    public async Task DetectAndSelectAsync_ColdStartPersistsProjectDefaults()
    {
        var projectPath = _fixture.CreateSubdirectory("project-cold");
        Directory.SetCurrentDirectory(projectPath);

        using var configStore = new AtomicConfigStore(_fixture.GetPath("config-cold.json"));

        var primary = new FakeDetector(
            "Primary",
            new ProviderInfo(
                "Primary",
                IsAvailable: true,
                StatusMessage: "ready",
                Models: [new ProviderModelInfo("primary-model", "Primary Model", "Primary")]));

        ConfigureRegistryFactory(primary);

        var setup = await ProviderOrchestrator
            .DetectAndSelectAsync(new CliOptions { PrintMode = true }, configStore);

        Assert.NotNull(setup);
        Assert.Equal("primary-model", setup!.SelectedModel.Id);
        Assert.Equal(2, primary.DetectCount);

        var persistedProvider = await configStore.GetDefaultProviderAsync(projectPath);
        var persistedModel = await configStore.GetDefaultModelAsync(projectPath);

        Assert.Equal("Primary", persistedProvider);
        Assert.Equal("primary-model", persistedModel);
    }

    public void Dispose()
    {
        ProviderOrchestrator.RegistryFactory = _originalRegistryFactory;
        Directory.SetCurrentDirectory(_originalCurrentDirectory);
        _fixture.Dispose();
    }

    private static void ConfigureRegistryFactory(params FakeDetector[] detectors)
    {
        ProviderOrchestrator.RegistryFactory = () =>
        {
            var metadataProvider = new ModelMetadataProvider();
            var registry = new ProviderRegistry(detectors, metadataProvider);
            var providerConfig = new ProviderConfigurationManager(new StubCredentialStore());
            return (registry, providerConfig, metadataProvider);
        };
    }

    private sealed class FakeDetector(string providerName, ProviderInfo result) : IProviderDetector
    {
        public string ProviderName => providerName;

        public int DetectCount { get; private set; }

        public Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
        {
            DetectCount++;
            return Task.FromResult(result);
        }

        public Kernel BuildKernel(ProviderModelInfo model) => Kernel.CreateBuilder().Build();
    }

    private sealed class StubCredentialStore : ICredentialStore
    {
        public bool IsAvailable => true;

        public string StoreName => "Stub";

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
