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
    public async Task RunTurnStreamingAsync_ToolCapableModel_ExecutesTaggedToolCallFallback()
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
            <tool_response> Exit code: 0 --- stdout --- C:\Users\user\project </tool_response>
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
    public async Task RunTurnStreamingAsync_ToolCapableClaudeModel_ExecutesTaggedToolUseFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            I'll run ls for you.
            <tool_use>
            {"name":"run_command","arguments":{"command":"cd"}}
            </tool_use>
            <tool_response>{"output":""}</tool_response>
            The directory appears to be empty.
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
            var result = await loop.RunTurnStreamingAsync("Run ls");
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
    public async Task RunTurnStreamingAsync_ToolCapableNonClaudeModel_ExecutesTaggedToolUseFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            I'll run ls for you.
            <tool_use>
            {"name":"run_command","arguments":{"command":"cd"}}
            </tool_use>
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
        var output = new NullOutput();
        AgentOutput.Current = output;
        try
        {
            _ = await loop.RunTurnStreamingAsync("Run ls");
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
    public async Task RunTurnAsync_ToolCapableModel_ExecutesTaggedToolCallFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstResponse = """
            I'll run ls for you.
            <tool_call>
            {"name":"run_command","arguments":{"command":"cd"}}
            </tool_call>
            """;
        const string FollowUpResponse = "Current directory resolved.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new List<ChatMessageContent> { new(AuthorRole.Assistant, FirstResponse) },
                new List<ChatMessageContent> { new(AuthorRole.Assistant, FollowUpResponse) });

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
            var result = await loop.RunTurnAsync("Run ls");
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
    public async Task RunTurnAsync_ToolCapableModel_ShellAlias_ExecutesRunCommandFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstResponse = """
            I'll run ls for you.
            <tool_call>
            {"name":"shell","arguments":{"command":"ls"}}
            </tool_call>
            <tool_response>
            </tool_response>
            """;
        const string FollowUpResponse = "Directory listing completed.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new List<ChatMessageContent> { new(AuthorRole.Assistant, FirstResponse) },
                new List<ChatMessageContent> { new(AuthorRole.Assistant, FollowUpResponse) });

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nREADME.md";
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
            var result = await loop.RunTurnAsync("run ls");
            result.Should().Be(FollowUpResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        output.ConfirmCalled.Should().BeTrue();
        output.ToolCallRenderCount.Should().Be(1);
        executedCommands.Should().ContainSingle().Which.Should().Be("ls");
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for shell", StringComparison.Ordinal))
            .Should().BeTrue();
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

    [Fact]
    public async Task RunTurnStreamingAsync_ToolCapableModel_FencedBashBlock_ExecutesFallbackToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            I'll check now.
            ```bash
            ls
            ```
            ```code
            JDAI.md
            ```
            """;
        const string FollowUpResponse = "Current directory contains multiple entries.";

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
                    return "Exit code: 0\n--- stdout ---\nfile-a\nfile-b";
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
            var result = await loop.RunTurnStreamingAsync("Run ls");
            result.Should().Be(FollowUpResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        output.ConfirmCalled.Should().BeTrue();
        executedCommands.Should().ContainSingle().Which.Should().Be("ls");
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for bash", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_SkipsDuplicateFingerprintInSameTurn()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            I'll run ls for you.
            <tool_use>
            {"name":"run_command","arguments":{"command":"ls"}}
            </tool_use>
            """;

        var executedCommands = new List<string>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IChatCompletionService>());
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) =>
                {
                    executedCommands.Add(command);
                    return "Exit code: 0\n--- stdout ---\nREADME.md";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model);
        var output = new NullOutput();
        session.ResetTurnState();
        _ = session.TryRegisterToolCallForCurrentTurn("run_command", "command=ls");
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = output;
        try
        {
            var method = typeof(AgentLoop).GetMethod(
                "TryExecuteTextToolCallAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var taskObj = method!.Invoke(loop, [Response, CancellationToken.None]) as Task;
            taskObj.Should().NotBeNull();
            await taskObj!;
            var result = taskObj!.GetType().GetProperty("Result")!.GetValue(taskObj);
            result.Should().BeNull();
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().BeEmpty();
        output.ToolCallRenderCount.Should().Be(0);
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
        public int ToolCallRenderCount { get; private set; }

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) { }
        public void EndStreaming() { }
        public void RenderToolCall(string toolName, string? args, string result) =>
            ToolCallRenderCount++;
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
