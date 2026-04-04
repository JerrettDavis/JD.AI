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
        SessionSetup? setup = null;

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

            setup = await SessionConfigurator.ConfigureAsync(
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
            setup?.Session.Store?.Dispose();
            DataDirectories.Reset();
        }
    }

    [Fact]
    public async Task ConfigureAsync_WhenContinuingSession_ResumesMostRecentSessionAndRestoresModel()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        var currentDirectory = Directory.GetCurrentDirectory();
        AgentSession? olderSession = null;
        AgentSession? latestSession = null;
        SessionSetup? setup = null;

        try
        {
            DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

            var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
            var secondaryModel = new ProviderModelInfo("secondary-model", "Secondary Model", "TestProvider");
            var providerSetup = CreateProviderSetup(primaryModel, secondaryModel);

            olderSession = await CreatePersistedSessionAsync(
                providerSetup,
                currentDirectory,
                "older-session",
                assistantReply: "older assistant");

            latestSession = await CreatePersistedSessionAsync(
                providerSetup,
                currentDirectory,
                "latest-session",
                switchToModel: secondaryModel,
                assistantReply: "latest assistant");
            latestSession.SessionInfo!.UpdatedAt = DateTime.UtcNow.AddMinutes(1);
            await latestSession.Store!.UpdateSessionAsync(latestSession.SessionInfo);

            setup = await SessionConfigurator.ConfigureAsync(
                new CliOptions
                {
                    ContinueSession = true,
                    PrintMode = true,
                },
                providerSetup);

            Assert.Equal(latestSession.SessionInfo!.Id, setup.Session.SessionInfo!.Id);
            Assert.Equal(2, setup.Session.SessionInfo.Turns.Count);
            Assert.Equal("latest assistant", setup.Session.SessionInfo.Turns[^1].Content);
            Assert.Equal(secondaryModel, setup.SelectedModel);
            Assert.Equal(secondaryModel, setup.Session.CurrentModel);
            Assert.NotSame(providerSetup.Kernel, setup.Kernel);
            Assert.Same(setup.Kernel, setup.Session.Kernel);
            var sessions = await setup.Session.Store!.ListSessionsAsync(setup.Session.SessionInfo.ProjectHash, 10);
            Assert.Equal(2, sessions.Count);
        }
        finally
        {
            setup?.Session.Store?.Dispose();
            olderSession?.Store?.Dispose();
            latestSession?.Store?.Dispose();
            DataDirectories.Reset();
        }
    }

    [Fact]
    public async Task ConfigureAsync_WhenCliSessionIdProvided_PrefersItOverResumeId()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        var currentDirectory = Directory.GetCurrentDirectory();
        AgentSession? requestedSession = null;
        AgentSession? ignoredSession = null;
        SessionSetup? setup = null;

        try
        {
            DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

            var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
            var providerSetup = CreateProviderSetup(primaryModel);

            requestedSession = await CreatePersistedSessionAsync(
                providerSetup,
                currentDirectory,
                "requested-session",
                assistantReply: "requested assistant");

            ignoredSession = await CreatePersistedSessionAsync(
                providerSetup,
                currentDirectory,
                "ignored-session",
                assistantReply: "ignored assistant");

            setup = await SessionConfigurator.ConfigureAsync(
                new CliOptions
                {
                    ResumeId = ignoredSession.SessionInfo!.Id,
                    CliSessionId = requestedSession.SessionInfo!.Id,
                    PrintMode = true,
                },
                providerSetup);

            Assert.Equal(requestedSession.SessionInfo!.Id, setup.Session.SessionInfo!.Id);
            Assert.Equal("requested assistant", setup.Session.SessionInfo.Turns[^1].Content);
        }
        finally
        {
            setup?.Session.Store?.Dispose();
            requestedSession?.Store?.Dispose();
            ignoredSession?.Store?.Dispose();
            DataDirectories.Reset();
        }
    }

    [Fact]
    public async Task ConfigureAsync_WhenCliSessionIdIsMissing_Throws()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();

        try
        {
            DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

            var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
            var providerSetup = CreateProviderSetup(primaryModel);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SessionConfigurator.ConfigureAsync(
                    new CliOptions
                    {
                        CliSessionId = "missing-session",
                        PrintMode = true,
                    },
                    providerSetup));

            Assert.Contains("missing-session", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DataDirectories.Reset();
        }
    }

    [Fact]
    public async Task ConfigureAsync_WhenForkSessionRequested_CreatesForkedCopyOfResumedSession()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        var currentDirectory = Directory.GetCurrentDirectory();
        AgentSession? sourceSession = null;
        SessionSetup? setup = null;

        try
        {
            DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

            var primaryModel = new ProviderModelInfo("primary-model", "Primary Model", "TestProvider");
            var providerSetup = CreateProviderSetup(primaryModel);

            sourceSession = await CreatePersistedSessionAsync(
                providerSetup,
                currentDirectory,
                "source-session",
                assistantReply: "fork me");

            setup = await SessionConfigurator.ConfigureAsync(
                new CliOptions
                {
                    CliSessionId = sourceSession.SessionInfo!.Id,
                    ForkSession = true,
                    PrintMode = true,
                },
                providerSetup);

            var sessions = await setup.Session.Store!.ListSessionsAsync(sourceSession.SessionInfo.ProjectHash, 10);
            var fork = Assert.Single(sessions, s => string.Equals(s.Name, "CLI fork", StringComparison.Ordinal));
            var forkedSession = await setup.Session.Store.GetSessionAsync(fork.Id);

            Assert.Equal(sourceSession.SessionInfo.Id, setup.Session.SessionInfo!.Id);
            Assert.NotNull(forkedSession);
            Assert.NotEqual(sourceSession.SessionInfo.Id, forkedSession!.Id);
            Assert.Equal(sourceSession.SessionInfo.MessageCount, forkedSession.MessageCount);
            Assert.Equal(sourceSession.SessionInfo.Turns.Count, forkedSession.Turns.Count);
            Assert.Equal("fork me", forkedSession.Turns[^1].Content);
        }
        finally
        {
            setup?.Session.Store?.Dispose();
            sourceSession?.Store?.Dispose();
            DataDirectories.Reset();
        }
    }

    private static ProviderSetup CreateProviderSetup(params ProviderModelInfo[] models)
    {
        var primaryModel = models[0];
        var (registry, providerConfig, metadataProvider) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", models));

        return new ProviderSetup(
            registry,
            providerConfig,
            metadataProvider,
            models,
            primaryModel,
            [],
            registry.BuildKernel(primaryModel));
    }

    private static async Task<AgentSession> CreatePersistedSessionAsync(
        ProviderSetup providerSetup,
        string projectPath,
        string sessionName,
        ProviderModelInfo? switchToModel = null,
        string assistantReply = "assistant reply")
    {
        var session = new AgentSession(providerSetup.Registry, providerSetup.Kernel, providerSetup.SelectedModel);
        await session.InitializePersistenceAsync(projectPath);

        session.SessionInfo!.Name = sessionName;

        if (switchToModel is not null)
        {
            session.SwitchModel(switchToModel, "manual");
        }

        await session.RecordUserTurnAsync("hello");
        await session.RecordAssistantTurnAsync(assistantReply);
        await session.Store!.UpdateSessionAsync(session.SessionInfo);

        return session;
    }
}
