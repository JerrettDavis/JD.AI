using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.History;
using NSubstitute;

namespace JD.AI.Tests.Workflows.History;

public sealed class WorkflowHistoryAdvisorTests
{
    private readonly IWorkflowHistoryGraphStore _store = Substitute.For<IWorkflowHistoryGraphStore>();
    private readonly WorkflowHistoryAdvisor _advisor;

    public WorkflowHistoryAdvisorTests()
    {
        _advisor = new WorkflowHistoryAdvisor(_store);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AdviseAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdviseAsync_EmptyGraph_ReturnsNoHistoryAdvisory()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkflowHistoryGraph()));

        var definition = new AgentWorkflowDefinition
        {
            Name = "TestWorkflow",
            Steps = [AgentStepDefinition.RunSkill("Step1")],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.HasHistory.Should().BeFalse();
        advisory.FamiliarityScore.Should().Be(0.0);
        advisory.StepAdvisories.Should().BeEmpty();
        advisory.SimilarPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task AdviseAsync_AllStepsSeen_FamiliarityScoreIsOne()
    {
        var step1 = AgentStepDefinition.RunSkill("Step1");
        var step2 = AgentStepDefinition.InvokeTool("Step2");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "ExistingWorkflow", successCount: 5, failureCount: 0);
        AddNodeToGraph(graph, step2, "ExistingWorkflow", successCount: 3, failureCount: 1);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [step1, step2],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.HasHistory.Should().BeTrue();
        advisory.FamiliarityScore.Should().Be(1.0);
        advisory.StepAdvisories.Should().HaveCount(2);
        advisory.StepAdvisories.Should().AllSatisfy(sa => sa.PreviouslySeen.Should().BeTrue());
    }

    [Fact]
    public async Task AdviseAsync_HalfStepsSeen_FamiliarityScoreIsHalf()
    {
        var step1 = AgentStepDefinition.RunSkill("Step1");
        var step2 = AgentStepDefinition.RunSkill("Step2");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "ExistingWorkflow", successCount: 1, failureCount: 0);
        // step2 is NOT in graph

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [step1, step2],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.HasHistory.Should().BeTrue();
        advisory.FamiliarityScore.Should().Be(0.5);
        advisory.StepAdvisories.Should().HaveCount(2);

        var seenAdvisory = advisory.StepAdvisories.First(sa => string.Equals(sa.StepName, "Step1"));
        seenAdvisory.PreviouslySeen.Should().BeTrue();

        var unseenAdvisory = advisory.StepAdvisories.First(sa => string.Equals(sa.StepName, "Step2"));
        unseenAdvisory.PreviouslySeen.Should().BeFalse();
        unseenAdvisory.HistoricalSuccessRate.Should().Be(0.0);
    }

    [Fact]
    public async Task AdviseAsync_StepHasSuccessRate_ReflectedInAdvisory()
    {
        var step = AgentStepDefinition.RunSkill("TestStep");

        var graph = new WorkflowHistoryGraph();
        var fp = StepFingerprint.Compute(step);
        var node = graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = step.Name,
            Kind = step.Kind,
            Target = step.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        node.SuccessCount = 8;
        node.FailureCount = 2;
        node.ExecutionCount = 10;

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [step],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.StepAdvisories[0].HistoricalSuccessRate.Should().Be(0.8);
    }

    [Fact]
    public async Task AdviseAsync_MostCommonNextStep_ReturnsHeaviestEdgeTarget()
    {
        var step1 = AgentStepDefinition.RunSkill("Step1");
        var step2 = AgentStepDefinition.RunSkill("Step2");
        var step3 = AgentStepDefinition.RunSkill("Step3");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "Workflow", successCount: 5, failureCount: 0);
        AddNodeToGraph(graph, step2, "Workflow", successCount: 5, failureCount: 0);
        AddNodeToGraph(graph, step3, "Workflow", successCount: 5, failureCount: 0);

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);
        var fp3 = StepFingerprint.Compute(step3);

        // Step1 goes to Step2 (weight 10) more often than Step3 (weight 2)
        for (var i = 0; i < 10; i++)
            graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);
        for (var i = 0; i < 2; i++)
            graph.RecordTransition(fp1, fp3, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [step1],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.StepAdvisories[0].MostCommonNextStep.Should().Be("Step2");
    }

    [Fact]
    public async Task AdviseAsync_NoSteps_ReturnsEmptyAdvisories()
    {
        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, AgentStepDefinition.RunSkill("SomeStep"), "Wf", successCount: 1, failureCount: 0);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "EmptyWorkflow",
            Steps = [],
        };

        var advisory = await _advisor.AdviseAsync(definition);

        advisory.StepAdvisories.Should().BeEmpty();
        advisory.FamiliarityScore.Should().Be(0.0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FindSimilarPathsAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindSimilarPathsAsync_EmptyGraph_ReturnsEmpty()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkflowHistoryGraph()));

        var definition = new AgentWorkflowDefinition
        {
            Name = "Test",
            Steps = [AgentStepDefinition.RunSkill("Step1")],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindSimilarPathsAsync_IdenticalWorkflow_JaccardIsOne()
    {
        var step1 = AgentStepDefinition.RunSkill("Step1");
        var step2 = AgentStepDefinition.InvokeTool("Step2");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "ExistingWorkflow", successCount: 5, failureCount: 0);
        AddNodeToGraph(graph, step2, "ExistingWorkflow", successCount: 5, failureCount: 0);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [step1, step2],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition);

        results.Should().HaveCount(1);
        results[0].Similarity.Should().Be(1.0);
        results[0].WorkflowName.Should().Be("ExistingWorkflow");
        results[0].SharedStepNames.Should().HaveCount(2);
        results[0].UniqueToCandidate.Should().BeEmpty();
    }

    [Fact]
    public async Task FindSimilarPathsAsync_NoOverlap_ReturnsEmpty()
    {
        var existingStep = AgentStepDefinition.RunSkill("ExistingStep");
        var newStep = AgentStepDefinition.RunSkill("NewStep");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, existingStep, "ExistingWorkflow", successCount: 1, failureCount: 0);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [newStep],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindSimilarPathsAsync_PartialOverlap_CorrectJaccard()
    {
        // candidate: {A, B, C}; existing: {A, B, D}
        // intersection = {A, B} = 2, union = {A, B, C, D} = 4 → jaccard = 0.5
        var stepA = AgentStepDefinition.RunSkill("A");
        var stepB = AgentStepDefinition.RunSkill("B");
        var stepC = AgentStepDefinition.RunSkill("C");
        var stepD = AgentStepDefinition.RunSkill("D");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, stepA, "ExistingWorkflow", successCount: 1, failureCount: 0);
        AddNodeToGraph(graph, stepB, "ExistingWorkflow", successCount: 1, failureCount: 0);
        AddNodeToGraph(graph, stepD, "ExistingWorkflow", successCount: 1, failureCount: 0);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "NewWorkflow",
            Steps = [stepA, stepB, stepC],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition);

        results.Should().HaveCount(1);
        results[0].Similarity.Should().BeApproximately(0.5, 0.001);
        results[0].SharedStepNames.Should().HaveCount(2);
        // stepC is not yet in the graph, so its fingerprint is used as the label
        results[0].UniqueToCandidate.Should().HaveCount(1);
        results[0].UniqueToCandidate[0].Should().Be(StepFingerprint.Compute(stepC));
    }

    [Fact]
    public async Task FindSimilarPathsAsync_ReturnsTopNResults()
    {
        // Create 6 workflows, each sharing 1 step with the candidate
        var sharedStep = AgentStepDefinition.RunSkill("Shared");
        var graph = new WorkflowHistoryGraph();

        for (var i = 0; i < 6; i++)
        {
            AddNodeToGraph(graph, sharedStep, $"Workflow{i}", successCount: 1, failureCount: 0);
        }

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "New",
            Steps = [sharedStep],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition, topN: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task FindSimilarPathsAsync_OrderedBySimilarityDescending()
    {
        var stepA = AgentStepDefinition.RunSkill("A");
        var stepB = AgentStepDefinition.RunSkill("B");
        var stepC = AgentStepDefinition.RunSkill("C");

        var graph = new WorkflowHistoryGraph();
        // Workflow1 has A,B,C — jaccard with {A,B} = 2/3
        AddNodeToGraph(graph, stepA, "Workflow1", successCount: 1, failureCount: 0);
        AddNodeToGraph(graph, stepB, "Workflow1", successCount: 1, failureCount: 0);
        AddNodeToGraph(graph, stepC, "Workflow1", successCount: 1, failureCount: 0);
        // Workflow2 has A only — jaccard with {A,B} = 1/2
        AddNodeToGraph(graph, stepA, "Workflow2", successCount: 1, failureCount: 0);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var definition = new AgentWorkflowDefinition
        {
            Name = "New",
            Steps = [stepA, stepB],
        };

        var results = await _advisor.FindSimilarPathsAsync(definition);

        results[0].Similarity.Should().BeGreaterThan(results[1].Similarity);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FindWorkflowsThroughStepAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindWorkflowsThroughStepAsync_ByName_FindsWorkflow()
    {
        var step = AgentStepDefinition.RunSkill("MyStep");

        var graph = new WorkflowHistoryGraph();
        var fp = StepFingerprint.Compute(step);
        var node = graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = "MyStep",
            Kind = step.Kind,
            Target = step.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        node.ExecutionCount = 3;
        node.WorkflowNames.Add("WorkflowA");
        node.WorkflowNames.Add("WorkflowB");

        // Add an edge so FindWorkflowsThroughStep can find via edge workflowNames
        var otherStep = AgentStepDefinition.RunSkill("OtherStep");
        var fp2 = StepFingerprint.Compute(otherStep);
        graph.GetOrAddNode(fp2, () => new WorkflowHistoryNode
        {
            Fingerprint = fp2,
            Name = "OtherStep",
            Kind = otherStep.Kind,
            Target = otherStep.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        graph.RecordTransition(fp, fp2, EdgeKind.Sequential, "WorkflowA", TimeSpan.Zero);
        graph.RecordTransition(fp, fp2, EdgeKind.Sequential, "WorkflowB", TimeSpan.Zero);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var results = await _advisor.FindWorkflowsThroughStepAsync("MyStep");

        results.Should().HaveCount(2);
        results.Select(r => r.WorkflowName).Should().Contain("WorkflowA", "WorkflowB");
    }

    [Fact]
    public async Task FindWorkflowsThroughStepAsync_UnknownStep_ReturnsEmpty()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkflowHistoryGraph()));

        var results = await _advisor.FindWorkflowsThroughStepAsync("NonExistentStep");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindWorkflowsThroughStepAsync_RespectsLimit()
    {
        var step = AgentStepDefinition.RunSkill("SharedStep");
        var graph = new WorkflowHistoryGraph();
        var fp = StepFingerprint.Compute(step);
        graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = "SharedStep",
            Kind = step.Kind,
            Target = step.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });

        var otherStep = AgentStepDefinition.RunSkill("Other");
        var fp2 = StepFingerprint.Compute(otherStep);
        graph.GetOrAddNode(fp2, () => new WorkflowHistoryNode
        {
            Fingerprint = fp2,
            Name = "Other",
            Kind = otherStep.Kind,
            Target = otherStep.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });

        for (var i = 0; i < 10; i++)
            graph.RecordTransition(fp, fp2, EdgeKind.Sequential, $"Workflow{i}", TimeSpan.Zero);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var results = await _advisor.FindWorkflowsThroughStepAsync("SharedStep", limit: 3);

        results.Should().HaveCount(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetCommonPathsFromAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommonPathsFromAsync_UnknownStep_ReturnsEmpty()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkflowHistoryGraph()));

        var results = await _advisor.GetCommonPathsFromAsync("NonExistentStep");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommonPathsFromAsync_LinearPath_ReturnsPathInOrder()
    {
        // Graph: A --10--> B --8--> C
        var step1 = AgentStepDefinition.RunSkill("A");
        var step2 = AgentStepDefinition.RunSkill("B");
        var step3 = AgentStepDefinition.RunSkill("C");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "Workflow", successCount: 10, failureCount: 0);
        AddNodeToGraph(graph, step2, "Workflow", successCount: 8, failureCount: 0);
        AddNodeToGraph(graph, step3, "Workflow", successCount: 8, failureCount: 0);

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);
        var fp3 = StepFingerprint.Compute(step3);

        for (var i = 0; i < 10; i++)
            graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);
        for (var i = 0; i < 8; i++)
            graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var results = await _advisor.GetCommonPathsFromAsync("A", topN: 1);

        results.Should().HaveCount(1);
        results[0].Steps.Should().HaveCount(3);
        results[0].Steps[0].Name.Should().Be("A");
        results[0].Steps[1].Name.Should().Be("B");
        results[0].Steps[2].Name.Should().Be("C");
    }

    [Fact]
    public async Task GetCommonPathsFromAsync_TotalWeightReflectsMinimumEdgeWeight()
    {
        // A --10--> B --5--> C  → min weight = 5
        var step1 = AgentStepDefinition.RunSkill("A");
        var step2 = AgentStepDefinition.RunSkill("B");
        var step3 = AgentStepDefinition.RunSkill("C");

        var graph = new WorkflowHistoryGraph();
        AddNodeToGraph(graph, step1, "Workflow", successCount: 10, failureCount: 0);
        AddNodeToGraph(graph, step2, "Workflow", successCount: 5, failureCount: 0);
        AddNodeToGraph(graph, step3, "Workflow", successCount: 5, failureCount: 0);

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);
        var fp3 = StepFingerprint.Compute(step3);

        for (var i = 0; i < 10; i++)
            graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
            graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "Workflow", TimeSpan.Zero);

        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));

        var results = await _advisor.GetCommonPathsFromAsync("A", topN: 1);

        results.Should().HaveCount(1);
        results[0].TotalWeight.Should().Be(5);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Null guards
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdviseAsync_NullDefinition_Throws()
    {
        await FluentActions.Invoking(() => _advisor.AdviseAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("definition");
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        FluentActions.Invoking(() => new WorkflowHistoryAdvisor(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static void AddNodeToGraph(
        WorkflowHistoryGraph graph,
        AgentStepDefinition step,
        string workflowName,
        long successCount,
        long failureCount)
    {
        var fp = StepFingerprint.Compute(step);
        var node = graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = step.Name,
            Kind = step.Kind,
            Target = step.Target,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        node.SuccessCount += successCount;
        node.FailureCount += failureCount;
        node.ExecutionCount += successCount + failureCount;
        node.WorkflowNames.Add(workflowName);
    }
}
