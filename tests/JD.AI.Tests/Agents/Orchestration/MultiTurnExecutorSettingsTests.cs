using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using NSubstitute;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class MultiTurnExecutorSettingsTests
{
    private static AgentSession SessionWithChatService(
        IChatCompletionService chatService,
        ProviderModelInfo model)
    {
        var registry = Substitute.For<IProviderRegistry>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();
        return new AgentSession(registry, kernel, model);
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> Chunks(
        params string[] texts)
    {
        foreach (var text in texts)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            await Task.Yield();
        }
    }

    [Fact]
    public async Task ExecuteAsync_MistralToolCapableModel_UsesMistralToolCallBehavior()
    {
        PromptExecutionSettings? capturedSettings = null;
        var svc = Substitute.For<IChatCompletionService>();
        svc.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks("[DONE]"));

        var model = new ProviderModelInfo(
            "mistral-large-pixtral-2411",
            "Mistral Large Pixtral 2411",
            "Mistral",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var session = SessionWithChatService(svc, model);
        var sut = new MultiTurnExecutor();
        var cfg = new SubagentConfig
        {
            Name = "mt",
            Prompt = "run",
            MaxTurns = 1,
        };

        var result = await sut.ExecuteAsync(cfg, session);

        result.Success.Should().BeTrue();
#pragma warning disable SKEXP0070
        var mistralSettings = capturedSettings as MistralAIPromptExecutionSettings;
        mistralSettings.Should().NotBeNull();
        mistralSettings!.ToolCallBehavior.Should().NotBeNull();
#pragma warning restore SKEXP0070
    }
}
