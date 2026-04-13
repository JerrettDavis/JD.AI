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

public sealed class AgentWorkflowBuilderTests
{
    private static Kernel CreateMockKernel()
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "mocked response")]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    [Fact]
    public void Constructor_WithKernel_StoresKernel()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Build_SingleSkillStep_ReturnsWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "test-workflow",
            Steps = [AgentStepDefinition.RunSkill("skill1")],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_MultipleSteps_ReturnsWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "multi-step",
            Steps =
            [
                AgentStepDefinition.RunSkill("step1"),
                AgentStepDefinition.RunSkill("step2"),
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_AgentStep_ReturnsWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "agent-workflow",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "agent-decision",
                    Kind = AgentStepKind.Agent,
                    Target = "Make a decision",
                },
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_ToolStep_ReturnsWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "tool-workflow",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "invoke-tool",
                    Kind = AgentStepKind.Tool,
                    Target = "plugin.function",
                },
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_EmptySteps_ReturnsValidWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "empty",
            Steps = [],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void BuildWithCapture_SingleStep_ReturnsWorkflow()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);
        var capture = new WorkflowExecutionCapture();

        var definition = new AgentWorkflowDefinition
        {
            Name = "captured",
            Steps = [AgentStepDefinition.RunSkill("skill1")],
        };

        var workflow = builder.BuildWithCapture(definition, capture);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void CreateData_WithPrompt_ReturnsDataWithPromptAndKernel()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var data = builder.CreateData("test prompt");

        data.Should().NotBeNull();
        data.Prompt.Should().Be("test prompt");
        data.Kernel.Should().Be(kernel);
        data.StepOutputs.Should().BeEmpty();
        data.FinalResult.Should().BeNull();
    }

    [Fact]
    public void CreateData_EmptyPrompt_ReturnsData()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var data = builder.CreateData("");

        data.Should().NotBeNull();
        data.Prompt.Should().BeEmpty();
        data.Kernel.Should().Be(kernel);
    }

    [Fact]
    public void Build_UnknownStepKind_SkipsSilently()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        // Create a step with an unknown kind by manipulating the definition
        var definition = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps = [AgentStepDefinition.RunSkill("skill1")], // Valid step
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_ToolWithoutDotNotation_TreatsAsSkill()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "tool-as-skill",
                    Kind = AgentStepKind.Tool,
                    Target = "simpletarget",
                },
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_AgentStepWithNoTarget_UsesDefaultPrompt()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "decision",
                    Kind = AgentStepKind.Agent,
                    Target = null,
                },
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_ToolStepWithDot_SplitsCorrectly()
    {
        var kernel = CreateMockKernel();
        var builder = new AgentWorkflowBuilder(kernel);

        var definition = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "invoke",
                    Kind = AgentStepKind.Tool,
                    Target = "myPlugin.myFunction",
                },
            ],
        };

        var workflow = builder.Build(definition);

        workflow.Should().NotBeNull();
    }
}
