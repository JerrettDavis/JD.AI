using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using System.Runtime.CompilerServices;
using Xunit;

namespace JD.AI.Tests.Orchestration;

public sealed class MultiTurnExecutorTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    private AgentSession CreateSession(IChatCompletionService? chatService = null)
    {
        var builder = Kernel.CreateBuilder();
        if (chatService != null)
        {
            builder.Services.AddSingleton(chatService);
        }

        var kernel = builder.Build();
        // ProviderModelInfo defaults to Chat | ToolCalling, no mutation needed
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(_registry, kernel, model);

        return session;
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> AsyncChunks(
        [EnumeratorCancellation] CancellationToken ct = default,
        params string[] texts)
    {
        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            await Task.Yield();
        }
    }

    private SubagentConfig CreateConfig(
        string name = "test-agent",
        string prompt = "Test prompt",
        int maxTurns = 10)
    {
        return new SubagentConfig
        {
            Name = name,
            Prompt = prompt,
            MaxTurns = maxTurns,
            Type = SubagentType.General,
        };
    }

    [Fact]
    public async Task ExecuteAsync_DoneMarkerOnFirstTurn_ExitsAfterOneTurn()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Here is the answer [DONE]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig(maxTurns: 5);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("[DONE]");
        result.Output.Should().Contain("answer");

        // Verify chat service was called exactly once
        chatService.Received(1).GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoneMarkerCaseInsensitive_ExitsEarly()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        var callCount = 0;
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Done with response [done]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig(maxTurns: 5);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("[done]");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NoDoneMarker_ExhaustsMaxTurns()
    {
        // Arrange
        var callCount = 0;
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, $"Thinking step {callCount}...");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig(maxTurns: 3);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        callCount.Should().Be(3);
        result.Output.Should().Contain("Thinking step");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeFirstTurn_ReturnsCancelledResult()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null, ct: cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrows_ReturnsFailedResult()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => { throw new InvalidOperationException("Service unavailable"); });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig();

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task ExecuteAsync_Duration_GreaterThanZero()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Quick response [DONE]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig();

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ProgressCallbacks_FiredCorrectly()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Response [DONE]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig();

        var progressCalls = new List<SubagentProgress>();
        void OnProgress(SubagentProgress progress) => progressCalls.Add(progress);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null, onProgress: OnProgress);

        // Assert
        result.Success.Should().BeTrue();
        progressCalls.Should().NotBeEmpty();

        // Should have Started, Thinking, and Completed
        var statuses = progressCalls.Select(p => p.Status).ToList();
        statuses.Should().Contain(SubagentStatus.Started);
        statuses.Should().Contain(SubagentStatus.Thinking);
        statuses.Should().Contain(SubagentStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_AllOutputAccumulatesAcrossTurns()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        var turnNumber = 0;
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                turnNumber++;
                if (turnNumber >= 2)
                {
                    var ct = callInfo.Arg<CancellationToken>();
                    return AsyncChunks(ct, $"Turn {turnNumber} response [DONE]");
                }
                else
                {
                    var ct = callInfo.Arg<CancellationToken>();
                    return AsyncChunks(ct, $"Turn {turnNumber} response");
                }
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig(maxTurns: 5);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Turn 1 response");
        result.Output.Should().Contain("Turn 2 response");
        result.Output.Should().Contain("[DONE]");
    }

    [Fact]
    public async Task ExecuteAsync_MultiTurn_ContinuationPromptAdded()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        var historyCaptures = new List<ChatHistory>();

        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var history = callInfo.Arg<ChatHistory>();
                historyCaptures.Add(new ChatHistory(history)); // Capture the state
                var ct = callInfo.Arg<CancellationToken>();

                if (historyCaptures.Count >= 2)
                {
                    return AsyncChunks(ct, "Final answer [DONE]");
                }
                else
                {
                    return AsyncChunks(ct, "Intermediate result");
                }
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig(maxTurns: 3);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        historyCaptures.Should().HaveCount(2);

        // Second turn should have continuation prompt added
        var secondHistory = historyCaptures[1];
        var lastMessage = secondHistory.Last();
        lastMessage.Content.Should().Contain("Continue");
    }

    [Fact]
    public async Task ExecuteAsync_AgentNameInResult_MatchesConfig()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Test response [DONE]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var agentName = "special-agent";
        var config = CreateConfig(name: agentName);

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be(agentName);
    }

    [Fact]
    public async Task ExecuteAsync_EventsRecorded_StartAndComplete()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return AsyncChunks(ct, "Response [DONE]");
            });

        var session = CreateSession(chatService);
        var executor = new MultiTurnExecutor();
        var config = CreateConfig();

        // Act
        var result = await executor.ExecuteAsync(config, session, teamContext: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();

        var eventTypes = result.Events.Select(e => e.EventType).ToList();
        eventTypes.Should().Contain(AgentEventType.Started);
        eventTypes.Should().Contain(AgentEventType.Completed);
    }
}
