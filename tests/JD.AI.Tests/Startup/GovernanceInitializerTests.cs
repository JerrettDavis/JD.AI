using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Tools;
using JD.AI.Startup;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Startup;

[Collection("DataDirectories")]
public sealed class GovernanceInitializerTests : IDisposable
{
    private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();

    [Fact]
    public void Initialize_ConfiguresSessionAndAppliesAllowedToolFiltering()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var cwd = fixture.CreateSubdirectory("workspace");
        Directory.SetCurrentDirectory(cwd);

        var model = new JD.AI.Core.Providers.ProviderModelInfo("model-a", "Model A", "TestProvider");
        var (registry, _, _) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", model));
        var kernel = registry.BuildKernel(model);
        kernel.ImportPluginFromObject(new MemoryTools(), "memory");
        kernel.ImportPluginFromObject(new TaskTools(), "tasks");

        var session = new AgentSession(registry, kernel, model);
        var options = new CliOptions
        {
            PrintMode = true,
            AllowedTools = ["memory"],
        };

        var setup = GovernanceInitializer.Initialize(cwd, session, kernel, options, maxBudgetUsd: 5m);

        Assert.Null(setup.PolicyEvaluator);
        Assert.NotNull(setup.AuditService);
        Assert.NotNull(session.AuditService);
        Assert.NotNull(session.LoadoutRegistry);
        Assert.NotNull(session.AllPlugins);
        Assert.NotNull(session.ApprovalService);
        Assert.IsType<DirectoryCheckpointStrategy>(setup.CheckpointStrategy);
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("memory", StringComparison.Ordinal));
        Assert.DoesNotContain(kernel.Plugins, p => p.Name.Equals("tasks", StringComparison.Ordinal));
    }

    [Fact]
    public void Initialize_UsesStashCheckpointStrategyWhenGitDirectoryExists()
    {
        using var fixture = new JD.AI.Tests.Fixtures.TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var projectPath = fixture.CreateSubdirectory("project");
        Directory.SetCurrentDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, ".git"));

        var model = new JD.AI.Core.Providers.ProviderModelInfo("model-a", "Model A", "TestProvider");
        var (registry, _, _) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", model));
        var kernel = registry.BuildKernel(model);
        var session = new AgentSession(registry, kernel, model);

        var setup = GovernanceInitializer.Initialize(
            projectPath,
            session,
            kernel,
            new CliOptions { PrintMode = true },
            maxBudgetUsd: null);

        Assert.IsType<StashCheckpointStrategy>(setup.CheckpointStrategy);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);
        DataDirectories.Reset();
    }
}
