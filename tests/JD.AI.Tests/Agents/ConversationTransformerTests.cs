using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Agents;

public sealed class ConversationTransformerTests
{
    private static readonly ProviderModelInfo TargetModel = new("target-model", "Target", "TestProvider");

    private static ChatHistory CreateSampleHistory()
    {
        var history = new ChatHistory();
        history.AddUserMessage("Hello, I need help with my project.");
        history.AddAssistantMessage("Sure! What do you need help with?");
        history.AddUserMessage("I want to refactor src/App.cs to use dependency injection.");
        history.AddAssistantMessage("Great idea. Let me look at the file.");
        return history;
    }

    [Fact]
    public async Task Preserve_ReturnsSameHistory()
    {
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, null, TargetModel, SwitchMode.Preserve);

        Assert.Same(history, result);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Fresh_ReturnsEmptyHistory()
    {
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, null, TargetModel, SwitchMode.Fresh);

        Assert.Empty(result);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Cancel_ThrowsOperationCanceledException()
    {
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            transformer.TransformAsync(history, null, TargetModel, SwitchMode.Cancel));
    }

    [Fact]
    public async Task Compact_FallsBackToPreserve_WhenNoKernel()
    {
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, null, TargetModel, SwitchMode.Compact);

        Assert.Same(history, result);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Transform_FallsBackToPreserve_WhenNoKernel()
    {
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, null, TargetModel, SwitchMode.Transform);

        Assert.Same(history, result);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Compact_ProducesShorterHistory_WithChatService()
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings?>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "Summary of conversation."),
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, kernel, TargetModel, SwitchMode.Compact);

        Assert.NotSame(history, result);
        Assert.Single(result);
        Assert.Contains("Summary of conversation", result[0].Content);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Transform_ProducesBriefing_WithChatService()
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings?>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "Briefing: refactor src/App.cs with DI."),
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, kernel, TargetModel, SwitchMode.Transform);

        Assert.NotSame(history, result);
        Assert.Single(result);
        Assert.NotNull(briefing);
        Assert.Contains("Briefing", briefing);
    }

    [Fact]
    public async Task Compact_FallsBackToPreserve_WhenKernelLacksChatService()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, kernel, TargetModel, SwitchMode.Compact);

        Assert.Same(history, result);
        Assert.Null(briefing);
    }

    [Fact]
    public async Task Transform_FallsBackToPreserve_WhenKernelLacksChatService()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var transformer = new ConversationTransformer();
        var history = CreateSampleHistory();

        var (result, briefing) = await transformer.TransformAsync(
            history, kernel, TargetModel, SwitchMode.Transform);

        Assert.Same(history, result);
        Assert.Null(briefing);
    }
}
