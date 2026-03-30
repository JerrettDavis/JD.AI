using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowOrchestratorTests
{
    private readonly IPromptIntentClassifier _classifier = Substitute.For<IPromptIntentClassifier>();
    private readonly IWorkflowMatcher _matcher = Substitute.For<IWorkflowMatcher>();
    private readonly IWorkflowBridge _bridge = Substitute.For<IWorkflowBridge>();
    private readonly WorkflowOrchestrator _orchestrator;

    public WorkflowOrchestratorTests()
    {
        _orchestrator = new WorkflowOrchestrator(_classifier, _matcher, _bridge);
    }

    [Fact]
    public async Task NullPrompt_ReturnsPassThrough()
    {
        var result = await _orchestrator.ProcessAsync(null!);

        result.Outcome.Should().Be(WorkflowOutcome.PassThrough);
        result.Intent.IsWorkflow.Should().BeFalse();
    }

    [Fact]
    public async Task EmptyPrompt_ReturnsPassThrough()
    {
        var result = await _orchestrator.ProcessAsync("");

        result.Outcome.Should().Be(WorkflowOutcome.PassThrough);
    }

    [Fact]
    public async Task WhitespacePrompt_ReturnsPassThrough()
    {
        var result = await _orchestrator.ProcessAsync("   ");

        result.Outcome.Should().Be(WorkflowOutcome.PassThrough);
    }

    [Fact]
    public async Task ConversationPrompt_ReturnsPassThrough()
    {
        _classifier.Classify("hello there")
            .Returns(new IntentClassification(false, 0.1, []));

        var result = await _orchestrator.ProcessAsync("hello there");

        result.Outcome.Should().Be(WorkflowOutcome.PassThrough);
        result.Intent.IsWorkflow.Should().BeFalse();
        result.Match.Should().BeNull();
        result.ExecutionResult.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowPrompt_EmptyCatalog_ReturnsPlanningNeeded()
    {
        _classifier.Classify("deploy the app then run tests")
            .Returns(new IntentClassification(true, 0.85, ["deploy", "then", "tests"]));
        _matcher.MatchAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns((WorkflowMatchResult?)null);

        var result = await _orchestrator.ProcessAsync("deploy the app then run tests");

        result.Outcome.Should().Be(WorkflowOutcome.PlanningNeeded);
        result.Intent.IsWorkflow.Should().BeTrue();
        result.PlanningPrompt.Should().Be("deploy the app then run tests");
        result.Match.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowPrompt_MatchFound_ExecutesAndReturnsResult()
    {
        var prompt = "deploy the app";
        var definition = new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy"],
            Steps = [AgentStepDefinition.RunSkill("deploy")],
        };
        var matchResult = new WorkflowMatchResult(definition, 1.0f, "exact");
        var bridgeResult = new WorkflowBridgeResult
        {
            Success = true,
            FinalOutput = "Deployed successfully",
            Duration = TimeSpan.FromMilliseconds(100),
        };

        _classifier.Classify(prompt)
            .Returns(new IntentClassification(true, 0.9, ["deploy"]));
        _matcher.MatchAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(matchResult);
        _bridge.ExecuteAsync(definition, Arg.Any<AgentWorkflowData>(), Arg.Any<CancellationToken>())
            .Returns(bridgeResult);

        var result = await _orchestrator.ProcessAsync(prompt);

        result.Outcome.Should().Be(WorkflowOutcome.Executed);
        result.Match.Should().Be(matchResult);
        result.ExecutionResult.Should().Be(bridgeResult);
        result.ExecutionResult!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowPrompt_ExecutionFails_ReturnsExecutionFailed()
    {
        var prompt = "deploy the app";
        var definition = new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy"],
            Steps = [AgentStepDefinition.RunSkill("deploy")],
        };
        var matchResult = new WorkflowMatchResult(definition, 1.0f, "exact");
        var bridgeResult = new WorkflowBridgeResult
        {
            Success = false,
            Errors = ["Step 'deploy' failed: connection refused"],
            Duration = TimeSpan.FromMilliseconds(50),
        };

        _classifier.Classify(prompt)
            .Returns(new IntentClassification(true, 0.9, ["deploy"]));
        _matcher.MatchAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(matchResult);
        _bridge.ExecuteAsync(definition, Arg.Any<AgentWorkflowData>(), Arg.Any<CancellationToken>())
            .Returns(bridgeResult);

        var result = await _orchestrator.ProcessAsync(prompt);

        result.Outcome.Should().Be(WorkflowOutcome.ExecutionFailed);
        result.Match.Should().Be(matchResult);
        result.ExecutionResult!.Success.Should().BeFalse();
        result.ExecutionResult.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task WorkflowPrompt_ExecutionThrows_ReturnsExecutionFailed()
    {
        var prompt = "deploy the app";
        var definition = new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy"],
            Steps = [AgentStepDefinition.RunSkill("deploy")],
        };
        var matchResult = new WorkflowMatchResult(definition, 1.0f, "exact");

        _classifier.Classify(prompt)
            .Returns(new IntentClassification(true, 0.9, ["deploy"]));
        _matcher.MatchAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(matchResult);
        _bridge.ExecuteAsync(definition, Arg.Any<AgentWorkflowData>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Kernel not set"));

        var result = await _orchestrator.ProcessAsync(prompt);

        result.Outcome.Should().Be(WorkflowOutcome.ExecutionFailed);
        result.Match.Should().Be(matchResult);
        result.ExecutionResult!.Success.Should().BeFalse();
        result.ExecutionResult.Errors.Should().Contain(e => e.Contains("Kernel not set"));
    }

    [Fact]
    public async Task ProcessAsync_PassesProvidedDataTobridge()
    {
        var prompt = "deploy the app";
        var definition = new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy"],
            Steps = [AgentStepDefinition.RunSkill("deploy")],
        };
        var matchResult = new WorkflowMatchResult(definition, 1.0f, "exact");
        var customData = new AgentWorkflowData { Prompt = prompt, FinalResult = "pre-set" };
        var bridgeResult = new WorkflowBridgeResult { Success = true };

        _classifier.Classify(prompt)
            .Returns(new IntentClassification(true, 0.9, ["deploy"]));
        _matcher.MatchAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(matchResult);
        _bridge.ExecuteAsync(definition, customData, Arg.Any<CancellationToken>())
            .Returns(bridgeResult);

        var result = await _orchestrator.ProcessAsync(prompt, customData);

        result.Outcome.Should().Be(WorkflowOutcome.Executed);
        await _bridge.Received(1).ExecuteAsync(definition, customData, Arg.Any<CancellationToken>());
    }
}
