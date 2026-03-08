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
}
