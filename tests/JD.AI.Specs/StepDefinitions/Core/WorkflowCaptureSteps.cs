using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;
using Microsoft.SemanticKernel;
using NSubstitute;
using Reqnroll;
using WorkflowFramework;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class WorkflowCaptureSteps
{
    private readonly ScenarioContext _context;

    public WorkflowCaptureSteps(ScenarioContext context) => _context = context;

    [Given(@"a workflow execution capture")]
    public void GivenAWorkflowExecutionCapture()
    {
        _context.Set(new WorkflowExecutionCapture(), "Capture");
    }

    [When(@"a step ""(.*)"" starts")]
    public async Task WhenAStepStarts(string stepName)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        var mockContext = Substitute.For<IWorkflowContext>();
        var mockStep = Substitute.For<IStep>();
        mockStep.Name.Returns(stepName);
        await capture.OnStepStartedAsync(mockContext, mockStep);
    }

    [When(@"the step ""(.*)"" completes")]
    public async Task WhenTheStepCompletes(string stepName)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        var mockContext = Substitute.For<IWorkflowContext>();
        var mockStep = Substitute.For<IStep>();
        mockStep.Name.Returns(stepName);
        await capture.OnStepCompletedAsync(mockContext, mockStep);
    }

    [When(@"the step ""(.*)"" fails with error ""(.*)""")]
    public async Task WhenTheStepFailsWithError(string stepName, string error)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        var mockContext = Substitute.For<IWorkflowContext>();
        var mockStep = Substitute.For<IStep>();
        mockStep.Name.Returns(stepName);
        await capture.OnStepFailedAsync(mockContext, mockStep, new InvalidOperationException(error));
    }

    [Given(@"a workflow definition ""(.*)"" with steps:")]
    public void GivenAWorkflowDefinitionWithSteps(string name, Table table)
    {
        var steps = table.Rows.Select(r => new AgentStepDefinition
        {
            Name = r["name"],
            Kind = Enum.Parse<AgentStepKind>(r["kind"]),
            Target = r["target"],
        }).ToList();

        var definition = new AgentWorkflowDefinition
        {
            Name = name,
            Steps = steps,
        };
        _context.Set(definition, "WorkflowDefinition");
    }

    [Given(@"a workflow builder with a kernel")]
    public void GivenAWorkflowBuilderWithKernel()
    {
        var kernel = Kernel.CreateBuilder().Build();
        _context.Set(new AgentWorkflowBuilder(kernel), "WorkflowBuilder");
        _context.Set(kernel, "Kernel");
    }

    [When(@"I build the workflow")]
    public void WhenIBuildTheWorkflow()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var builder = new AgentWorkflowBuilder(kernel);
        var definition = _context.Get<AgentWorkflowDefinition>("WorkflowDefinition");
        var workflow = builder.Build(definition);
        _context.Set(workflow, "BuiltWorkflow");
    }

    [When(@"I create workflow data with prompt ""(.*)""")]
    public void WhenICreateWorkflowDataWithPrompt(string prompt)
    {
        var builder = _context.Get<AgentWorkflowBuilder>("WorkflowBuilder");
        var data = builder.CreateData(prompt);
        _context.Set(data, "WorkflowData");
    }

    [Then(@"the capture should have (\d+) events?")]
    public void ThenTheCaptureShouldHaveEvents(int count)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        capture.Events.Should().HaveCount(count);
    }

    [Then(@"the last event should be a ""(.*)"" event for step ""(.*)""")]
    public void ThenTheLastEventShouldBe(string kind, string stepName)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        var lastEvent = capture.Events.Last();
        lastEvent.Kind.ToString().Should().Be(kind);
        lastEvent.StepName.Should().Be(stepName);
    }

    [Then(@"the completed count should be (\d+)")]
    public void ThenTheCompletedCountShouldBe(int count)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        capture.CompletedCount.Should().Be(count);
    }

    [Then(@"the failed count should be (\d+)")]
    public void ThenTheFailedCountShouldBe(int count)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        capture.FailedCount.Should().Be(count);
    }

    [Then(@"the last event error should be ""(.*)""")]
    public void ThenTheLastEventErrorShouldBe(string expected)
    {
        var capture = _context.Get<WorkflowExecutionCapture>("Capture");
        var lastEvent = capture.Events.Last();
        lastEvent.Error.Should().Be(expected);
    }

    [Then(@"the workflow should be created successfully")]
    public void ThenTheWorkflowShouldBeCreatedSuccessfully()
    {
        var workflow = _context.Get<IWorkflow<AgentWorkflowData>>("BuiltWorkflow");
        workflow.Should().NotBeNull();
    }

    [Then(@"the workflow data prompt should be ""(.*)""")]
    public void ThenTheWorkflowDataPromptShouldBe(string expected)
    {
        var data = _context.Get<AgentWorkflowData>("WorkflowData");
        data.Prompt.Should().Be(expected);
    }
}
