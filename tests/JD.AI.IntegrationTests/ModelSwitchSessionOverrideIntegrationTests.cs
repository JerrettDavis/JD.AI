using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Xunit;

namespace JD.AI.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ModelSwitchSessionOverrideIntegrationTests
{
    [Fact]
    public async Task ModelSet_OverridesSessionWithoutPersistingDefaults()
    {
        var initialModel = new ProviderModelInfo("base-model", "Base Model", "ProviderA");
        var newModel = new ProviderModelInfo("runtime-model", "Runtime Model", "ProviderB");

        var registry = new FakeRegistry(initialModel, newModel);
        var session = new AgentSession(registry, BuildKernel(), initialModel);

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-model-override-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDirectory);
            var configPath = Path.Combine(tempDirectory, "config.json");
            using var configStore = new AtomicConfigStore(configPath);

            await configStore.SetDefaultProviderAsync("ProviderA", tempDirectory, CancellationToken.None);
            await configStore.SetDefaultModelAsync("base-model", tempDirectory, CancellationToken.None);

            var router = new SlashCommandRouter(session, registry, configStore: configStore);
            var result = await router.ExecuteAsync("/model set runtime-model");

            Assert.NotNull(result);
            Assert.Contains("Switched session model", result);
            Assert.Equal("runtime-model", session.CurrentModel?.Id);
            Assert.Equal("ProviderB", session.CurrentModel?.ProviderName);

            var config = await configStore.ReadAsync();
            Assert.True(config.ProjectDefaults.TryGetValue(tempDirectory, out var defaults));
            Assert.Equal("ProviderA", defaults!.Provider);
            Assert.Equal("base-model", defaults.Model);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static Kernel BuildKernel() => Kernel.CreateBuilder().Build();

    private sealed class FakeRegistry(params ProviderModelInfo[] models) : IProviderRegistry
    {
        private readonly List<ProviderModelInfo> _models = [.. models];

        public Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProviderInfo>>([]);

        public Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(bool forceRefresh, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProviderInfo>>([]);

        public Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProviderModelInfo>>(_models);

        public Kernel BuildKernel(ProviderModelInfo model) => Kernel.CreateBuilder().Build();

        public IProviderDetector? GetDetector(string providerName) => null;
    }
}
