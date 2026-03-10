using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class SingleTurnExecutorTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an AgentSession backed by a kernel that contains the supplied chat service.
    /// </summary>
    private static AgentSession SessionWithChatService(
        IChatCompletionService chatService,
        ProviderModelInfo? model = null)
    {
        model ??= new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();
        return new AgentSession(registry, kernel, model);
    }

    /// <summary>
    /// Returns an async-enumerable that yields the given text chunks one by one.
    /// </summary>
    private static async IAsyncEnumerable<StreamingChatMessageContent> Chunks(
        params string[] texts)
    {
        foreach (var text in texts)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            await Task.Yield();
        }
    }

    /// <summary>
    /// Returns an async-enumerable that produces no items.
    /// </summary>
    private static async IAsyncEnumerable<StreamingChatMessageContent> NoChunks()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static SubagentConfig Cfg(
        string name = "agent1",
        string prompt = "do work",
        string? systemPrompt = null,
        SubagentType type = SubagentType.General) =>
        new()
        {
            Name = name,
            Prompt = prompt,
            SystemPrompt = systemPrompt,
            Type = type,
        };

    private static IChatCompletionService ChatServiceReturning(
        params string[] chunks)
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks(chunks));
        return svc;
    }

    private static IChatCompletionService ChatServiceReturningEmpty()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(NoChunks());
        return svc;
    }

    // ── ExecuteAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ValidResponse_ReturnsSuccessResult()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("Hello, world!"));

        var result = await sut.ExecuteAsync(Cfg(), session);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Hello, world!");
        result.AgentName.Should().Be("agent1");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleChunks_ConcatenatesOutput()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("Hello", ", ", "world", "!"));

        var result = await sut.ExecuteAsync(Cfg(), session);

        result.Output.Should().Be("Hello, world!");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyResponse_ReturnsNoResponseFallback()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturningEmpty());

        var result = await sut.ExecuteAsync(Cfg(), session);

        result.Output.Should().Be("(no response)");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DurationIsPositive()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));

        var result = await sut.ExecuteAsync(Cfg(), session);

        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ── ExecuteAsync — events ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessPath_RecordsStartedAndCompletedEvents()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("output"));

        var result = await sut.ExecuteAsync(Cfg("myagent"), session);

        result.Events.Should().HaveCount(2);
        result.Events[0].EventType.Should().Be(AgentEventType.Started);
        result.Events[0].AgentName.Should().Be("myagent");
        result.Events[1].EventType.Should().Be(AgentEventType.Completed);
        result.Events[1].AgentName.Should().Be("myagent");
    }

    [Fact]
    public async Task ExecuteAsync_StartedEvent_ContainsPromptInContent()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));

        var result = await sut.ExecuteAsync(Cfg(prompt: "do the thing"), session);

        result.Events[0].Content.Should().Be("do the thing");
    }

    [Fact]
    public async Task ExecuteAsync_CompletedEvent_ContainsCharCountInContent()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("hello"));

        var result = await sut.ExecuteAsync(Cfg(), session);

        // "hello" = 5 chars
        result.Events[1].Content.Should().Be("5 chars");
    }

    // ── ExecuteAsync — progress callbacks ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CallsOnProgressWithStarted()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));
        var progresses = new List<SubagentProgress>();

        await sut.ExecuteAsync(Cfg("myagent"), session, onProgress: progresses.Add);

        progresses.Should().ContainSingle(p => p.Status == SubagentStatus.Started);
        progresses.First(p => p.Status == SubagentStatus.Started)
            .AgentName.Should().Be("myagent");
    }

    [Fact]
    public async Task ExecuteAsync_CallsOnProgressWithThinking()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));
        var progresses = new List<SubagentProgress>();

        await sut.ExecuteAsync(Cfg("myagent"), session, onProgress: progresses.Add);

        progresses.Should().ContainSingle(p => p.Status == SubagentStatus.Thinking);
    }

    [Fact]
    public async Task ExecuteAsync_CallsOnProgressWithCompleted()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));
        var progresses = new List<SubagentProgress>();

        await sut.ExecuteAsync(Cfg("myagent"), session, onProgress: progresses.Add);

        progresses.Should().ContainSingle(p => p.Status == SubagentStatus.Completed);
        var completed = progresses.First(p => p.Status == SubagentStatus.Completed);
        completed.Elapsed.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NullOnProgress_DoesNotThrow()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));

        var act = async () => await sut.ExecuteAsync(Cfg(), session, onProgress: null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ProgressOrder_IsStartedThenThinkingThenCompleted()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));
        var statuses = new List<SubagentStatus>();

        await sut.ExecuteAsync(Cfg(), session, onProgress: p => statuses.Add(p.Status));

        statuses.Should().Equal(
            SubagentStatus.Started,
            SubagentStatus.Thinking,
            SubagentStatus.Completed);
    }

    // ── ExecuteAsync — TeamContext ─────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithTeamContext_RecordsStartedAndCompletedEvents()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("result"));
        var ctx = new TeamContext("test goal");

        await sut.ExecuteAsync(Cfg("myagent"), session, teamContext: ctx);

        var events = ctx.GetEventsSnapshot();
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(AgentEventType.Started);
        events[1].EventType.Should().Be(AgentEventType.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_WithTeamContext_StoresResultInContext()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("stored output"));
        var ctx = new TeamContext("goal");

        await sut.ExecuteAsync(Cfg("myagent"), session, teamContext: ctx);

        var stored = ctx.GetResult("myagent");
        stored.Should().NotBeNull();
        stored!.Output.Should().Be("stored output");
        stored.AgentName.Should().Be("myagent");
    }

    [Fact]
    public async Task ExecuteAsync_WithTeamContext_AppendsContextSummaryToSystemPrompt()
    {
        // Capture what system prompt gets used by inspecting what's passed to the chat service
        ChatHistory? capturedHistory = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Do<ChatHistory>(h => capturedHistory = h),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("done"));

        var session = SessionWithChatService(svc);
        var ctx = new TeamContext("the team goal");

        var sut = new SingleTurnExecutor();
        await sut.ExecuteAsync(Cfg(), session, teamContext: ctx);

        capturedHistory.Should().NotBeNull();
        var systemMsg = capturedHistory!
            .FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;
        systemMsg.Should().Contain("the team goal");
    }

    [Fact]
    public async Task ExecuteAsync_NullTeamContext_DoesNotThrow()
    {
        var sut = new SingleTurnExecutor();
        var session = SessionWithChatService(ChatServiceReturning("ok"));

        var act = async () => await sut.ExecuteAsync(Cfg(), session, teamContext: null);
        await act.Should().NotThrowAsync();
    }

    // ── ExecuteAsync — system prompt selection ────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CustomSystemPrompt_UsesConfigSystemPrompt()
    {
        ChatHistory? capturedHistory = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Do<ChatHistory>(h => capturedHistory = h),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var session = SessionWithChatService(svc);
        var cfg = Cfg(systemPrompt: "You are a pirate assistant.");

        var sut = new SingleTurnExecutor();
        await sut.ExecuteAsync(cfg, session);

        var systemMsg = capturedHistory!
            .FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;
        systemMsg.Should().Be("You are a pirate assistant.");
    }

    [Fact]
    public async Task ExecuteAsync_NoCustomSystemPrompt_UsesDefaultForType()
    {
        ChatHistory? capturedHistory = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Do<ChatHistory>(h => capturedHistory = h),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var session = SessionWithChatService(svc);
        // Explore type has a well-known default prompt
        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "look around",
            Type = SubagentType.Explore,
        };

        var sut = new SingleTurnExecutor();
        await sut.ExecuteAsync(cfg, session);

        var systemMsg = capturedHistory!
            .FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;
        systemMsg.Should().Contain("explore subagent");
    }

    [Fact]
    public async Task ExecuteAsync_UserMessageMatchesPrompt()
    {
        ChatHistory? capturedHistory = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Do<ChatHistory>(h => capturedHistory = h),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(prompt: "analyze this code"), session);

        var userMsg = capturedHistory!
            .FirstOrDefault(m => m.Role == AuthorRole.User)?.Content;
        userMsg.Should().Be("analyze this code");
    }

    // ── ExecuteAsync — cancellation ────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ReturnsCancelledResult()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var session = SessionWithChatService(svc);
        var cts = new CancellationTokenSource();
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg("myagent"), session, ct: cts.Token);

        result.Success.Should().BeFalse();
        result.Output.Should().Be("[cancelled]");
        result.Error.Should().Be("Cancelled");
        result.AgentName.Should().Be("myagent");
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_RecordsCancelledEvent()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg("myagent"), session);

        result.Events.Should().ContainSingle(e => e.EventType == AgentEventType.Cancelled);
        result.Events.Should().ContainSingle(e => e.EventType == AgentEventType.Started);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_CallsOnProgressWithCancelled()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var session = SessionWithChatService(svc);
        var statuses = new List<SubagentStatus>();
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session, onProgress: p => statuses.Add(p.Status));

        statuses.Should().Contain(SubagentStatus.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_WithTeamContext_RecordsCancelledEvent()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var session = SessionWithChatService(svc);
        var ctx = new TeamContext("goal");
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg("myagent"), session, teamContext: ctx);

        ctx.GetEventsSnapshot()
            .Should().ContainSingle(e => e.EventType == AgentEventType.Cancelled);
    }

    // ── ExecuteAsync — generic exception handling ──────────────────────────

    [Fact]
    public async Task ExecuteAsync_Exception_ReturnsFailureResult()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg("myagent"), session);

        result.Success.Should().BeFalse();
        result.Output.Should().BeEmpty();
        result.Error.Should().Be("boom");
        result.AgentName.Should().Be("myagent");
    }

    [Fact]
    public async Task ExecuteAsync_Exception_RecordsErrorEvent()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("something went wrong"));

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg("myagent"), session);

        result.Events.Should().ContainSingle(e => e.EventType == AgentEventType.Error);
        result.Events.First(e => e.EventType == AgentEventType.Error)
            .Content.Should().Be("something went wrong");
    }

    [Fact]
    public async Task ExecuteAsync_Exception_CallsOnProgressWithFailed()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("bad"));

        var session = SessionWithChatService(svc);
        var statuses = new List<SubagentStatus>();
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session, onProgress: p => statuses.Add(p.Status));

        statuses.Should().Contain(SubagentStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_Exception_WithTeamContext_RecordsErrorEvent()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("network failure"));

        var session = SessionWithChatService(svc);
        var ctx = new TeamContext("goal");
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg("myagent"), session, teamContext: ctx);

        ctx.GetEventsSnapshot()
            .Should().ContainSingle(e => e.EventType == AgentEventType.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Exception_DurationIsSet()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("err"));

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg(), session);

        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ── ExecuteAsync — model capabilities ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ModelWithoutToolCalling_PassesNullFunctionChoiceBehavior()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        // Model with Chat-only (no ToolCalling)
        var model = new ProviderModelInfo(
            "chat-only", "Chat Only", "TestProvider",
            Capabilities: ModelCapabilities.Chat);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        capturedSettings.Should().NotBeNull();
        // FunctionChoiceBehavior should be null when model lacks ToolCalling
        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings.Should().NotBeNull();
        openAiSettings!.FunctionChoiceBehavior.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ModelWithToolCalling_PassesAutoFunctionChoiceBehavior()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo(
            "tool-model", "Tool Model", "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings.Should().NotBeNull();
        openAiSettings!.FunctionChoiceBehavior.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MistralModelWithToolCalling_UsesMistralToolCallBehavior()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo(
            "mistral-large-pixtral-2411",
            "Mistral Large Pixtral 2411",
            "Mistral",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

#pragma warning disable SKEXP0070
        var mistralSettings = capturedSettings as MistralAIPromptExecutionSettings;
        mistralSettings.Should().NotBeNull();
        mistralSettings!.ToolCallBehavior.Should().NotBeNull();
#pragma warning restore SKEXP0070
    }

    [Fact]
    public async Task ExecuteAsync_MistralChatOnlyModel_DisablesMistralToolCallBehavior()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo(
            "mistral-medium-2508",
            "Mistral Medium 2508",
            "Mistral",
            Capabilities: ModelCapabilities.Chat);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

#pragma warning disable SKEXP0070
        var mistralSettings = capturedSettings as MistralAIPromptExecutionSettings;
        mistralSettings.Should().NotBeNull();
        mistralSettings!.ToolCallBehavior.Should().BeNull();
#pragma warning restore SKEXP0070
    }

    [Fact]
    public async Task ExecuteAsync_ModelIdPassedToSettings()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo("specific-model-id", "Specific Model", "TestProvider");
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings!.ModelId.Should().Be("specific-model-id");
    }

    [Fact]
    public async Task ExecuteAsync_WithReasoningEffortOverride_MapsProviderReasoningSettings()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo("claude-sonnet-4-6", "Claude Sonnet 4.6", "Anthropic");
        var session = SessionWithChatService(svc, model);
        session.ReasoningEffortOverride = ReasoningEffort.Max;
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings.Should().NotBeNull();
        openAiSettings!.ExtensionData.Should().ContainKey("output_config");
        openAiSettings.ExtensionData!["output_config"]
            .Should().BeOfType<Dictionary<string, object>>();
        var outputConfig = (Dictionary<string, object>)openAiSettings.ExtensionData["output_config"];
        outputConfig["effort"].Should().Be("max");
    }

    [Fact]
    public async Task ExecuteAsync_ModelMaxOutputTokensZero_DefaultsTo4096()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo("m", "M", "P", MaxOutputTokens: 0);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings!.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public async Task ExecuteAsync_ModelMaxOutputTokensPositive_UsesModelValue()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("ok"));

        var model = new ProviderModelInfo("m", "M", "P", MaxOutputTokens: 8192);
        var session = SessionWithChatService(svc, model);
        var sut = new SingleTurnExecutor();

        await sut.ExecuteAsync(Cfg(), session);

        var openAiSettings = capturedSettings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        openAiSettings!.MaxTokens.Should().Be(8192);
    }

    // ── BuildScopedKernel ─────────────────────────────────────────────────

    [Fact]
    public void BuildScopedKernel_ReturnsChatServiceFromParentKernel()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var session = SessionWithChatService(svc);
        var cfg = Cfg();

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        var resolved = kernel.GetRequiredService<IChatCompletionService>();
        resolved.Should().BeSameAs(svc);
    }

    [Fact]
    public void BuildScopedKernel_NoMatchingPlugins_ReturnsKernelWithNoPlugins()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // Add a plugin that does NOT match any toolset for 'Explore' type
        var unrelatedPlugin = KernelPluginFactory.CreateFromFunctions(
            "UnrelatedPlugin",
            [KernelFunctionFactory.CreateFromMethod(() => "noop", "Noop")]);
        parentKernel.Plugins.Add(unrelatedPlugin);

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        // Explore type wants: FileTools, SearchTools, GitTools, MemoryTools
        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "look",
            Type = SubagentType.Explore,
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().BeEmpty("UnrelatedPlugin is not in Explore's tool set");
    }

    [Fact]
    public void BuildScopedKernel_MatchingPlugin_IsIncludedInScopedKernel()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // "FileTools" is part of Explore's toolset
        var filePlugin = KernelPluginFactory.CreateFromFunctions(
            "FileTools",
            [KernelFunctionFactory.CreateFromMethod(() => "content", "ReadFile")]);
        parentKernel.Plugins.Add(filePlugin);

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "look",
            Type = SubagentType.Explore,
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().ContainSingle(p => p.Name == "FileTools");
    }

    [Fact]
    public void BuildScopedKernel_PluginMatchingIsCaseInsensitive()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // Use lowercase "filetools" — should still match "FileTools" in the toolset
        var plugin = KernelPluginFactory.CreateFromFunctions(
            "filetools",
            [KernelFunctionFactory.CreateFromMethod(() => "data", "Get")]);
        parentKernel.Plugins.Add(plugin);

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "look",
            Type = SubagentType.Explore,
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().ContainSingle(p =>
            string.Equals(p.Name, "filetools", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildScopedKernel_AdditionalTools_AreIncludedInScopedKernel()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // "ShellTools" is NOT in Explore's toolset by default, but added via AdditionalTools
        var shellPlugin = KernelPluginFactory.CreateFromFunctions(
            "ShellTools",
            [KernelFunctionFactory.CreateFromMethod(() => "output", "Run")]);
        parentKernel.Plugins.Add(shellPlugin);

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "run",
            Type = SubagentType.Explore,
            AdditionalTools = ["ShellTools"],
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().ContainSingle(p => p.Name == "ShellTools");
    }

    [Fact]
    public void BuildScopedKernel_MultiplePlugins_OnlyMatchingOnesIncluded()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // FileTools is in Explore's toolset, ShellTools is not
        var filePlugin = KernelPluginFactory.CreateFromFunctions(
            "FileTools",
            [KernelFunctionFactory.CreateFromMethod(() => "content", "Read")]);
        var shellPlugin = KernelPluginFactory.CreateFromFunctions(
            "ShellTools",
            [KernelFunctionFactory.CreateFromMethod(() => "out", "Run")]);
        parentKernel.Plugins.Add(filePlugin);
        parentKernel.Plugins.Add(shellPlugin);

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "look",
            Type = SubagentType.Explore,
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().ContainSingle(p => p.Name == "FileTools");
        kernel.Plugins.Should().NotContain(p => p.Name == "ShellTools");
    }

    [Fact]
    public void BuildScopedKernel_GeneralType_IncludesAllKnownPlugins()
    {
        var svc = Substitute.For<IChatCompletionService>();
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(svc);
        var parentKernel = builder.Build();

        // General type includes: FileTools, SearchTools, GitTools, ShellTools, WebTools, MemoryTools
        string[] generalPlugins = ["FileTools", "SearchTools", "GitTools", "ShellTools", "WebTools", "MemoryTools"];
        foreach (var pluginName in generalPlugins)
        {
            var p = KernelPluginFactory.CreateFromFunctions(
                pluginName,
                [KernelFunctionFactory.CreateFromMethod(() => "x", "Do")]);
            parentKernel.Plugins.Add(p);
        }

        var model = new ProviderModelInfo("m", "M", "P");
        var session = new AgentSession(registry, parentKernel, model);

        var cfg = new SubagentConfig
        {
            Name = "agent",
            Prompt = "work",
            Type = SubagentType.General,
        };

        var kernel = SingleTurnExecutor.BuildScopedKernel(cfg, session);

        kernel.Plugins.Should().HaveCount(generalPlugins.Length);
        foreach (var pluginName in generalPlugins)
        {
            kernel.Plugins.Should().ContainSingle(p => p.Name == pluginName,
                $"{pluginName} should be included for General type");
        }
    }

    // ── ExecuteAsync — implements ISubagentExecutor ────────────────────────

    [Fact]
    public void SingleTurnExecutor_ImplementsISubagentExecutor()
    {
        var sut = new SingleTurnExecutor();
        sut.Should().BeAssignableTo<ISubagentExecutor>();
    }

    // ── ExecuteAsync — null content chunks are skipped ────────────────────

    [Fact]
    public async Task ExecuteAsync_ChunksWithNullContent_SkippedInOutput()
    {
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(ChunksWithNullAndEmpty());

        var session = SessionWithChatService(svc);
        var sut = new SingleTurnExecutor();

        var result = await sut.ExecuteAsync(Cfg(), session);

        // Only "hello" should appear — null/empty chunks are skipped
        result.Output.Should().Be("hello");
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> ChunksWithNullAndEmpty()
    {
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, null);
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, "");
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, "hello");
        await Task.Yield();
    }
}
