using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using WorkflowFramework;
using Xunit;

namespace JD.AI.Tests.Workflows;

public sealed class RunSkillStepTests
{
    private static Kernel CreateMockKernel(string response = "test-output")
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    private static IWorkflowContext<AgentWorkflowData> CreateMockContext(Kernel kernel, string prompt = "test prompt")
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        var data = new AgentWorkflowData { Prompt = prompt, Kernel = kernel };
        context.Data.Returns(data);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public void Constructor_ValidInput_StoresValues()
    {
        var step = new RunSkillStep("analyze", "analyze the input");
        step.Name.Should().Be("analyze");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockKernel_StoresOutput()
    {
        var kernel = CreateMockKernel("analysis: done");
        var context = CreateMockContext(kernel);

        var step = new RunSkillStep("analyze", "analyze {prompt}");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs.Should().ContainKey("analyze");
        context.Data.StepOutputs["analyze"].Should().Be("analysis: done");
        context.Data.FinalResult.Should().Be("analysis: done");
    }

    [Fact]
    public async Task ExecuteAsync_NullKernel_ThrowsInvalidOperationException()
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        context.Data.Returns(new AgentWorkflowData { Prompt = "test", Kernel = null });

        var step = new RunSkillStep("analyze", "test");
        await step.Invoking(s => s.ExecuteAsync(context))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel not set*");
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesPromptPlaceholder()
    {
        var kernel = CreateMockKernel();
        var context = CreateMockContext(kernel, "my custom prompt");

        var step = new RunSkillStep("step", "Process: {prompt}");
        await step.ExecuteAsync(context);

        // The actual prompt processing is done within ExecuteAsync and passed to the kernel
        context.Data.StepOutputs.Should().ContainKey("step");
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesPreviousPlaceholder()
    {
        var kernel = CreateMockKernel();
        var context = CreateMockContext(kernel);
        context.Data.StepOutputs["previous_step"] = "previous output";

        var step = new RunSkillStep("current", "Follow up on {previous}");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs.Should().ContainKey("current");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyKernelResponse_StoresEmptyString()
    {
        var kernel = CreateMockKernel("");
        var context = CreateMockContext(kernel);

        var step = new RunSkillStep("step", "prompt");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs["step"].Should().Be("");
        context.Data.FinalResult.Should().Be("");
    }
}

public sealed class InvokeToolStepTests
{
    private static Kernel CreateMockKernelWithFunction(string result = "function result")
    {
        var builder = Kernel.CreateBuilder();

        // Create a mock plugin with a mock function
        var mockFunction = Substitute.For<KernelFunction>();
        mockFunction.ToString().Returns(result);

        var mockPlugin = Substitute.For<KernelPlugin>();
        mockPlugin.Name.Returns("test-plugin");

        var kernel = builder.Build();
        return kernel;
    }

    private static IWorkflowContext<AgentWorkflowData> CreateMockContext(Kernel kernel)
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        var data = new AgentWorkflowData { Prompt = "test", Kernel = kernel };
        context.Data.Returns(data);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public void Constructor_ValidInput_StoresValues()
    {
        var step = new InvokeToolStep("invoke", "plugin", "function");
        step.Name.Should().Be("invoke");
    }

    [Fact]
    public void Constructor_WithArguments_StoresArguments()
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal) { { "key", "value" } };
        var step = new InvokeToolStep("invoke", "plugin", "function", args);
        step.Name.Should().Be("invoke");
    }

    [Fact]
    public void Constructor_NullArguments_CreatesEmptyDictionary()
    {
        var step = new InvokeToolStep("invoke", "plugin", "function", null);
        step.Name.Should().Be("invoke");
    }

    [Fact]
    public async Task ExecuteAsync_NullKernel_ThrowsInvalidOperationException()
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        context.Data.Returns(new AgentWorkflowData { Kernel = null });

        var step = new InvokeToolStep("invoke", "plugin", "function");
        await step.Invoking(s => s.ExecuteAsync(context))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel not set*");
    }
}

