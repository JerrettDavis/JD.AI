using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Agents;

[Collection("AgentOutput")]
[Feature("Agent Loop Tool Calling Error Recovery")]
public sealed class AgentLoopToolCallingErrorTests : TinyBddXunitBase
{
    public AgentLoopToolCallingErrorTests(ITestOutputHelper output) : base(output) { }

    [Scenario("IsToolCallingError detects tool_use_id in exception message"), Fact]
    public async Task IsToolCallingError_DetectsToolUseIdMessage()
    {
        bool? result = null;

        await Given("an exception containing 'tool_use_id' in its message", () =>
            {
                return new HttpRequestException(
                    "unexpected `tool_use_id` found in `tool_result` blocks: toolu_vrtx_015zMuQrj5tK3J2uC2Sz3kQS");
            })
            .When("IsToolCallingError is called", ex =>
            {
                result = AgentLoop.IsToolCallingError(ex);
                return true;
            })
            .Then("it returns true", _ =>
            {
                result.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolCallingError detects tool_result in nested exception"), Fact]
    public async Task IsToolCallingError_DetectsToolResultInInnerException()
    {
        bool? result = null;

        await Given("an exception with 'tool_result' in an inner exception", () =>
            {
                var inner = new InvalidOperationException(
                    "Each `tool_result` block must have a corresponding `tool_use` block");
                return new HttpRequestException("HTTP 400", inner);
            })
            .When("IsToolCallingError is called", ex =>
            {
                result = AgentLoop.IsToolCallingError(ex);
                return true;
            })
            .Then("it returns true", _ =>
            {
                result.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolCallingError returns false for unrelated errors"), Fact]
    public async Task IsToolCallingError_ReturnsFalseForOtherErrors()
    {
        bool? result = null;

        await Given("an exception with an unrelated message", () =>
            {
                return new HttpRequestException("Connection refused");
            })
            .When("IsToolCallingError is called", ex =>
            {
                result = AgentLoop.IsToolCallingError(ex);
                return true;
            })
            .Then("it returns false", _ =>
            {
                result.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Streaming tool calling error retries without tools"), Fact]
    public async Task RunTurnStreamingAsync_ToolCallingError_RetriesWithoutTools()
    {
        string? result = null;
        var spyOutput = new SpyAgentOutput();

        await Given("a chat service that throws tool_use_id error on streaming but succeeds on non-streaming", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var model = new ProviderModelInfo("test-model", "Test", "TestProvider");

                var chatService = Substitute.For<IChatCompletionService>();

                var toolError = new HttpRequestException(
                    "messages.0.content.1: unexpected `tool_use_id` found in `tool_result` blocks: toolu_vrtx_015z");
                var fallbackContent = new ChatMessageContent(AuthorRole.Assistant, "Fallback response without tools");

                // Streaming call throws tool calling error
                chatService
                    .GetStreamingChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings>(),
                        Arg.Any<Kernel>(),
                        Arg.Any<CancellationToken>())
                    .Throws(toolError);

                // Non-streaming retry succeeds (mock plural form — the actual interface method)
                chatService
                    .GetChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings?>(),
                        Arg.Any<Kernel?>(),
                        Arg.Any<CancellationToken>())
                    .Returns(new List<ChatMessageContent> { fallbackContent });

                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(chatService);
                var testKernel = builder.Build();

                return (registry, testKernel, model);
            })
            .When("RunTurnStreamingAsync is called",
                new Func<(IProviderRegistry registry, Kernel testKernel, ProviderModelInfo model),
                    Task<(IProviderRegistry, Kernel, ProviderModelInfo)>>(async ctx =>
            {
                var previousOutput = AgentOutput.Current;
                AgentOutput.Current = spyOutput;
                try
                {
                    var session = new AgentSession(ctx.registry, ctx.testKernel, ctx.model);
                    var loop = new AgentLoop(session);
                    result = await loop.RunTurnStreamingAsync("what tools do you have?");
                }
                finally
                {
                    AgentOutput.Current = previousOutput;
                }
                return ctx;
            }))
            .Then("the response is the fallback text", _ =>
            {
                result.Should().Be("Fallback response without tools");
                return true;
            })
            .And("the output contains the fallback response", _ =>
            {
                spyOutput.StreamingChunks.Should().Contain("Fallback response without tools");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Streaming tool calling error cleans up history"), Fact]
    public async Task RunTurnStreamingAsync_ToolCallingError_CleansUpHistory()
    {
        AgentSession? session = null;
        var spyOutput = new SpyAgentOutput();

        await Given("a chat service that throws tool_use_id error on streaming but succeeds on non-streaming", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var model = new ProviderModelInfo("test-model", "Test", "TestProvider");

                var chatService = Substitute.For<IChatCompletionService>();

                var toolError = new HttpRequestException(
                    "unexpected `tool_use_id` found in `tool_result` blocks");
                var recoveredContent = new ChatMessageContent(AuthorRole.Assistant, "Recovered response");

                // Streaming call throws tool calling error
                chatService
                    .GetStreamingChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings>(),
                        Arg.Any<Kernel>(),
                        Arg.Any<CancellationToken>())
                    .Throws(toolError);

                // Non-streaming retry succeeds (mock plural form — the actual interface method)
                chatService
                    .GetChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings?>(),
                        Arg.Any<Kernel?>(),
                        Arg.Any<CancellationToken>())
                    .Returns(new List<ChatMessageContent> { recoveredContent });

                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(chatService);
                var testKernel = builder.Build();

                session = new AgentSession(registry, testKernel, model);

                return session;
            })
            .When("RunTurnStreamingAsync completes after recovery",
                new Func<AgentSession, Task<AgentSession>>(async ctx =>
            {
                var previousOutput = AgentOutput.Current;
                AgentOutput.Current = spyOutput;
                try
                {
                    var loop = new AgentLoop(ctx);
                    await loop.RunTurnStreamingAsync("test message");
                }
                finally
                {
                    AgentOutput.Current = previousOutput;
                }
                return ctx;
            }))
            .Then("history contains only user message and assistant response", _ =>
            {
                // Should be: user message + assistant response = 2 messages
                session!.History.Should().HaveCount(2);
                session.History[0].Role.Should().Be(AuthorRole.User);
                session.History[1].Role.Should().Be(AuthorRole.Assistant);
                session.History[1].Content.Should().Be("Recovered response");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolsRejectedError detects 400 via HttpRequestException.StatusCode when tools were enabled"), Fact]
    public async Task IsToolsRejectedError_Detects400ViaStatusCode()
    {
        bool? result = null;

        await Given("an HttpRequestException with StatusCode=BadRequest and tools were enabled", () =>
            {
                // Simulate SK's OpenAI connector which sets StatusCode on HttpRequestException
                return new HttpRequestException(
                    "Service request failed.", inner: null,
                    statusCode: System.Net.HttpStatusCode.BadRequest);
            })
            .When("IsToolsRejectedError is called with toolsWereEnabled=true", ex =>
            {
                result = AgentLoop.IsToolsRejectedError(ex, toolsWereEnabled: true);
                return true;
            })
            .Then("it returns true", _ =>
            {
                result.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolsRejectedError detects 400 via anchored message pattern when tools were enabled"), Fact]
    public async Task IsToolsRejectedError_Detects400ViaAnchoredMessage()
    {
        bool? result = null;

        await Given("an HttpRequestException with '400 (Bad Request)' in its message and tools were enabled", () =>
            {
                return new HttpRequestException("Status: 400 (Bad Request)");
            })
            .When("IsToolsRejectedError is called with toolsWereEnabled=true", ex =>
            {
                result = AgentLoop.IsToolsRejectedError(ex, toolsWereEnabled: true);
                return true;
            })
            .Then("it returns true", _ =>
            {
                result.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolsRejectedError does not fire on bare '400' appearing in unrelated message text"), Fact]
    public async Task IsToolsRejectedError_DoesNotFire_On400InUnrelatedMessage()
    {
        bool? result = null;

        await Given("an exception whose message contains '400' but not as an HTTP status", () =>
            {
                // e.g. rate-limit or payload-size message — should NOT be treated as BadRequest
                return new HttpRequestException("Rate limit reached. Retry after 400 seconds.");
            })
            .When("IsToolsRejectedError is called with toolsWereEnabled=true", ex =>
            {
                result = AgentLoop.IsToolsRejectedError(ex, toolsWereEnabled: true);
                return true;
            })
            .Then("it returns false to avoid silently swallowing non-HTTP-status 400 text", _ =>
            {
                result.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolsRejectedError returns false when tools were not enabled"), Fact]
    public async Task IsToolsRejectedError_ReturnsFalseWhenToolsNotEnabled()
    {
        bool? result = null;

        await Given("a 400 Bad Request exception but tools were NOT enabled", () =>
            {
                return new HttpRequestException(
                    "Service request failed.", inner: null,
                    statusCode: System.Net.HttpStatusCode.BadRequest);
            })
            .When("IsToolsRejectedError is called with toolsWereEnabled=false", ex =>
            {
                result = AgentLoop.IsToolsRejectedError(ex, toolsWereEnabled: false);
                return true;
            })
            .Then("it returns false to avoid swallowing unrelated 400 errors", _ =>
            {
                result.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsToolsRejectedError returns false for non-400 errors even with tools enabled"), Fact]
    public async Task IsToolsRejectedError_ReturnsFalseForNon400Errors()
    {
        bool? result = null;

        await Given("a 500 Internal Server Error with tools enabled", () =>
            {
                return new HttpRequestException(
                    "Service request failed.", inner: null,
                    statusCode: System.Net.HttpStatusCode.InternalServerError);
            })
            .When("IsToolsRejectedError is called with toolsWereEnabled=true", ex =>
            {
                result = AgentLoop.IsToolsRejectedError(ex, toolsWereEnabled: true);
                return true;
            })
            .Then("it returns false because it's not a Bad Request", _ =>
            {
                result.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Non-streaming unresolved function call is recovered via retry without tools"), Fact]
    public async Task RunTurnAsync_UnpairedFunctionCallInHistory_RetriesWithoutTools()
    {
        string? result = null;

        await Given("a chat service that returns empty content while appending an unpaired function call", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var model = new ProviderModelInfo("test-model", "Test", "TestProvider");

                var chatService = Substitute.For<IChatCompletionService>();
                var recovered = new ChatMessageContent(AuthorRole.Assistant, "Recovered response without tools");

                chatService
                    .GetChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings?>(),
                        Arg.Any<Kernel?>(),
                        Arg.Any<CancellationToken>())
                    .Returns(
                        callInfo =>
                        {
                            var history = callInfo.ArgAt<ChatHistory>(0);
                            history.Add(new ChatMessageContent(AuthorRole.Assistant, [
                                new FunctionCallContent(
                                    functionName: "toolDiscovery",
                                    pluginName: "toolDiscovery",
                                    id: "toolu_01J5o3ra5PqVyditJwJdywu5",
                                    arguments: new KernelArguments()),
                            ]));

                            return new List<ChatMessageContent>
                            {
                                new(AuthorRole.Assistant, string.Empty),
                            };
                        },
                        _ => new List<ChatMessageContent> { recovered });

                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(chatService);
                var testKernel = builder.Build();

                return (registry, testKernel, model);
            })
            .When("RunTurnAsync is called",
                new Func<(IProviderRegistry registry, Kernel testKernel, ProviderModelInfo model),
                    Task<(IProviderRegistry, Kernel, ProviderModelInfo)>>(async ctx =>
            {
                var session = new AgentSession(ctx.registry, ctx.testKernel, ctx.model);
                var loop = new AgentLoop(session);
                result = await loop.RunTurnAsync("What tools do we have available?");

                session.History.Should().HaveCount(2);
                session.History[0].Role.Should().Be(AuthorRole.User);
                session.History[1].Role.Should().Be(AuthorRole.Assistant);
                session.History.SelectMany(m => m.Items ?? [])
                    .Should().NotContain(i => i is FunctionCallContent);

                return ctx;
            }))
            .Then("the response comes from the retry path", _ =>
            {
                result.Should().Be("Recovered response without tools");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Streaming unresolved function call is recovered via retry without tools"), Fact]
    public async Task RunTurnStreamingAsync_UnpairedFunctionCallInHistory_RetriesWithoutTools()
    {
        string? result = null;
        var spyOutput = new SpyAgentOutput();

        await Given("a streaming chat service that appends an unpaired function call with no text", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var model = new ProviderModelInfo("test-model", "Test", "TestProvider");

                var chatService = Substitute.For<IChatCompletionService>();
                var recovered = new ChatMessageContent(AuthorRole.Assistant, "Recovered stream response");

                chatService
                    .GetStreamingChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings>(),
                        Arg.Any<Kernel>(),
                        Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var history = callInfo.ArgAt<ChatHistory>(0);
                        return StreamWithUnpairedFunctionCall(history);
                    });

                chatService
                    .GetChatMessageContentsAsync(
                        Arg.Any<ChatHistory>(),
                        Arg.Any<PromptExecutionSettings?>(),
                        Arg.Any<Kernel?>(),
                        Arg.Any<CancellationToken>())
                    .Returns(new List<ChatMessageContent> { recovered });

                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(chatService);
                var testKernel = builder.Build();

                return (registry, testKernel, model);
            })
            .When("RunTurnStreamingAsync is called",
                new Func<(IProviderRegistry registry, Kernel testKernel, ProviderModelInfo model),
                    Task<(IProviderRegistry, Kernel, ProviderModelInfo)>>(async ctx =>
            {
                var previousOutput = AgentOutput.Current;
                AgentOutput.Current = spyOutput;
                try
                {
                    var session = new AgentSession(ctx.registry, ctx.testKernel, ctx.model);
                    var loop = new AgentLoop(session);
                    result = await loop.RunTurnStreamingAsync("What tools do we have available?");

                    session.History.Should().HaveCount(2);
                    session.History[0].Role.Should().Be(AuthorRole.User);
                    session.History[1].Role.Should().Be(AuthorRole.Assistant);
                    session.History.SelectMany(m => m.Items ?? [])
                        .Should().NotContain(i => i is FunctionCallContent);
                }
                finally
                {
                    AgentOutput.Current = previousOutput;
                }
                return ctx;
            }))
            .Then("the streamed output comes from the retry path", _ =>
            {
                result.Should().Be("Recovered stream response");
                spyOutput.StreamingChunks.Should().Contain("Recovered stream response");
                return true;
            })
            .AssertPassed();

        static async IAsyncEnumerable<StreamingChatMessageContent> StreamWithUnpairedFunctionCall(ChatHistory history)
        {
            history.Add(new ChatMessageContent(AuthorRole.Assistant, [
                new FunctionCallContent(
                    functionName: "toolDiscovery",
                    pluginName: "toolDiscovery",
                    id: "toolu_01Phna5tsdXcDzuLXiuXaMpx",
                    arguments: new KernelArguments()),
            ]));

            await Task.Yield();
            yield break;
        }
    }

    private sealed class SpyAgentOutput : IAgentOutput
    {
        public bool BeginStreamingCalled { get; private set; }
        public bool EndStreamingCalled { get; private set; }
        public List<string> StreamingChunks { get; } = [];
        public List<string> Errors { get; } = [];

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) => Errors.Add(message);
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
