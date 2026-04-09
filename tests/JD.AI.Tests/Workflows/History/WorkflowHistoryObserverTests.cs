using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.History;
using NSubstitute;

namespace JD.AI.Tests.Workflows.History;

public sealed class WorkflowHistoryObserverTests
{
    private readonly IWorkflowHistoryGraphStore _store = Substitute.For<IWorkflowHistoryGraphStore>();
    private readonly WorkflowHistoryObserver _observer;

    public WorkflowHistoryObserverTests()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkflowHistoryGraph()));
        _observer = new WorkflowHistoryObserver(_store);
    }

    [Fact]
    public async Task IngestRunAsync_SimpleThreeStepWorkflow_CreatesThreeNodes()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                AgentStepDefinition.RunSkill("Step1"),
                AgentStepDefinition.InvokeTool("Step2"),
                AgentStepDefinition.Nested("Step3"),
            },
        };

        var result = new WorkflowBridgeResult { Success = true };

        await _observer.IngestRunAsync(definition, result);

        await _store.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        await _store.Received(1).SaveAsync(Arg.Any<WorkflowHistoryGraph>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestRunAsync_ThreeStepWorkflow_CreatesSequentialEdges()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                AgentStepDefinition.RunSkill("Step1"),
                AgentStepDefinition.InvokeTool("Step2"),
                AgentStepDefinition.Nested("Step3"),
            },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        // Should have 2 sequential edges (Step1->Step2, Step2->Step3)
        graph.Edges.Where(e => e.Kind == EdgeKind.Sequential).Should().HaveCount(2);
    }

    [Fact]
    public async Task IngestRunAsync_NestedSteps_CreatesSubStepEdges()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                new AgentStepDefinition
                {
                    Name = "ParentStep",
                    Kind = AgentStepKind.Loop,
                    Condition = "done",
                    SubSteps = new[]
                    {
                        AgentStepDefinition.RunSkill("SubStep1"),
                        AgentStepDefinition.InvokeTool("SubStep2"),
                    },
                },
            },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        // Should have 2 SubStep edges (ParentStep->SubStep1, ParentStep->SubStep2)
        graph.Edges.Where(e => e.Kind == EdgeKind.SubStep).Should().HaveCount(2);
    }

    [Fact]
    public async Task IngestRunAsync_SuccessfulRun_IncrementsSuccessCount()
    {
        var step = AgentStepDefinition.RunSkill("TestStep");
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[] { step },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        var fp = StepFingerprint.Compute(step);
        var node = graph.GetNode(fp);

        node!.SuccessCount.Should().Be(1);
        node.FailureCount.Should().Be(0);
        node.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task IngestRunAsync_FailedRun_IncrementsFailureCount()
    {
        var step = AgentStepDefinition.RunSkill("TestStep");
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[] { step },
        };

        var result = new WorkflowBridgeResult { Success = false };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        var fp = StepFingerprint.Compute(step);
        var node = graph.GetNode(fp);

        node!.SuccessCount.Should().Be(0);
        node.FailureCount.Should().Be(1);
        node.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task IngestRunAsync_MultipleRuns_CumulateCounters()
    {
        var step = AgentStepDefinition.RunSkill("TestStep");
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[] { step },
        };

        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        // First run: success
        await _observer.IngestRunAsync(definition, new WorkflowBridgeResult { Success = true });

        // Second run: failure
        await _observer.IngestRunAsync(definition, new WorkflowBridgeResult { Success = false });

        // Third run: success
        await _observer.IngestRunAsync(definition, new WorkflowBridgeResult { Success = true });

        var fp = StepFingerprint.Compute(step);
        var node = graph.GetNode(fp);

        node!.ExecutionCount.Should().Be(3);
        node.SuccessCount.Should().Be(2);
        node.FailureCount.Should().Be(1);
    }

    [Fact]
    public async Task IngestRunAsync_DeeplyNestedSteps_FlattensCorrectly()
    {
        // Structure: Parent -> Child1, Child2
        //           Child1 -> GrandChild1
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                new AgentStepDefinition
                {
                    Name = "Parent",
                    Kind = AgentStepKind.Loop,
                    Condition = "done",
                    SubSteps = new[]
                    {
                        new AgentStepDefinition
                        {
                            Name = "Child1",
                            Kind = AgentStepKind.Conditional,
                            Condition = "test",
                            SubSteps = new[]
                            {
                                AgentStepDefinition.RunSkill("GrandChild1"),
                            },
                        },
                        AgentStepDefinition.InvokeTool("Child2"),
                    },
                },
            },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        // Should have 4 nodes: Parent, Child1, Child2, GrandChild1
        graph.NodeCount.Should().Be(4);

        // Nodes should be created for all steps
        var parentFp = StepFingerprint.Compute(definition.Steps[0]);
        var child1Fp = StepFingerprint.Compute(definition.Steps[0].SubSteps[0]);
        var child2Fp = StepFingerprint.Compute(definition.Steps[0].SubSteps[1]);
        var grandchildFp = StepFingerprint.Compute(definition.Steps[0].SubSteps[0].SubSteps[0]);

        graph.GetNode(parentFp).Should().NotBeNull();
        graph.GetNode(child1Fp).Should().NotBeNull();
        graph.GetNode(child2Fp).Should().NotBeNull();
        graph.GetNode(grandchildFp).Should().NotBeNull();
    }

    [Fact]
    public async Task IngestRunAsync_AddsWorkflowNameToNode()
    {
        var step = AgentStepDefinition.RunSkill("TestStep");
        var definition = new AgentWorkflowDefinition
        {
            Name = "MyWorkflow",
            Steps = new[] { step },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        var fp = StepFingerprint.Compute(step);
        var node = graph.GetNode(fp);

        node!.WorkflowNames.Should().Contain("MyWorkflow");
    }

    [Fact]
    public async Task IngestRunAsync_MultipleWorkflows_AggregateWorkflowNames()
    {
        var step = AgentStepDefinition.RunSkill("SharedStep");

        var definition1 = new AgentWorkflowDefinition
        {
            Name = "Workflow1",
            Steps = new[] { step },
        };

        var definition2 = new AgentWorkflowDefinition
        {
            Name = "Workflow2",
            Steps = new[] { step },
        };

        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition1, new WorkflowBridgeResult { Success = true });
        await _observer.IngestRunAsync(definition2, new WorkflowBridgeResult { Success = true });

        var fp = StepFingerprint.Compute(step);
        var node = graph.GetNode(fp);

        node!.WorkflowNames.Should().Contain("Workflow1", "Workflow2");
    }

    [Fact]
    public async Task IngestRunAsync_NullDefinition_Throws()
    {
        var result = new WorkflowBridgeResult { Success = true };

        await FluentActions.Invoking(() => _observer.IngestRunAsync(null!, result))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("definition");
    }

    [Fact]
    public async Task IngestRunAsync_NullResult_Throws()
    {
        var definition = new AgentWorkflowDefinition { Name = "Test" };

        await FluentActions.Invoking(() => _observer.IngestRunAsync(definition, null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public async Task IngestRunAsync_RecordsTransitionNames()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                AgentStepDefinition.RunSkill("Step1"),
                AgentStepDefinition.InvokeTool("Step2"),
            },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        var edges = graph.Edges.ToList();
        edges.Should().AllSatisfy(e => e.WorkflowNames.Should().Contain("TestWorkflow"));
    }

    [Fact]
    public async Task IngestRunAsync_EmptyStepsWorkflow_CompletesWithoutError()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "EmptyWorkflow",
            Steps = [],
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        graph.NodeCount.Should().Be(0);
        graph.EdgeCount.Should().Be(0);
        await _store.Received(1).SaveAsync(graph, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestRunAsync_SequentialAndSubStepEdges_CoexistCorrectly()
    {
        // Parent -> Child1, Child2 (SubStep edges)
        // Child1 -> Child2 (Sequential edge in flattened order)
        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = new[]
            {
                new AgentStepDefinition
                {
                    Name = "Parent",
                    Kind = AgentStepKind.Loop,
                    Condition = "done",
                    SubSteps = new[]
                    {
                        AgentStepDefinition.RunSkill("Child1"),
                        AgentStepDefinition.InvokeTool("Child2"),
                    },
                },
            },
        };

        var result = new WorkflowBridgeResult { Success = true };
        var graph = new WorkflowHistoryGraph();
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(graph));

        await _observer.IngestRunAsync(definition, result);

        var subStepEdges = graph.Edges.Where(e => e.Kind == EdgeKind.SubStep).ToList();
        var sequentialEdges = graph.Edges.Where(e => e.Kind == EdgeKind.Sequential).ToList();

        // 2 SubStep edges: Parent->Child1, Parent->Child2
        subStepEdges.Should().HaveCount(2);

        // 2 Sequential edges: Parent->Child1, Child1->Child2 (flattened order)
        sequentialEdges.Should().HaveCount(2);
    }
}
