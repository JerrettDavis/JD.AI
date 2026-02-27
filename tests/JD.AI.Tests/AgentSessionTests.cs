using JD.AI.Tui.Agent;
using JD.AI.Tui.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tui.Tests;

public sealed class AgentSessionTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    private AgentSession CreateSession()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        return new AgentSession(_registry, kernel, model);
    }

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var session = CreateSession();

        Assert.NotNull(session.Kernel);
        Assert.NotNull(session.CurrentModel);
        Assert.Equal("test-model", session.CurrentModel?.Id);
        Assert.Empty(session.History);
        Assert.False(session.AutoRunEnabled);
        Assert.False(session.SkipPermissions);
        Assert.Equal(0, session.TotalTokens);
    }

    [Fact]
    public void ClearHistory_EmptiesHistoryAndResetsTokens()
    {
        var session = CreateSession();
        session.History.AddUserMessage("hello");
        session.History.AddAssistantMessage("hi");
        session.TotalTokens = 500;

        session.ClearHistory();

        Assert.Empty(session.History);
        Assert.Equal(0, session.TotalTokens);
    }

    [Fact]
    public void SwitchModel_ChangesKernelAndModel()
    {
        var session = CreateSession();
        var newModel = new ProviderModelInfo("new-model", "New", "NewProvider");
        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        session.SwitchModel(newModel);

        Assert.Equal("new-model", session.CurrentModel?.Id);
    }

    [Fact]
    public void SwitchModel_PreservesPlugins()
    {
        var session = CreateSession();
        session.Kernel.Plugins.AddFromType<DummyPlugin>("test");

        var newModel = new ProviderModelInfo("new-model", "New", "NewProvider");
        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        session.SwitchModel(newModel);

        Assert.Contains(session.Kernel.Plugins, p =>
            string.Equals(p.Name, "test", StringComparison.Ordinal));
    }

    [Fact]
    public void SwitchModel_DoesNotBreakHistory()
    {
        var session = CreateSession();
        session.History.AddUserMessage("before switch");

        var newModel = new ProviderModelInfo("new-model", "New", "NewProvider");
        _registry.BuildKernel(newModel).Returns(Kernel.CreateBuilder().Build());

        session.SwitchModel(newModel);

        Assert.Single(session.History);
        Assert.Equal("before switch", session.History[0].Content);
    }

    [Fact]
    public async Task CompactAsync_NoOpWhenFewTokens()
    {
        var session = CreateSession();
        session.History.AddUserMessage("short");

        // Should not throw — too few tokens to compact
        await session.CompactAsync();

        Assert.Single(session.History);
    }

    // Dummy plugin for SwitchModel test
    private sealed class DummyPlugin
    {
        [Microsoft.SemanticKernel.KernelFunction("dummy")]
        [System.ComponentModel.Description("A dummy function")]
        public static string Dummy() => "dummy";
    }
}
