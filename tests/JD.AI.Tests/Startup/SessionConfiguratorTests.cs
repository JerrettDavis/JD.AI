using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

[Collection("DataDirectories")]
public sealed class SessionConfiguratorTests
{
    [Fact]
    public async Task ConfigureAsync_AppliesCliFlagsAndExplicitFallbackModels()
    {
        var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
        var (registry, providerConfig, metadataProvider) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", primaryModel));
        var providerSetup = new ProviderSetup(
            registry,
            providerConfig,
            metadataProvider,
            [primaryModel],
            primaryModel,
            ["route-fallback-1"],
            registry.BuildKernel(primaryModel));

        var options = new CliOptions
        {
            SkipPermissions = true,
            VerboseMode = true,
            PermissionModeStr = "plan",
            FallbackModels = ["fallback-a", "fallback-b"],
            NoSessionPersistence = true,
            MaxBudgetUsd = 12.34m,
            PrintMode = true,
            DebugMode = true,
            DebugCategories = "agent,tools",
        };

        var setup = await SessionConfigurator.ConfigureAsync(options, providerSetup);

        Assert.Equal(primaryModel, setup.SelectedModel);
        Assert.Equal(registry.BuildKernel(primaryModel).Plugins.Count, setup.Kernel.Plugins.Count);
        Assert.True(setup.Session.SkipPermissions);
        Assert.True(setup.Session.Verbose);
        Assert.Equal(PermissionMode.Plan, setup.Session.PermissionMode);
        Assert.Equal(["fallback-a", "fallback-b"], setup.Session.FallbackModels);
        Assert.True(setup.Session.NoSessionPersistence);
        Assert.Equal(12.34m, setup.Session.MaxBudgetUsd);
    }

    [Fact]
    public async Task ConfigureAsync_UsesRoutedFallbackModelsWhenCliFallbackNotProvided()
    {
        var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
        var (registry, providerConfig, metadataProvider) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", primaryModel));
        var providerSetup = new ProviderSetup(
            registry,
            providerConfig,
            metadataProvider,
            [primaryModel],
            primaryModel,
            ["route-fallback-1", "route-fallback-2"],
            registry.BuildKernel(primaryModel));

        var setup = await SessionConfigurator.ConfigureAsync(
            new CliOptions { NoSessionPersistence = true, PrintMode = true },
            providerSetup);

        Assert.Equal(["route-fallback-1", "route-fallback-2"], setup.Session.FallbackModels);
        Assert.Equal(Directory.GetCurrentDirectory(), setup.ProjectPath);
        Assert.Null(setup.WorktreeManager);
    }
}
