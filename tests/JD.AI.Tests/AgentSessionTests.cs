using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests;

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
    public void SwitchModel_PreservesAutoFunctionInvocationFilters()
    {
        var session = CreateSession();
        var filter = new PassThroughAutoFunctionFilter();
        session.Kernel.AutoFunctionInvocationFilters.Add(filter);

        var newModel = new ProviderModelInfo("new-model", "New", "NewProvider");
        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        session.SwitchModel(newModel);

        Assert.Contains(
            session.Kernel.AutoFunctionInvocationFilters,
            f => ReferenceEquals(f, filter));
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

    [Fact]
    public void CaptureOriginalSystemPromptIfUnset_ThenReset_RestoresOriginalText()
    {
        var session = CreateSession();
        session.History.AddSystemMessage("Original system prompt");
        session.CaptureOriginalSystemPromptIfUnset();

        session.ReplaceSystemPrompt("Modified system prompt");
        var reset = session.TryResetSystemPrompt();

        Assert.True(reset);
        Assert.Equal("Original system prompt", session.History[0].Content);
    }

    [Fact]
    public void TryResetSystemPrompt_ReturnsFalse_WhenNoSystemPromptExists()
    {
        var session = CreateSession();

        var reset = session.TryResetSystemPrompt();

        Assert.False(reset);
        Assert.Empty(session.History);
    }

    [Fact]
    public void ResetTurnState_ClearsWorkflowDeclinedFlag()
    {
        var session = CreateSession();
        session.WorkflowDeclinedThisTurn = true;

        session.ResetTurnState();

        Assert.False(session.WorkflowDeclinedThisTurn);
    }

    [Fact]
    public void ReplaceSystemPrompt_ReplacesFirstSystemMessageInPlace()
    {
        var session = CreateSession();
        session.History.AddUserMessage("u1");
        session.History.AddSystemMessage("old");
        session.History.AddAssistantMessage("a1");

        session.ReplaceSystemPrompt("new");

        Assert.Equal("u1", session.History[0].Content);
        Assert.Equal(AuthorRole.System, session.History[1].Role);
        Assert.Equal("new", session.History[1].Content);
        Assert.Equal("a1", session.History[2].Content);
    }

    [Fact]
    public void CycleReasoningEffort_FollowsExpectedOrder()
    {
        var session = CreateSession();

        Assert.Equal(ReasoningEffort.Low, session.CycleReasoningEffort());
        Assert.Equal(ReasoningEffort.Medium, session.CycleReasoningEffort());
        Assert.Equal(ReasoningEffort.High, session.CycleReasoningEffort());
        Assert.Equal(ReasoningEffort.Max, session.CycleReasoningEffort());
        Assert.Null(session.CycleReasoningEffort());
    }

    [Fact]
    public void TryRegisterToolCallForCurrentTurn_DeduplicatesWithinTurn()
    {
        var session = CreateSession();

        var first = session.TryRegisterToolCallForCurrentTurn("run_command", "command=ls");
        var duplicate = session.TryRegisterToolCallForCurrentTurn("run_command", "command=ls");

        Assert.True(first);
        Assert.False(duplicate);
    }

    [Fact]
    public void TryRegisterToolCallForCurrentTurn_ResetTurnState_AllowsSameFingerprintAgain()
    {
        var session = CreateSession();

        _ = session.TryRegisterToolCallForCurrentTurn("run_command", "command=ls");
        session.ResetTurnState();
        var afterReset = session.TryRegisterToolCallForCurrentTurn("run_command", "command=ls");

        Assert.True(afterReset);
    }

    // Dummy plugin for SwitchModel test
    private sealed class DummyPlugin
    {
        [Microsoft.SemanticKernel.KernelFunction("dummy")]
        [System.ComponentModel.Description("A dummy function")]
        public static string Dummy() => "dummy";
    }

    private sealed class PassThroughAutoFunctionFilter : IAutoFunctionInvocationFilter
    {
        public Task OnAutoFunctionInvocationAsync(
            AutoFunctionInvocationContext context,
            Func<AutoFunctionInvocationContext, Task> next) => next(context);
    }
}
