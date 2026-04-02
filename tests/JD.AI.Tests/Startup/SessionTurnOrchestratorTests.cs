using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Providers;
using JD.AI.Core.Safety;
using JD.AI.Core.Skills;
using JD.AI.Core.Usage;
using JD.AI.Startup;
using JD.AI.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Tests.Startup;

[Collection("DataDirectories")]
public sealed class SessionTurnOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_Blocks_WhenSessionBudgetAlreadyReached()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var (session, loop, chatService) = CreateLoopContext(new ProviderModelInfo("model", "Model", "TestProvider"));
        session.SessionSpendUsd = 5m;

        using var budgetTracker = new BudgetTracker();
        using var skills = CreateSkillManager();
        var orchestrator = new SessionTurnOrchestrator(
            session,
            CreateGovernanceSetup(fixture.DirectoryPath, budgetTracker, new BudgetPolicy { MaxSessionUsd = 5m }),
            skills);

        string? warning = null;
        var result = await orchestrator.ExecuteAsync(
            loop,
            "hello",
            new SessionTurnExecutionOptions(
                Streaming: false,
                AutoCompact: false,
                CompactThresholdPercent: 0,
                ContextWindowTokens: 0,
                OnWarning: message => warning = message));

        result.Should().Be(SessionTurnExecutionResult.BudgetBlockedResult);
        warning.Should().Contain("Budget limit ($5.00) reached");
        await chatService.DidNotReceiveWithAnyArgs().GetChatMessageContentsAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_Blocks_WhenBudgetTrackerReportsExceeded()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var (session, loop, chatService) = CreateLoopContext(new ProviderModelInfo("model", "Model", "TestProvider"));

        using var budgetTracker = new BudgetTracker();
        await budgetTracker.RecordSpendAsync(3.5m, "TestProvider");

        using var skills = CreateSkillManager();
        var orchestrator = new SessionTurnOrchestrator(
            session,
            CreateGovernanceSetup(fixture.DirectoryPath, budgetTracker, new BudgetPolicy { MaxDailyUsd = 1m }),
            skills);

        string? warning = null;
        var result = await orchestrator.ExecuteAsync(
            loop,
            "hello",
            new SessionTurnExecutionOptions(
                Streaming: false,
                AutoCompact: false,
                CompactThresholdPercent: 0,
                ContextWindowTokens: 0,
                OnWarning: message => warning = message));

        result.Should().Be(SessionTurnExecutionResult.BudgetBlockedResult);
        warning.Should().Be("Budget exceeded — daily: $3.50, monthly: $3.50.");
        await chatService.DidNotReceiveWithAnyArgs().GetChatMessageContentsAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCancelled_WhenCancellationAlreadyRequested()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<ChatMessageContent>>([new ChatMessageContent(AuthorRole.Assistant, "done")]);
            });

        var (session, loop, _) = CreateLoopContext(new ProviderModelInfo("model", "Model", "TestProvider"), chatService);

        using var budgetTracker = new BudgetTracker();
        using var skills = CreateSkillManager();
        var orchestrator = new SessionTurnOrchestrator(
            session,
            CreateGovernanceSetup(fixture.DirectoryPath, budgetTracker, budgetPolicy: null),
            skills);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        string? warning = null;
        var result = await orchestrator.ExecuteAsync(
            loop,
            "hello",
            new SessionTurnExecutionOptions(
                Streaming: false,
                AutoCompact: false,
                CompactThresholdPercent: 0,
                ContextWindowTokens: 0,
                OnWarning: message => warning = message),
            cts.Token);

        result.Should().Be(SessionTurnExecutionResult.CancelledResult);
        warning.Should().Be("Turn cancelled.");
    }

    [Fact]
    public async Task ExecuteAsync_CompletesAndRecordsEstimatedSpend()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var workspace = fixture.CreateSubdirectory("workspace");
        var model = new ProviderModelInfo("model", "Model", "TestProvider");
        var (session, loop, _) = CreateLoopContext(model);
        await session.InitializePersistenceAsync(workspace);

        using var budgetTracker = new BudgetTracker();
        using var skills = CreateSkillManager();
        var costEstimator = Substitute.For<ICostEstimator>();
        costEstimator.ResolveRates(model).Returns((0.001m, 0.002m, "test"));
        costEstimator.EstimateTurnCostUsd(model, Arg.Any<long>(), Arg.Any<long>()).Returns(1.25m);

        var orchestrator = new SessionTurnOrchestrator(
            session,
            CreateGovernanceSetup(fixture.DirectoryPath, budgetTracker, new BudgetPolicy { MaxSessionUsd = 10m }),
            skills,
            costEstimator);

        var result = await orchestrator.ExecuteAsync(
            loop,
            "hello",
            new SessionTurnExecutionOptions(
                Streaming: false,
                AutoCompact: false,
                CompactThresholdPercent: 0,
                ContextWindowTokens: 0));

        var budgetStatus = await budgetTracker.GetStatusAsync();

        result.Completed.Should().BeTrue();
        result.Response.Should().Be("done");
        session.SessionSpendUsd.Should().Be(1.25m);
        budgetStatus.TodayUsd.Should().Be(1.25m);
        session.SessionInfo.Should().NotBeNull();
        session.SessionInfo!.Turns.Should().HaveCount(2);
        costEstimator.Received(1).EstimateTurnCostUsd(model, Arg.Any<long>(), Arg.Any<long>());
    }

    private static SkillLifecycleManager CreateSkillManager() =>
        new([], userConfigPath: null, workspaceConfigPath: null);

    private static GovernanceSetup CreateGovernanceSetup(
        string rootPath,
        BudgetTracker budgetTracker,
        BudgetPolicy? budgetPolicy)
    {
        return new GovernanceSetup(
            PolicyEvaluator: null,
            AuditService: new AuditService([]),
            FileAuditSink: new FileAuditSink(Path.Combine(rootPath, "audit")),
            BudgetTracker: budgetTracker,
            BudgetPolicy: budgetPolicy,
            CircuitBreaker: new CircuitBreaker(new ToolLoopDetector()),
            CheckpointStrategy: new DirectoryCheckpointStrategy(rootPath),
            Instructions: new InstructionsResult());
    }

    private static (AgentSession Session, AgentLoop Loop, IChatCompletionService ChatService) CreateLoopContext(
        ProviderModelInfo model,
        IChatCompletionService? chatService = null)
    {
        var registry = Substitute.For<IProviderRegistry>();
        if (chatService is null)
        {
            chatService = Substitute.For<IChatCompletionService>();
            chatService
                .GetChatMessageContentsAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings?>(),
                    Arg.Any<Kernel?>(),
                    Arg.Any<CancellationToken>())
                .Returns([new ChatMessageContent(AuthorRole.Assistant, "done")]);
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model);
        return (session, new AgentLoop(session), chatService);
    }
}
