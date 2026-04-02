using System.Reflection;
using FluentAssertions;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Safety;
using JD.AI.Core.Skills;
using JD.AI.Rendering;
using JD.AI.Services;
using JD.AI.Startup;
using JD.AI.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Tests.Startup;

[Collection("DataDirectories")]
public sealed class InteractiveLoopTests
{
    [Fact]
    public async Task CheckSystemPromptBudgetAsync_AutoMode_CompactsWhenPromptExceedsBudget()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var systemPrompt = string.Join(' ', Enumerable.Repeat("instruction", 120));
        var (loop, session, chatService) = CreateLoopContext(
            fixture,
            new ProviderModelInfo("model", "Model", "TestProvider", ContextWindowTokens: 100),
            systemPrompt);

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "compressed prompt")]);

        await InvokeCheckSystemPromptBudgetAsync(loop, new TuiSettings
        {
            SystemPromptBudgetPercent = 10,
            SystemPromptCompaction = SystemPromptCompaction.Auto,
        });

        session.History[0].Content.Should().Be("compressed prompt");
    }

    [Fact]
    public async Task CheckSystemPromptBudgetAsync_AlwaysMode_CompactsWhenPromptExceedsBudget()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var systemPrompt = string.Join(' ', Enumerable.Repeat("instruction", 80));
        var (loop, session, chatService) = CreateLoopContext(
            fixture,
            new ProviderModelInfo("model", "Model", "TestProvider", ContextWindowTokens: 100),
            systemPrompt);

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "always compacted")]);

        await InvokeCheckSystemPromptBudgetAsync(loop, new TuiSettings
        {
            SystemPromptBudgetPercent = 10,
            SystemPromptCompaction = SystemPromptCompaction.Always,
        });

        session.History[0].Content.Should().Be("always compacted");
    }

    [Fact]
    public async Task CheckSystemPromptBudgetAsync_OffMode_LeavesPromptUnchangedWhenOverBudget()
    {
        using var fixture = new TempDirectoryFixture();
        DataDirectories.SetRoot(fixture.GetPath("jdai-root"));

        var originalPrompt = string.Join(' ', Enumerable.Repeat("instruction", 120));
        var (loop, session, chatService) = CreateLoopContext(
            fixture,
            new ProviderModelInfo("model", "Model", "TestProvider", ContextWindowTokens: 100),
            originalPrompt);

        await InvokeCheckSystemPromptBudgetAsync(loop, new TuiSettings
        {
            SystemPromptBudgetPercent = 10,
            SystemPromptCompaction = SystemPromptCompaction.Off,
        });

        session.History[0].Content.Should().Be(originalPrompt);
    }

    [Fact]
    public void WireEventHooks_TogglePlanMode_CyclesThroughAllPermissionModes()
    {
        var session = CreateSession(new ProviderModelInfo("model", "Model", "TestProvider"), "system prompt");
        var interactiveInput = new InteractiveInput(new CompletionProvider());

        InvokeWireEventHooks(interactiveInput, session, "system prompt");

        RaiseInteractiveEvent(interactiveInput, "OnTogglePlanMode");
        session.PermissionMode.Should().Be(PermissionMode.Plan);

        RaiseInteractiveEvent(interactiveInput, "OnTogglePlanMode");
        session.PermissionMode.Should().Be(PermissionMode.AcceptEdits);

        RaiseInteractiveEvent(interactiveInput, "OnTogglePlanMode");
        session.PermissionMode.Should().Be(PermissionMode.Normal);
    }

    [Fact]
    public void WireEventHooks_ToggleExtendedThinking_CyclesReasoningEffort()
    {
        var session = CreateSession(new ProviderModelInfo("model", "Model", "TestProvider"), "system prompt");
        var interactiveInput = new InteractiveInput(new CompletionProvider());

        InvokeWireEventHooks(interactiveInput, session, "system prompt");

        RaiseInteractiveEvent(interactiveInput, "OnToggleExtendedThinking");
        session.ReasoningEffortOverride.Should().Be(ReasoningEffort.Low);

        RaiseInteractiveEvent(interactiveInput, "OnToggleExtendedThinking");
        session.ReasoningEffortOverride.Should().Be(ReasoningEffort.Medium);
    }

    private static (InteractiveLoop Loop, AgentSession Session, IChatCompletionService ChatService) CreateLoopContext(
        TempDirectoryFixture fixture,
        ProviderModelInfo model,
        string systemPrompt)
    {
        var chatService = Substitute.For<IChatCompletionService>();
        var session = CreateSession(model, systemPrompt, chatService);
        var registry = new ProviderRegistry([]);
        var loop = new InteractiveLoop(
            session,
            new CliOptions(),
            model,
            [model],
            session.Kernel,
            registry,
            new ProviderConfigurationManager(Substitute.For<ICredentialStore>()),
            new AtomicConfigStore(fixture.GetPath("config.json")),
            new ModelMetadataProvider(),
            CreateGovernanceSetup(fixture.DirectoryPath),
            new SkillLifecycleManager([], userConfigPath: null, workspaceConfigPath: null),
            _ => { },
            systemPrompt,
            new PluginLoader(NullLogger<PluginLoader>.Instance),
            pluginManager: Substitute.For<IPluginLifecycleManager>(),
            gateway: null);

        return (loop, session, chatService);
    }

    private static AgentSession CreateSession(
        ProviderModelInfo model,
        string systemPrompt,
        IChatCompletionService? chatService = null)
    {
        chatService ??= Substitute.For<IChatCompletionService>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(Substitute.For<IProviderRegistry>(), kernel, model);
        session.History.AddSystemMessage(systemPrompt);
        return session;
    }

    private static GovernanceSetup CreateGovernanceSetup(string rootPath)
    {
        return new GovernanceSetup(
            PolicyEvaluator: null,
            AuditService: new AuditService([]),
            FileAuditSink: new FileAuditSink(Path.Combine(rootPath, "audit")),
            BudgetTracker: new BudgetTracker(),
            BudgetPolicy: null,
            CircuitBreaker: new CircuitBreaker(new ToolLoopDetector()),
            CheckpointStrategy: new DirectoryCheckpointStrategy(rootPath),
            Instructions: new InstructionsResult());
    }

    private static async Task InvokeCheckSystemPromptBudgetAsync(InteractiveLoop loop, TuiSettings settings)
    {
        var method = typeof(InteractiveLoop).GetMethod(
            "CheckSystemPromptBudgetAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = method!.Invoke(loop, [settings]) as Task;
        task.Should().NotBeNull();
        await task!;
    }

    private static void InvokeWireEventHooks(InteractiveInput interactiveInput, AgentSession session, string systemPrompt)
    {
        var method = typeof(InteractiveLoop).GetMethod(
            "WireEventHooks",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        method!.Invoke(null, [interactiveInput, session, systemPrompt]);
    }

    private static void RaiseInteractiveEvent(InteractiveInput interactiveInput, string eventName)
    {
        var field = typeof(InteractiveInput).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();

        var handler = field!.GetValue(interactiveInput) as MulticastDelegate;
        handler.Should().NotBeNull();
        try
        {
            handler!.DynamicInvoke(interactiveInput, EventArgs.Empty);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is IOException)
        {
            // ChatRenderer hits console-size APIs that are unavailable under test runners.
            // The state transition already occurred before the render call.
        }
    }
}
