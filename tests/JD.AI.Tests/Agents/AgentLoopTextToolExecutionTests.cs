using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Agents;

[Collection("AgentOutput")]
public sealed class AgentLoopTextToolExecutionTests
{
    [Fact]
    public async Task RunTurnStreamingAsync_TaggedToolCall_ExecutesRealToolAndReinvokesModel()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat);

        const string FirstResponse = """
            Looks like we're on Windows. Let me try again:
            <tool_call> {"name": "run_command", "arguments": {"command": "cd"}} </tool_call>
            <tool_response> Exit code: 0 --- stdout --- C:\Users\user\project </tool_response>
            We're running in C:\Users\user\project.
            """;
        const string FollowUpResponse = "We're running in C:\\Users\\jd.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamOnce(FirstResponse));

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, FollowUpResponse),
            });

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nC:\\Users\\jd";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnStreamingAsync("What folder are we running in?");
            result.Should().Be(FollowUpResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().ContainSingle().Which.Should().Be("cd");
        session.History.Last().Role.Should().Be(AuthorRole.Assistant);
        session.History.Last().Content.Should().Be(FollowUpResponse);
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunTurnStreamingAsync_ToolCapableModel_DoesNotExecuteTaggedTextToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            Let me check.
            <tool_call> {"name": "run_command", "arguments": {"command": "cd"}} </tool_call>
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamOnce(Response));

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nC:\\Users\\jd";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            _ = await loop.RunTurnStreamingAsync("What folder are we running in?");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().BeEmpty();
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RunTurnStreamingAsync_ToolCapableModel_BareJsonArray_ExecutesFallbackToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            [{"name":"run_command","arguments":{"command":"cd"}}]
            """;
        const string FollowUpResponse = "Current directory resolved.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamOnce(Response));

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, FollowUpResponse),
            });

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nC:\\Users\\jd";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        var output = new NullOutput();
        AgentOutput.Current = output;
        try
        {
            var result = await loop.RunTurnStreamingAsync("What folder are we running in?");
            result.Should().Be(FollowUpResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        output.ConfirmCalled.Should().BeTrue();
        executedCommands.Should().ContainSingle().Which.Should().Be("cd");
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunTurnStreamingAsync_JsonOutputMode_DoesNotExecuteBareJsonArrayFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            [{"name":"run_command","arguments":{"command":"cd"}}]
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamOnce(Response));

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nC:\\Users\\jd";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput { JsonOutputMode = true };
        try
        {
            _ = await loop.RunTurnStreamingAsync("What folder are we running in?");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().BeEmpty();
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
            .Should().BeFalse();
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamOnce(string text)
    {
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
        await Task.CompletedTask;
    }

    private sealed class NullOutput : IAgentOutput
    {
        public bool ConfirmCalled { get; private set; }
        public bool JsonOutputMode { get; init; }

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) { }
        public void EndStreaming() { }
        public void BeginTurn() { }
        public void EndTurn(TurnMetrics metrics) { }
        public bool IsJsonOutputMode => JsonOutputMode;
        public bool ConfirmToolCall(string toolName, string? args)
        {
            ConfirmCalled = true;
            return true;
        }
    }
}
