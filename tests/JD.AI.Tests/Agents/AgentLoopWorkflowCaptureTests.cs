using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Tests.Agents;

public sealed class AgentLoopWorkflowCaptureTests
{
    [Fact]
    public async Task RunTurnAsync_RecordingWorkflow_SavesAndClearsCapture()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "done")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
        };
        session.CapturedWorkflowSteps.Add(("shell-run_command", "command=dotnet --info"));

        var saveCalled = false;
        var savedStepCount = 0;
        session.SaveCapturedWorkflowAsync = (name, steps, _) =>
        {
            saveCalled = true;
            savedStepCount = steps.Count;
            return Task.FromResult($"{name}-saved");
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnAsync("hello");

        saveCalled.Should().BeTrue();
        savedStepCount.Should().Be(1);
        session.CapturedWorkflowSteps.Should().BeEmpty();
        session.ActiveWorkflowName.Should().BeNull();
    }

    [Fact]
    public async Task RunTurnAsync_EmptyRecordingWorkflow_ClearsWithoutSaving()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "done")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
        };

        var saveCalled = false;
        session.SaveCapturedWorkflowAsync = (_, _, _) =>
        {
            saveCalled = true;
            return Task.FromResult("unexpected");
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnAsync("hello");

        saveCalled.Should().BeFalse();
        session.CapturedWorkflowSteps.Should().BeEmpty();
        session.ActiveWorkflowName.Should().BeNull();
    }

    [Fact]
    public async Task RunTurnAsync_WorkflowSaveFailure_PreservesCaptureForRetry()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "done")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
        };
        session.CapturedWorkflowSteps.Add(("shell-run_command", "command=[REDACTED]"));
        session.SaveCapturedWorkflowAsync = (_, _, _) =>
            throw new InvalidOperationException("disk full");

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnAsync("hello");

        session.ActiveWorkflowName.Should().Be("recording");
        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("shell-run_command", "command=[REDACTED]"));
    }

    [Fact]
    public async Task RunTurnAsync_ToolCallingRetry_SavesAndClearsCapturedWorkflow()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("Each `tool_use` block must have a corresponding `tool_result` block"),
                _ => [new ChatMessageContent(AuthorRole.Assistant, "done")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
        };
        session.CapturedWorkflowSteps.Add(("shell-run_command", "command=[REDACTED]"));

        var saveCalled = false;
        session.SaveCapturedWorkflowAsync = (name, steps, _) =>
        {
            saveCalled = true;
            steps.Should().ContainSingle().Which.Should().Be(("shell-run_command", "command=[REDACTED]"));
            return Task.FromResult($"{name}-saved");
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnAsync("hello");

        saveCalled.Should().BeTrue();
        session.CapturedWorkflowSteps.Should().BeEmpty();
        session.ActiveWorkflowName.Should().BeNull();
    }

    [Fact]
    public async Task RunTurnStreamingAsync_ToolCallingRetry_SavesAndClearsCapturedWorkflow()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        var chat = Substitute.For<IChatCompletionService>();
        var retryResponse = Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, "done"));
        chat.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Each `tool_use` block must have a corresponding `tool_result` block"));
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "done")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
        };
        session.CapturedWorkflowSteps.Add(("shell-run_command", "command=[REDACTED]"));

        var saveCalled = false;
        session.SaveCapturedWorkflowAsync = (name, steps, _) =>
        {
            saveCalled = true;
            steps.Should().ContainSingle().Which.Should().Be(("shell-run_command", "command=[REDACTED]"));
            return Task.FromResult($"{name}-saved");
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnStreamingAsync("hello");

        saveCalled.Should().BeTrue();
        session.CapturedWorkflowSteps.Should().BeEmpty();
        session.ActiveWorkflowName.Should().BeNull();
    }

    [Fact]
    public async Task RunTurnAsync_ToolCallingRetryFollowUpFailure_PreservesCapturedWorkflow()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        const string RetryResponse = """
            <tool_call>
            {"name":"run_command","arguments":{"command":"cd"}}
            </tool_call>
            """;

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("Each `tool_use` block must have a corresponding `tool_result` block"),
                _ => [new ChatMessageContent(AuthorRole.Assistant, RetryResponse)],
                _ => throw new InvalidOperationException("follow-up failed"));

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) => $"ran {command}",
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
            PermissionMode = PermissionMode.BypassAll,
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnAsync("hello");

        session.ActiveWorkflowName.Should().Be("recording");
        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("run_command", "command=[REDACTED]"));
    }

    [Fact]
    public async Task RunTurnStreamingAsync_ToolCallingRetryFollowUpFailure_PreservesCapturedWorkflow()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        const string RetryResponse = """
            <tool_call>
            {"name":"run_command","arguments":{"command":"cd"}}
            </tool_call>
            """;

        var chat = Substitute.For<IChatCompletionService>();
        chat.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Each `tool_use` block must have a corresponding `tool_result` block"));
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => [new ChatMessageContent(AuthorRole.Assistant, RetryResponse)],
                _ => throw new InvalidOperationException("follow-up failed"));

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("shell", [
            KernelFunctionFactory.CreateFromMethod(
                (string command) => $"ran {command}",
                "run_command",
                "Execute command")
        ]);

        var session = new AgentSession(registry, kernel, model)
        {
            ActiveWorkflowName = "recording",
            PermissionMode = PermissionMode.BypassAll,
        };

        var loop = new AgentLoop(session);
        _ = await loop.RunTurnStreamingAsync("hello");

        session.ActiveWorkflowName.Should().Be("recording");
        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("run_command", "command=[REDACTED]"));
    }
}
