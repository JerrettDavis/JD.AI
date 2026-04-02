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
        output.StreamingChunks.Should().NotContain(chunk => chunk.Contains("<tool_call>", StringComparison.Ordinal));
        session.History.Any(m =>
            m.Role == AuthorRole.User &&
            m.Content is not null &&
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunTurnStreamingAsync_SplitTaggedToolCall_DoesNotStreamRawPayload()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstChunk = "Let me check.\n<tool";
        const string SecondChunk = "_call> {\"name\": \"run_command\", \"arguments\": {\"command\": \"cd\"}} </tool_call>";
        const string FollowUpResponse = "Current directory resolved.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamMany(FirstChunk, SecondChunk));

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, FollowUpResponse)]);

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

        executedCommands.Should().ContainSingle().Which.Should().Be("cd");
        output.StreamingChunks.Should().NotContain(chunk =>
            chunk.Contains("<tool", StringComparison.OrdinalIgnoreCase) ||
            chunk.Contains("\"command\": \"cd\"", StringComparison.Ordinal));
        output.StreamingChunks.Should().ContainSingle().Which.Should().Be(FollowUpResponse);
    }

    [Fact]
    public async Task RunTurnStreamingAsync_SplitFencedJsonToolCall_DoesNotStreamRawPayload()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstChunk = "Let me check.\n```json";
        const string SecondChunk = """
            
            {"name":"run_command","arguments":{"command":"cd"}}
            ```
            """;
        const string FollowUpResponse = "Current directory resolved.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamMany(FirstChunk, SecondChunk));

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, FollowUpResponse)]);

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

        executedCommands.Should().ContainSingle().Which.Should().Be("cd");
        output.StreamingChunks.Should().NotContain(chunk =>
            chunk.Contains("```json", StringComparison.OrdinalIgnoreCase) ||
            chunk.Contains("\"command\":\"cd\"", StringComparison.Ordinal));
        output.StreamingChunks.Should().ContainSingle().Which.Should().Be(FollowUpResponse);
    }

    [Fact]
    public async Task RunTurnStreamingAsync_SplitUnlabeledFencedJsonToolCall_DoesNotStreamRawPayload()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstChunk = "Let me check.\n```";
        const string SecondChunk = """
            
            {"name":"run_command","arguments":{"command":"cd"}}
            ```
            """;
        const string FollowUpResponse = "Current directory resolved.";

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(StreamMany(FirstChunk, SecondChunk));

        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, FollowUpResponse)]);

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

        executedCommands.Should().ContainSingle().Which.Should().Be("cd");
        output.StreamingChunks.Should().NotContain(chunk =>
            chunk.Contains("```", StringComparison.Ordinal) ||
            chunk.Contains("\"command\":\"cd\"", StringComparison.Ordinal));
        output.StreamingChunks.Should().ContainSingle().Which.Should().Be(FollowUpResponse);
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
    public async Task RunTurnAsync_ToolCallingRetry_ExecutesTaggedToolCallFallback()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "test-model",
            "Test",
            "TestProvider",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string RetryResponse = """
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
                _ => throw new InvalidOperationException("Each `tool_use` block must have a corresponding `tool_result` block"),
                _ => [new ChatMessageContent(AuthorRole.Assistant, RetryResponse)],
                _ => [new ChatMessageContent(AuthorRole.Assistant, FollowUpResponse)]);

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
    public async Task RunTurnAsync_ToolCapableModel_ProseWrappedFencedJson_ExecutesFallbackToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstResponse = """
            I'll run ls for you.
            ```json
            {"name":"run_command","arguments":{"command":"ls"}}
            ```
            This will list the directory.
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
            m.Content.Contains("Tool result for run_command", StringComparison.Ordinal))
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
    public async Task RunTurnStreamingAsync_ToolCapableModel_StandaloneFencedBashBlock_ExecutesFallbackToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            ```bash
            ls
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
    public async Task RunTurnStreamingAsync_ProseWrappedFencedBashBlock_DoesNotExecuteFallbackToolCall()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            Run this locally:
            ```bash
            ls
            ```
            It will list the directory.
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
            result.Should().Be(Response);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        output.ConfirmCalled.Should().BeFalse();
        executedCommands.Should().BeEmpty();
        session.History.Should().Contain(m =>
            m.Role == AuthorRole.Assistant &&
            m.Content == Response);
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
        _ = session.TryRegisterToolCallForCurrentTurn(
            "run_command",
            ToolConfirmationFilter.BuildArgsFingerprint(
                "run_command",
                new KernelArguments { ["command"] = "ls" }));
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
            result.Should().NotBeNull();
            GetResultProperty<string>(result!, "AssistantMessage")
                .Should().Contain("Skipped duplicate tool call in current turn.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().BeEmpty();
        output.ToolCallRenderCount.Should().Be(0);
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_UsesCanonicalToolNameForSafetyAndDenyRules()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "shell.run_command", "arguments": {"command": "pwd"}} </tool_call>
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
                    return "blocked";
                },
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            CurrentTurn = new JD.AI.Core.Sessions.TurnRecord
            {
                SessionId = "sess",
                Role = "assistant",
            },
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        session.ToolPermissionProfile.AddDenied("run_command", projectScope: false);
        var output = new NullOutput();
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = output;
        try
        {
            var result = await InvokeTextToolCallAsync(loop, Response);
            result.Should().NotBeNull();
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        executedCommands.Should().BeEmpty();
        session.CurrentTurn.ToolCalls.Should().ContainSingle(call =>
            call.ToolName == "run_command" &&
            call.Status == "denied" &&
            call.Result == "Tool blocked: blocked by explicit deny rule.");
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_RedactsRecordedArguments_AndSharesConfirmOnceAcrossChannels()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "file.write_file", "arguments": {"path": "b.txt", "content": "secret-body"}} </tool_call>
            """;

        var writes = new List<(string Path, string Content)>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IChatCompletionService>());
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) =>
                {
                    writes.Add((path, content));
                    return "ok";
                },
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            CurrentTurn = new JD.AI.Core.Sessions.TurnRecord
            {
                SessionId = "sess",
                Role = "assistant",
            },
            PermissionMode = PermissionMode.Normal,
            WorkflowDeclinedThisTurn = true,
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var output = new CountingOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var loop = new AgentLoop(session);

        await filter.OnAutoFunctionInvocationAsync(
            CreateStructuredContext(kernel, "write_file", new KernelArguments
            {
                ["path"] = "a.txt",
                ["content"] = "alpha",
            }),
            ctx =>
            {
                ctx.Result = new FunctionResult(ctx.Function, "ok");
                return Task.CompletedTask;
            });

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = output;
        try
        {
            var result = await InvokeTextToolCallAsync(loop, Response);
            result.Should().NotBeNull();
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        output.ConfirmCallCount.Should().Be(1);
        writes.Should().ContainSingle().Which.Should().Be(("b.txt", "secret-body"));
        session.CurrentTurn.ToolCalls.Should().HaveCount(2);
        session.CurrentTurn.ToolCalls.Last().Arguments.Should().Contain("content=[REDACTED]");
        session.CurrentTurn.ToolCalls.Last().Arguments.Should().NotContain("secret-body");
        output.RenderedArgs.Should().ContainInOrder("path=a.txt [5 chars]", "path=b.txt [11 chars]");
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_DifferentWriteContents_AreNotCollapsedByRedaction()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstResponse = """
            <tool_call> {"name": "write_file", "arguments": {"path": "same.txt", "content": "alpha"}} </tool_call>
            """;
        const string SecondResponse = """
            <tool_call> {"name": "write_file", "arguments": {"path": "same.txt", "content": "beta"}} </tool_call>
            """;

        var writes = new List<(string Path, string Content)>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IChatCompletionService>());
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) =>
                {
                    writes.Add((path, content));
                    return "ok";
                },
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            CurrentTurn = new JD.AI.Core.Sessions.TurnRecord
            {
                SessionId = "sess",
                Role = "assistant",
            },
            PermissionMode = PermissionMode.BypassAll,
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);
        var output = new CountingOutput();

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = output;
        try
        {
            await InvokeTextToolCallAsync(loop, FirstResponse);
            await InvokeTextToolCallAsync(loop, SecondResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        writes.Should().HaveCount(2);
        writes.Should().ContainInOrder(
            ("same.txt", "alpha"),
            ("same.txt", "beta"));
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_CapturesWorkflowStepsForSuccessfulTextTool()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "file.write_file", "arguments": {"path": "draft.txt", "content": "hello world"}} </tool_call>
            """;

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IChatCompletionService>());
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) => "ok",
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
            PermissionMode = PermissionMode.BypassAll,
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await InvokeTextToolCallAsync(loop, Response);
            result.Should().NotBeNull();
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("write_file", "path=draft.txt [11 chars]"));
    }

    [Fact]
    public async Task TryExecuteTextToolCallAsync_NamedWorkflow_DoesNotCaptureWorkflowStep()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "file.write_file", "arguments": {"path": "draft.txt", "content": "hello world"}} </tool_call>
            """;

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IChatCompletionService>());
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) => "ok",
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "named-workflow",
            PermissionMode = PermissionMode.BypassAll,
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await InvokeTextToolCallAsync(loop, Response);
            result.Should().NotBeNull();
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.CapturedWorkflowSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task RunTurnAsync_TextToolFallback_StoresSanitizedAssistantHistory()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string FirstResponse = """
            <tool_call> {"name": "file.write_file", "arguments": {"path": "draft.txt", "content": "secret-body"}} </tool_call>
            """;
        const string FollowUpResponse = "Draft saved.";

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

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) => "ok",
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            PermissionMode = PermissionMode.BypassAll,
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be(FollowUpResponse);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Should().Contain(m =>
            m.Role == AuthorRole.Assistant &&
            m.Content == "[Text tool call]: file.write_file(path=draft.txt [11 chars])");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content =>
                content.Contains("<tool_call>", StringComparison.Ordinal) ||
                content.Contains("secret-body", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_TextToolFallbackDenied_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "file.write_file", "arguments": {"path": "draft.txt", "content": "secret-body"}} </tool_call>
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("file", [
            KernelFunctionFactory.CreateFromMethod(
                (string path, string content) => "ok",
                "write_file",
                "Write file")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        session.ToolPermissionProfile.AddDenied("write_file", projectScope: false);
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: file.write_file(path=draft.txt [11 chars]) — Tool blocked: blocked by explicit deny rule.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: file.write_file(path=draft.txt [11 chars]) — Tool blocked: blocked by explicit deny rule.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content =>
                content.Contains("<tool_call>", StringComparison.Ordinal) ||
                content.Contains("secret-body", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_TextToolFallbackUnavailable_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "missing_tool", "arguments": {"path": "draft.txt"}} </tool_call>
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: missing_tool(path=draft.txt) — Tool unavailable.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: missing_tool(path=draft.txt) — Tool unavailable.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content => content.Contains("<tool_call>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_TextToolFallbackUnavailable_RedactsSensitivePersistedArgs()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "missing_tool", "arguments": {"path": "draft.txt", "token": "abc123", "body": "secret"}} </tool_call>
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: missing_tool(path=draft.txt, token=[REDACTED], body=[REDACTED]) — Tool unavailable.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: missing_tool(path=draft.txt, token=[REDACTED], body=[REDACTED]) — Tool unavailable.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content =>
                content.Contains("abc123", StringComparison.Ordinal) ||
                content.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_MalformedTaggedToolPayload_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            <tool_call> {"name": "", "arguments": {"path": "draft.txt"}} </tool_call>
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content => content.Contains("<tool_call>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_FencedJsonUnavailable_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            Let me try that.
            ```json
            {"name":"missing_tool","arguments":{"path":"draft.txt"}}
            ```
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: missing_tool(path=draft.txt) — Tool unavailable.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: missing_tool(path=draft.txt) — Tool unavailable.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content => content.Contains("```json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunTurnAsync_MalformedFencedJsonToolPayload_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            Let me try that.
            ```json
            {"name":"","arguments":{"path":"draft.txt"}}
            ```
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content => content.Contains("```json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunTurnAsync_ParseInvalidFencedJsonToolPayload_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            ```json
            {"name":"missing_tool","arguments":{"path":"draft.txt"
            ```
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content => content.Contains("```json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunTurnAsync_FencedJsonWithMissingArgumentsKey_ReturnsSanitizedAssistantMessage()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            ```json
            {"name":"file.write_file","params":{"path":"draft.txt","content":"secret-body"}}
            ```
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("save it");
            result.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be("[Text tool call]: tool — Malformed tool payload.");
        session.History.Select(m => m.Content ?? string.Empty)
            .Should().NotContain(content =>
                content.Contains("```json", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("secret-body", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_ProseWrappedJsonExample_PreservesAssistantResponse()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Claude Code",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        const string Response = """
            Here is an example payload shape:
            ```json
            {"name":"example","arguments":"documented fields"}
            ```
            Use it as reference only.
            """;

        var chatService = Substitute.For<IChatCompletionService>();
        chatService
            .GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, Response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap,
        };
        var loop = new AgentLoop(session);

        var previousOutput = AgentOutput.Current;
        AgentOutput.Current = new NullOutput();
        try
        {
            var result = await loop.RunTurnAsync("show me an example");
            result.Should().Be(Response);
        }
        finally
        {
            AgentOutput.Current = previousOutput;
        }

        session.History.Last().Content.Should().Be(Response);
    }

    private static AutoFunctionInvocationContext CreateStructuredContext(
        Kernel kernel,
        string functionName,
        KernelArguments arguments)
    {
        var function = KernelFunctionFactory.CreateFromMethod(() => string.Empty, functionName, functionName);
        return new AutoFunctionInvocationContext(
            kernel,
            function,
            new FunctionResult(function, string.Empty),
            new ChatHistory(),
            new ChatMessageContent(AuthorRole.Assistant, "tool call"))
        {
            Arguments = arguments,
        };
    }

    private static async Task<object?> InvokeTextToolCallAsync(AgentLoop loop, string response)
    {
        var method = typeof(AgentLoop).GetMethod(
            "TryExecuteTextToolCallAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var taskObj = method!.Invoke(loop, [response, CancellationToken.None]) as Task;
        taskObj.Should().NotBeNull();
        await taskObj!;
        return taskObj!.GetType().GetProperty("Result")!.GetValue(taskObj);
    }

    private static T? GetResultProperty<T>(object result, string propertyName)
    {
        var property = result.GetType().GetProperty(propertyName);
        property.Should().NotBeNull();
        return (T?)property!.GetValue(result);
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamOnce(string text)
    {
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamMany(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk);
        }

        await Task.CompletedTask;
    }

    private sealed class NullOutput : IAgentOutput
    {
        public bool ConfirmCalled { get; private set; }
        public bool JsonOutputMode { get; init; }
        public int ToolCallRenderCount { get; private set; }
        public List<string> StreamingChunks { get; } = [];

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) => StreamingChunks.Add(text);
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

    private sealed class CountingOutput : IAgentOutput
    {
        public int ConfirmCallCount { get; private set; }
        public List<string?> RenderedArgs { get; } = [];

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) { }
        public void EndStreaming() { }
        public void RenderToolCall(string toolName, string? args, string result) => RenderedArgs.Add(args);
        public void BeginTurn() { }
        public void EndTurn(TurnMetrics metrics) { }

        public bool ConfirmToolCall(string toolName, string? args)
        {
            ConfirmCallCount++;
            return true;
        }
    }
}
