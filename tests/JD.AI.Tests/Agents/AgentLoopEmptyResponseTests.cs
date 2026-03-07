using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Agents;

[Collection("AgentOutput")]
[Feature("Agent Loop Empty Response Rendering")]
public sealed class AgentLoopEmptyResponseTests : TinyBddXunitBase
{
    public AgentLoopEmptyResponseTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Empty streaming response renders fallback text to output"), Fact]
    public async Task EmptyStreamingResponse_RendersFallback()
    {
        string? result = null;
        var spyOutput = new SpyAgentOutput();

        await Given("a mock chat service that returns empty streaming chunks", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var model = new ProviderModelInfo("test-model", "Test", "TestProvider");

                var chatService = Substitute.For<IChatCompletionService>();
                chatService
                    .GetStreamingChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings>(),
                        Arg.Any<Kernel>(),
                        Arg.Any<CancellationToken>())
                    .Returns(EmptyAsyncEnumerable());

                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(chatService);
                var testKernel = builder.Build();

                return (registry, testKernel, model);
            })
            .When("RunTurnStreamingAsync completes",
                new Func<(IProviderRegistry registry, Kernel testKernel, ProviderModelInfo model),
                    Task<(IProviderRegistry, Kernel, ProviderModelInfo)>>(async ctx =>
            {
                var previousOutput = AgentOutput.Current;
                AgentOutput.Current = spyOutput;
                try
                {
                    var session = new AgentSession(ctx.registry, ctx.testKernel, ctx.model);
                    var loop = new AgentLoop(session);
                    result = await loop.RunTurnStreamingAsync("hello");
                }
                finally
                {
                    AgentOutput.Current = previousOutput;
                }
                return ctx;
            }))
            .Then("the returned string is '(no response)'", _ =>
            {
                result.Should().Be("(no response)");
                return true;
            })
            .And("BeginStreaming was called on the output", _ =>
            {
                spyOutput.BeginStreamingCalled.Should().BeTrue();
                return true;
            })
            .And("WriteStreamingChunk was called with '(no response)'", _ =>
            {
                spyOutput.StreamingChunks.Should().ContainSingle()
                    .Which.Should().Be("(no response)");
                return true;
            })
            .And("EndStreaming was called on the output", _ =>
            {
                spyOutput.EndStreamingCalled.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> EmptyAsyncEnumerable()
    {
        // Yield a chunk with null/empty content to simulate empty LLM response
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, null);
        await Task.CompletedTask;
    }

    private sealed class SpyAgentOutput : IAgentOutput
    {
        public bool BeginStreamingCalled { get; private set; }
        public bool EndStreamingCalled { get; private set; }
        public List<string> StreamingChunks { get; } = [];

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() => BeginStreamingCalled = true;
        public void WriteStreamingChunk(string text) => StreamingChunks.Add(text);
        public void EndStreaming() => EndStreamingCalled = true;
        public void BeginTurn() { }
        public void EndTurn(TurnMetrics metrics) { }
    }
}