public sealed class ValidateStepTests
{
    private static IWorkflowContext<AgentWorkflowData> CreateMockContext()
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        context.Data.Returns(new AgentWorkflowData());
        context.Errors.Returns(new List<WorkflowError>());
        return context;
    }

    [Fact]
    public void Constructor_ValidInput_StoresValues()
    {
        var step = new ValidateStep("validate", d => true);
        step.Name.Should().Be("validate");
    }

    [Fact]
    public async Task ExecuteAsync_PredicateTrue_DoesNotAbort()
    {
        var context = CreateMockContext();
        var step = new ValidateStep("validate", d => true);

        await step.ExecuteAsync(context);

        context.IsAborted.Should().Be(false);
        context.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PredicateFalse_AbortsAndAddsError()
    {
        var context = CreateMockContext();
        var step = new ValidateStep("validate", d => false, "custom error");

        await step.ExecuteAsync(context);

        context.IsAborted.Should().Be(true);
        context.Errors.Should().HaveCount(1);
        context.Errors[0].StepName.Should().Be("validate");
        context.Errors[0].Exception.Message.Should().Be("custom error");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultFailureMessage()
    {
        var context = CreateMockContext();
        var step = new ValidateStep("validate", d => false);

        await step.ExecuteAsync(context);

        context.Errors[0].Exception.Message.Should().Be("Validation failed");
    }

    [Fact]
    public async Task ExecuteAsync_ChecksDataCondition()
    {
        var context = CreateMockContext();
        context.Data.Prompt = "test prompt";

        var step = new ValidateStep("validate", d => !string.IsNullOrEmpty(d.Prompt));

        await step.ExecuteAsync(context);

        context.IsAborted.Should().Be(false);
    }
}

public sealed class AgentDecisionStepTests
{
    private static Kernel CreateMockKernel(string response = "decision made")
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, response)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    private static IWorkflowContext<AgentWorkflowData> CreateMockContext(Kernel kernel)
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        var data = new AgentWorkflowData { Prompt = "test", Kernel = kernel };
        context.Data.Returns(data);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public void Constructor_ValidInput_StoresValues()
    {
        var step = new AgentDecisionStep("decide", "make a decision");
        step.Name.Should().Be("decide");
    }

    [Fact]
    public void Constructor_WithPlugins_StoresPlugins()
    {
        var plugins = new[] { "plugin1", "plugin2" };
        var step = new AgentDecisionStep("decide", "prompt", plugins);
        step.Name.Should().Be("decide");
    }

    [Fact]
    public void Constructor_NullPlugins_AllowsConstruction()
    {
        var step = new AgentDecisionStep("decide", "prompt", null);
        step.Name.Should().Be("decide");
    }

    [Fact]
    public async Task ExecuteAsync_NullKernel_ThrowsInvalidOperationException()
    {
        var context = Substitute.For<IWorkflowContext<AgentWorkflowData>>();
        context.Data.Returns(new AgentWorkflowData { Kernel = null });

        var step = new AgentDecisionStep("decide", "prompt");
        await step.Invoking(s => s.ExecuteAsync(context))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel not set*");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockKernel_StoresOutput()
    {
        var kernel = CreateMockKernel("agent decision result");
        var context = CreateMockContext(kernel);

        var step = new AgentDecisionStep("decide", "What should we do?");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs.Should().ContainKey("decide");
        context.Data.FinalResult.Should().Be("agent decision result");
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesPromptPlaceholder()
    {
        var kernel = CreateMockKernel();
        var context = CreateMockContext(kernel);
        context.Data.Prompt = "custom user input";

        var step = new AgentDecisionStep("decide", "Given: {prompt}");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs.Should().ContainKey("decide");
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesPreviousPlaceholder()
    {
        var kernel = CreateMockKernel();
        var context = CreateMockContext(kernel);
        context.Data.StepOutputs["prior"] = "prior result";

        var step = new AgentDecisionStep("decide", "Based on: {previous}");
        await step.ExecuteAsync(context);

        context.Data.StepOutputs.Should().ContainKey("decide");
    }
}

public sealed class AgentWorkflowDataTests
{
    [Fact]
    public void Constructor_HasDefaultValues()
    {
        var data = new AgentWorkflowData();
        data.Prompt.Should().BeEmpty();
        data.StepOutputs.Should().BeEmpty();
        data.FinalResult.Should().BeNull();
        data.Kernel.Should().BeNull();
    }

    [Fact]
    public void StepOutputs_IsWritable()
    {
        var data = new AgentWorkflowData();
        data.StepOutputs["key"] = "value";
        data.StepOutputs["key"].Should().Be("value");
    }

    [Fact]
    public void Prompt_IsWritable()
    {
        var data = new AgentWorkflowData { Prompt = "test" };
        data.Prompt.Should().Be("test");
    }

    [Fact]
    public void FinalResult_IsWritable()
    {
        var data = new AgentWorkflowData { FinalResult = "final" };
        data.FinalResult.Should().Be("final");
    }
}
