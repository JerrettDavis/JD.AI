using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Tasks;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Tests.Agents;

public sealed class SubagentRunnerTests
{
    // ── GetLoadoutName ─────────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentType.Explore, WellKnownLoadouts.Research)]
    [InlineData(SubagentType.Task, WellKnownLoadouts.Minimal)]
    [InlineData(SubagentType.Plan, WellKnownLoadouts.Developer)]
    [InlineData(SubagentType.Review, WellKnownLoadouts.Developer)]
    [InlineData(SubagentType.General, WellKnownLoadouts.Full)]
    public void GetLoadoutName_ReturnsExpectedLoadout(SubagentType type, string expected)
    {
        SubagentRunner.GetLoadoutName(type).Should().Be(expected);
    }

    [Fact]
    public void GetLoadoutName_UnknownValue_ReturnsMinimal()
    {
        SubagentRunner.GetLoadoutName((SubagentType)999).Should().Be(WellKnownLoadouts.Minimal);
    }

    [Fact]
    public void GetLoadoutName_AllDefinedValues_ReturnNonEmpty()
    {
        foreach (var type in Enum.GetValues<SubagentType>())
        {
            SubagentRunner.GetLoadoutName(type).Should().NotBeNullOrWhiteSpace(
                $"GetLoadoutName({type}) should return a non-empty loadout name");
        }
    }

    [Fact]
    public void GetLoadoutName_ExploreAndTask_ReturnDifferentLoadouts()
    {
        var explore = SubagentRunner.GetLoadoutName(SubagentType.Explore);
        var task = SubagentRunner.GetLoadoutName(SubagentType.Task);
        explore.Should().NotBe(task);
    }

    [Fact]
    public void GetLoadoutName_PlanAndReview_ReturnSameLoadout()
    {
        var plan = SubagentRunner.GetLoadoutName(SubagentType.Plan);
        var review = SubagentRunner.GetLoadoutName(SubagentType.Review);
        plan.Should().Be(review);
    }

    // ── RunAsync Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GivenValidPrompt_WhenModelStreamsResponse_ReturnsFullConcatenatedText()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => StreamChunks(ci.Arg<CancellationToken>(), "Hello", " World"));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        // Act
        var result = await runner.RunAsync(SubagentType.General, "test prompt");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public async Task RunAsync_GivenEmptyModelResponse_ReturnsNoResponsePlaceholder()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => StreamChunks(ci.Arg<CancellationToken>(), "", null));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        // Act
        var result = await runner.RunAsync(SubagentType.Task, "empty test");

        // Assert
        result.Should().Be("(no response)");
    }

    [Fact]
    public async Task RunAsync_GivenCancelledToken_ReturnsCancelledMessage()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => SlowStream(ci.Arg<CancellationToken>()));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        var result = await runner.RunAsync(SubagentType.Explore, "cancellation test", cts.Token);

        // Assert
        result.Should().Contain("cancelled", because: "the result should indicate cancellation");
    }

    [Fact]
    public async Task RunAsync_GivenChatServiceThrows_ReturnsErrorMessage()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        var exceptionMessage = "API quota exceeded";

        async IAsyncEnumerable<StreamingChatMessageContent> ThrowingEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException(exceptionMessage);
#pragma warning disable CS0162 // Unreachable code
            yield break;
#pragma warning restore CS0162
        }

        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ThrowingEnumerable(ci.Arg<CancellationToken>()));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        // Act
        var result = await runner.RunAsync(SubagentType.Review, "error test");

        // Assert
        result.Should().Contain("error", because: "error messages should be indicated");
        result.Should().Contain(exceptionMessage, because: "the actual error message should be included");
    }

    // ── RunParallelAsync Tests ─────────────────────────────────────────

    [Fact]
    public async Task RunParallelAsync_GivenMultipleTasks_WhenAllSucceed_ReturnsDictionaryWithAllLabels()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ci => StreamChunks(ci.Arg<CancellationToken>(), "Response A"),
                ci => StreamChunks(ci.Arg<CancellationToken>(), "Response B"),
                ci => StreamChunks(ci.Arg<CancellationToken>(), "Response C"));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        var tasks = new[]
        {
            (SubagentType.Explore, "a", "prompt a"),
            (SubagentType.Task, "b", "prompt b"),
            (SubagentType.Plan, "c", "prompt c"),
        };

        // Act
        var results = await runner.RunParallelAsync(tasks);

        // Assert
        results.Should().HaveCount(3);
        results.Should().ContainKey("a").WhoseValue.Should().Be("Response A");
        results.Should().ContainKey("b").WhoseValue.Should().Be("Response B");
        results.Should().ContainKey("c").WhoseValue.Should().Be("Response C");
    }

    // ── SpawnAsync Tests ───────────────────────────────────────────────

    [Fact]
    public async Task SpawnAsync_GivenCall_ReturnsNonNullTask()
    {
        // Arrange
        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => StreamChunks(ci.Arg<CancellationToken>(), "spawn result"));

        var session = CreateSessionWithChatService(chatService);
        var runner = new SubagentRunner(session);

        // Act
        var task = await runner.SpawnAsync(SubagentType.General, "spawn test");

        // Assert
        task.Should().NotBeNull();
        task.Id.Should().NotBeNullOrWhiteSpace();
        task.Type.Should().Be(AgentTaskType.LocalAgent);
        task.Description.Should().NotBeNullOrWhiteSpace();
    }

    // ── Helper Methods ─────────────────────────────────────────────────

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamChunks(
        [EnumeratorCancellation] CancellationToken ct = default,
        params string?[] chunks)
    {
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk);
            }
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> SlowStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, "x");
            await Task.Delay(100, ct);
        }
    }

    private static AgentSession CreateSessionWithChatService(IChatCompletionService chatService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(
            Substitute.For<IProviderRegistry>(),
            kernel,
            new ProviderModelInfo("test-model", "Test Model", "TestProvider"));

        return session;
    }
}
