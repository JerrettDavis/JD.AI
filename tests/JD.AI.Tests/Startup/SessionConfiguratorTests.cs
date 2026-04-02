using JD.AI.Core.Agents;
using JD.AI.Core.Config;
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
        Assert.Same(providerSetup.Kernel, setup.Kernel);
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

    [Theory]
    [InlineData("acceptedits", PermissionMode.AcceptEdits)]
    [InlineData("dontask", PermissionMode.BypassAll)]
    [InlineData("normal", PermissionMode.Normal)]
    [InlineData("unexpected", PermissionMode.Normal)]
    public async Task ConfigureAsync_MapsPermissionModes(string permissionMode, PermissionMode expected)
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
            [],
            registry.BuildKernel(primaryModel));

        var setup = await SessionConfigurator.ConfigureAsync(
            new CliOptions
            {
                PermissionModeStr = permissionMode,
                NoSessionPersistence = true,
                PrintMode = true,
            },
            providerSetup);

        Assert.Equal(expected, setup.Session.PermissionMode);
    }

    [Fact]
    public async Task ConfigureAsync_WhenStartingNewSession_InitializesPersistence()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        var currentDirectory = Directory.GetCurrentDirectory();

        try
        {
            DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

            var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
            var (registry, providerConfig, metadataProvider) = StartupTestProviderFactory.CreateRegistry(
                StartupTestProviderFactory.AvailableProvider("TestProvider", primaryModel));
            var providerSetup = new ProviderSetup(
                registry,
                providerConfig,
                metadataProvider,
                [primaryModel],
                primaryModel,
                [],
                registry.BuildKernel(primaryModel));

            var setup = await SessionConfigurator.ConfigureAsync(
                new CliOptions
                {
                    IsNewSession = true,
                    PrintMode = true,
                },
                providerSetup);

            Assert.Equal(currentDirectory, setup.ProjectPath);
            Assert.NotNull(setup.Session.Store);
            Assert.NotNull(setup.Session.SessionInfo);
            Assert.Equal(currentDirectory, setup.Session.SessionInfo!.ProjectPath);
        }
        finally
        {
            DataDirectories.Reset();
        }
    }
}
