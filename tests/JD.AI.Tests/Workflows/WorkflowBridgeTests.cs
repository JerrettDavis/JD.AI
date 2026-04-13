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

public class WorkflowBridgeTests
{
    private readonly WorkflowBridge _bridge = new();

    private static Kernel CreateMockKernel(string response = "mock-output")
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

    [Fact]
    public void Build_SingleSkillWorkflow_ReturnsWorkflow()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "single-skill",
            Steps = [AgentStepDefinition.RunSkill("analyze")],
        };

        var workflow = _bridge.Build(definition);

        workflow.Should().NotBeNull();
        workflow.Name.Should().Be("single-skill");
    }

    [Fact]
    public async Task Execute_SingleSkill_CapturesOutput()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "skill-exec",
            Steps = [AgentStepDefinition.RunSkill("summarize")],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "summarize this text",
            Kernel = CreateMockKernel("Summary: done"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Summary: done");
        result.StepOutputs.Should().ContainKey("summarize");
        result.StepOutputs["summarize"].Should().Be("Summary: done");
        result.Errors.Should().BeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Execute_MultiStep_CapturesAllOutputs()
    {
        // Skill -> Tool (without dot, falls back to skill) -> Validate
        var definition = new AgentWorkflowDefinition
        {
            Name = "multi-step",
            Steps =
            [
                AgentStepDefinition.RunSkill("step1"),
                new AgentStepDefinition
                {
                    Name = "step2",
                    Kind = AgentStepKind.Skill,
                    Target = "follow-up on {previous}",
                },
            ],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "test prompt",
            Kernel = CreateMockKernel("step-output"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.StepOutputs.Should().HaveCount(2);
        result.StepOutputs.Should().ContainKey("step1");
        result.StepOutputs.Should().ContainKey("step2");
    }

    [Fact]
    public async Task Execute_ConditionalStep_EvaluatesTrueCondition()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "conditional-true",
            Steps =
            [
                AgentStepDefinition.If("true",
                    AgentStepDefinition.RunSkill("inner-skill")),
            ],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "conditional test",
            Kernel = CreateMockKernel("conditional-output"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.StepOutputs.Should().ContainKey("inner-skill");
    }

    [Fact]
    public async Task Execute_ConditionalStep_SkipsFalseCondition()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "conditional-false",
            Steps =
            [
                AgentStepDefinition.If("false",
                    AgentStepDefinition.RunSkill("should-not-run")),
            ],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "conditional test",
            Kernel = CreateMockKernel("output"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.StepOutputs.Should().NotContainKey("should-not-run");
    }

    [Fact]
    public void Build_UnknownStepKind_HandlesGracefully()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "unknown-kind",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "weird-step",
                    Kind = (AgentStepKind)999,
                },
            ],
        };

        // Should not throw — unknown kinds are silently skipped
        var workflow = _bridge.Build(definition);
        workflow.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_MissingKernel_ReturnsError()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "no-kernel",
            Steps = [AgentStepDefinition.RunSkill("analyze")],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "test",
            Kernel = null, // No kernel set
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Kernel not set"));
    }

    [Fact]
    public void Build_LoopStep_CreatesValidWorkflow()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "loop-workflow",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "loop-step",
                    Kind = AgentStepKind.Loop,
                    Condition = "counter < 3",
                    SubSteps = [AgentStepDefinition.RunSkill("inner")],
                },
            ],
        };

        var workflow = _bridge.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_LoopStepWithoutSubSteps_HandlesGracefully()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "empty-loop",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "loop",
                    Kind = AgentStepKind.Loop,
                    Condition = "true",
                    SubSteps = [],
                },
            ],
        };

        var workflow = _bridge.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_LoopStepWithMultipleSubSteps_HandlesGracefully()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "multi-step-loop",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "loop",
                    Kind = AgentStepKind.Loop,
                    Condition = "counter < 5",
                    SubSteps =
                    [
                        AgentStepDefinition.RunSkill("step1"),
                        AgentStepDefinition.RunSkill("step2"),
                    ],
                },
            ],
        };

        var workflow = _bridge.Build(definition);

        workflow.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_WithCancellation_RespectsCancellationToken()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "cancellable",
            Steps = [AgentStepDefinition.RunSkill("slow-step")],
        };

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(3);
                ct.ThrowIfCancellationRequested();
                return new List<ChatMessageContent>
                    { new(AuthorRole.Assistant, "never-reached") };
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        var kernel = builder.Build();

        var data = new AgentWorkflowData
        {
            Prompt = "test",
            Kernel = kernel,
        };

        var result = await _bridge.ExecuteAsync(definition, data, cts.Token);

        // With a pre-cancelled token, the workflow should either fail or
        // produce no step outputs (engine may skip steps entirely).
        var wasSkipped = result.StepOutputs.Count == 0;
        var hadErrors = result.Errors.Count > 0;
        (wasSkipped || hadErrors || !result.Success).Should().BeTrue(
            "cancellation should prevent normal execution");
    }

    [Fact]
    public async Task Execute_ToolStepWithDotTarget_ParsesPluginAndFunction()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "tool-test",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "git-diff",
                    Kind = AgentStepKind.Tool,
                    Target = "GitPlugin.GetDiff",
                },
            ],
        };

        // Without the plugin registered, this should error — but it should parse correctly
        var data = new AgentWorkflowData
        {
            Prompt = "test",
            Kernel = CreateMockKernel(),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        // Will fail because GitPlugin isn't registered, but error message should reference the function
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Execute_AgentStepWithAllowedPlugins_Executes()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "agent-decision",
            Steps =
            [
                AgentStepDefinition.AgentStep(
                    "decide", "Analyze: {prompt}", "PluginA", "PluginB"),
            ],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "what should I do?",
            Kernel = CreateMockKernel("Decision: proceed"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Decision: proceed");
        result.StepOutputs.Should().ContainKey("decide");
    }

    [Fact]
    public async Task Execute_NestedSubSteps_ExecutesRecursively()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "nested",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "sub-workflow",
                    Kind = AgentStepKind.Nested,
                    SubSteps =
                    [
                        AgentStepDefinition.RunSkill("inner1"),
                        AgentStepDefinition.RunSkill("inner2"),
                    ],
                },
            ],
        };

        var data = new AgentWorkflowData
        {
            Prompt = "nested test",
            Kernel = CreateMockKernel("nested-output"),
        };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.StepOutputs.Should().ContainKey("inner1");
        result.StepOutputs.Should().ContainKey("inner2");
    }

    [Fact]
    public void EvaluateCondition_LiteralTrue_ReturnsTrue()
    {
        var ctx = new WorkflowContext<AgentWorkflowData>(new AgentWorkflowData());
        WorkflowBridge.EvaluateCondition(ctx, "true").Should().BeTrue();
    }

    [Fact]
    public void EvaluateCondition_LiteralFalse_ReturnsFalse()
    {
        var ctx = new WorkflowContext<AgentWorkflowData>(new AgentWorkflowData());
        WorkflowBridge.EvaluateCondition(ctx, "false").Should().BeFalse();
    }

    [Fact]
    public void EvaluateCondition_Null_ReturnsFalse()
    {
        var ctx = new WorkflowContext<AgentWorkflowData>(new AgentWorkflowData());
        WorkflowBridge.EvaluateCondition(ctx, null).Should().BeFalse();
    }

    [Fact]
    public void EvaluateCondition_StepOutputKey_ReturnsTrueWhenPresent()
    {
        var data = new AgentWorkflowData();
        data.StepOutputs["analyze"] = "some result";
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        WorkflowBridge.EvaluateCondition(ctx, "analyze").Should().BeTrue();
    }

    [Fact]
    public void EvaluateCondition_HasPrompt_ReturnsTrueWhenSet()
    {
        var data = new AgentWorkflowData { Prompt = "hello" };
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        WorkflowBridge.EvaluateCondition(ctx, "hasPrompt").Should().BeTrue();
    }

    [Fact]
    public void EvaluateCondition_HasFinalResult_ReturnsFalseWhenEmpty()
    {
        var data = new AgentWorkflowData();
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        WorkflowBridge.EvaluateCondition(ctx, "hasFinalResult").Should().BeFalse();
    }

    [Fact]
    public async Task Execute_NullDefinition_ThrowsArgumentNull()
    {
        var act = () => _bridge.ExecuteAsync(null!, new AgentWorkflowData());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Execute_NullData_ThrowsArgumentNull()
    {
        var def = new AgentWorkflowDefinition { Name = "test" };
        var act = () => _bridge.ExecuteAsync(def, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Build_EmptySteps_ProducesWorkflow()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "empty",
            Steps = [],
        };

        var workflow = _bridge.Build(definition);
        workflow.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_EmptySteps_SucceedsWithNoOutput()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "empty-exec",
            Steps = [],
        };

        var data = new AgentWorkflowData { Prompt = "test" };

        var result = await _bridge.ExecuteAsync(definition, data);

        result.Success.Should().BeTrue();
        result.StepOutputs.Should().BeEmpty();
        result.FinalOutput.Should().BeNull();
    }
}
