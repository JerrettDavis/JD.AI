using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Startup;
using Microsoft.SemanticKernel;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Governance Initializer")]
[Collection("DataDirectories")]
public sealed class GovernanceInitializerBddTests : TinyBddXunitBase
{
    public GovernanceInitializerBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Initialize wires governance services for a normal project folder"), Fact]
    public async Task Initialize_WithNoGit_UsesDirectoryCheckpointAndRegistersPlugins()
    {
        GovernanceSetup? setup = null;
        Kernel? kernel = null;
        AgentSession? session = null;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"jdai-governance-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tempRoot, "project");
        var dataDir = Path.Combine(tempRoot, "data");
        var originalCwd = Directory.GetCurrentDirectory();

        await Given("a project directory without git metadata", () =>
            {
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(dataDir);
                DataDirectories.SetRoot(dataDir);
                Directory.SetCurrentDirectory(projectDir);

                kernel = Kernel.CreateBuilder().Build();
                kernel.Plugins.AddFromType<KeepPlugin>("keep");
                session = CreateSession(kernel);

                return (kernel, session);
            })
            .When("governance initialization runs", ctx =>
            {
                setup = GovernanceInitializer.Initialize(
                    projectDir,
                    ctx.session,
                    ctx.kernel,
                    new CliOptions { PrintMode = true },
                    maxBudgetUsd: 25m);
                return ctx;
            })
            .Then("audit budget checkpointing and core plugins are available", _ =>
            {
                setup.Should().NotBeNull();
                setup!.AuditService.Should().NotBeNull();
                setup.BudgetPolicy.Should().NotBeNull();
                setup.BudgetPolicy!.MaxSessionUsd.Should().Be(25m);
                setup.CheckpointStrategy.Should().BeOfType<DirectoryCheckpointStrategy>();

                kernel!.Plugins.Should().Contain(p => p.Name == "policy");
                kernel.Plugins.Should().Contain(p => p.Name == "toolDiscovery");
                kernel.Plugins.Should().Contain(p => p.Name == "SubagentTools");

                session!.AuditService.Should().BeSameAs(setup.AuditService);
                session.LoadoutRegistry.Should().NotBeNull();
                session.AllPlugins.Should().Contain(p => p.Name == "keep");
                return true;
            })
            .AssertPassed();

        Directory.SetCurrentDirectory(originalCwd);
        DataDirectories.Reset();
        try { Directory.Delete(tempRoot, recursive: true); }
        catch { /* best effort */ }
    }

    [Scenario("Initialize chooses stash checkpointing in a git repository"), Fact]
    public async Task Initialize_WithGitDirectory_UsesStashCheckpointing()
    {
        GovernanceSetup? setup = null;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"jdai-governance-git-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tempRoot, "project");
        var dataDir = Path.Combine(tempRoot, "data");
        var originalCwd = Directory.GetCurrentDirectory();

        await Given("a project directory containing .git", () =>
            {
                Directory.CreateDirectory(Path.Combine(projectDir, ".git"));
                Directory.CreateDirectory(dataDir);
                DataDirectories.SetRoot(dataDir);
                Directory.SetCurrentDirectory(projectDir);

                var kernel = Kernel.CreateBuilder().Build();
                var session = CreateSession(kernel);
                return (kernel, session);
            })
            .When("governance initialization runs", ctx =>
            {
                setup = GovernanceInitializer.Initialize(
                    projectDir,
                    ctx.session,
                    ctx.kernel,
                    new CliOptions { PrintMode = true },
                    maxBudgetUsd: null);
                return ctx;
            })
            .Then("stash checkpointing is selected", _ =>
            {
                setup.Should().NotBeNull();
                setup!.CheckpointStrategy.Should().BeOfType<StashCheckpointStrategy>();
                return true;
            })
            .AssertPassed();

        Directory.SetCurrentDirectory(originalCwd);
        DataDirectories.Reset();
        try { Directory.Delete(tempRoot, recursive: true); }
        catch { /* best effort */ }
    }

    [Scenario("Allowed and disallowed tool filters shape available plugins"), Fact]
    public async Task Initialize_AppliesAllowedAndDisallowedTools()
    {
        Kernel? kernel = null;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"jdai-governance-filter-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tempRoot, "project");
        var dataDir = Path.Combine(tempRoot, "data");
        var originalCwd = Directory.GetCurrentDirectory();

        await Given("a kernel with keep and drop plugins", () =>
            {
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(dataDir);
                DataDirectories.SetRoot(dataDir);
                Directory.SetCurrentDirectory(projectDir);

                kernel = Kernel.CreateBuilder().Build();
                kernel.Plugins.AddFromType<KeepPlugin>("keep");
                kernel.Plugins.AddFromType<DropPlugin>("drop");
                return CreateSession(kernel);
            })
            .When("initializing with allow and disallow lists", session =>
            {
                var opts = new CliOptions
                {
                    PrintMode = true,
                    AllowedTools = ["keep", "policy", "toolDiscovery", "SubagentTools"],
                    DisallowedTools = ["drop"],
                };

                GovernanceInitializer.Initialize(
                    projectDir,
                    session,
                    kernel!,
                    opts,
                    maxBudgetUsd: null);
                return session;
            })
            .Then("only allowed plugins remain and disallowed ones are removed", _ =>
            {
                kernel!.Plugins.Should().Contain(p => p.Name == "keep");
                kernel.Plugins.Should().Contain(p => p.Name == "policy");
                kernel.Plugins.Should().Contain(p => p.Name == "toolDiscovery");
                kernel.Plugins.Should().Contain(p => p.Name == "SubagentTools");
                kernel.Plugins.Should().NotContain(p => p.Name == "drop");
                return true;
            })
            .AssertPassed();

        Directory.SetCurrentDirectory(originalCwd);
        DataDirectories.Reset();
        try { Directory.Delete(tempRoot, recursive: true); }
        catch { /* best effort */ }
    }

    private static AgentSession CreateSession(Kernel kernel)
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        return new AgentSession(registry, kernel, model)
        {
            NoSessionPersistence = true,
        };
    }

    private sealed class KeepPlugin
    {
        [KernelFunction("keep_tool")]
        public string Keep() => "ok";
    }

    private sealed class DropPlugin
    {
        [KernelFunction("drop_tool")]
        public string Drop() => "no";
    }
}
