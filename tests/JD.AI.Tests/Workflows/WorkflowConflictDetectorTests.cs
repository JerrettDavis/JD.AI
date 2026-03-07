using JD.AI.Workflows;
using JD.AI.Workflows.Consensus;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowConflictDetectorTests
{
    private static AgentWorkflowDefinition MakeWorkflow(
        string name = "test-wf",
        string version = "1.0",
        string description = "Test workflow",
        params AgentStepDefinition[] steps) =>
        new()
        {
            Name = name,
            Version = version,
            Description = description,
            Steps = [.. steps],
        };

    private static AgentStepDefinition MakeStep(string correlationId, string name = "step") =>
        new()
        {
            CorrelationId = correlationId,
            Name = name,
            Kind = AgentStepKind.Tool,
            Target = name,
        };

    [Fact]
    public void Detect_NoChanges_NoConflicts()
    {
        var ancestor = MakeWorkflow();
        var report = WorkflowConflictDetector.Detect(ancestor, ancestor, ancestor);

        Assert.False(report.HasConflicts);
        Assert.Empty(report.Conflicts);
    }

    [Fact]
    public void Detect_OnesSideChangesDescription_AutoResolved()
    {
        var ancestor = MakeWorkflow();
        var ours = MakeWorkflow(description: "Updated description");

        var report = WorkflowConflictDetector.Detect(ancestor, ours, ancestor);

        Assert.False(report.HasConflicts);
        Assert.Contains(report.AutoResolved, r => r.Contains("Description"));
    }

    [Fact]
    public void Detect_BothChangeDescriptionDifferently_Conflict()
    {
        var ancestor = MakeWorkflow();
        var ours = MakeWorkflow(description: "Our desc");
        var theirs = MakeWorkflow(description: "Their desc");

        var report = WorkflowConflictDetector.Detect(ancestor, ours, theirs);

        Assert.True(report.HasConflicts);
        Assert.Single(report.Conflicts);
        Assert.Equal(ConflictKind.MetadataConflict, report.Conflicts[0].Kind);
    }

    [Fact]
    public void Detect_BothAddSameStep_AutoResolved()
    {
        var ancestor = MakeWorkflow();
        var step = MakeStep("s1", "shared-step");
        var ours = MakeWorkflow(steps: step);
        var theirs = MakeWorkflow(steps: new AgentStepDefinition
        {
            CorrelationId = "s1",
            Name = "shared-step",
            Kind = AgentStepKind.Tool,
            Target = "shared-step",
        });

        var report = WorkflowConflictDetector.Detect(ancestor, ours, theirs);

        Assert.False(report.HasConflicts);
    }

    [Fact]
    public void Detect_BothModifyStepDifferently_Conflict()
    {
        var step = MakeStep("s1", "original");
        var ancestor = MakeWorkflow(steps: step);

        var ourStep = MakeStep("s1", "our-change");
        var theirStep = MakeStep("s1", "their-change");

        var ours = MakeWorkflow(steps: ourStep);
        var theirs = MakeWorkflow(steps: theirStep);

        var report = WorkflowConflictDetector.Detect(ancestor, ours, theirs);

        Assert.True(report.HasConflicts);
        Assert.Equal(ConflictKind.StepConflict, report.Conflicts[0].Kind);
    }

    [Fact]
    public void Detect_OneDeletesOtherModifies_DeleteModifyConflict()
    {
        var step = MakeStep("s1", "original");
        var ancestor = MakeWorkflow(steps: step);

        var ours = MakeWorkflow(); // deleted
        var modifiedStep = MakeStep("s1", "modified");
        var theirs = MakeWorkflow(steps: modifiedStep);

        var report = WorkflowConflictDetector.Detect(ancestor, ours, theirs);

        Assert.True(report.HasConflicts);
        Assert.Equal(ConflictKind.DeleteModifyConflict, report.Conflicts[0].Kind);
    }

    [Fact]
    public void TryMerge_NoConflicts_ReturnsMerged()
    {
        var step = MakeStep("s1", "shared");
        var ancestor = MakeWorkflow(steps: step);

        var ours = MakeWorkflow(description: "Updated desc", steps: step);
        var theirs = MakeWorkflow(steps: step);

        var merged = WorkflowConflictDetector.TryMerge(ancestor, ours, theirs);

        Assert.NotNull(merged);
        Assert.Equal("Updated desc", merged.Description);
    }

    [Fact]
    public void TryMerge_WithConflicts_ReturnsNull()
    {
        var ancestor = MakeWorkflow();
        var ours = MakeWorkflow(description: "A");
        var theirs = MakeWorkflow(description: "B");

        var merged = WorkflowConflictDetector.TryMerge(ancestor, ours, theirs);

        Assert.Null(merged);
    }

    [Fact]
    public void ComputeHash_DeterministicForSameContent()
    {
        var wf1 = MakeWorkflow();
        var wf2 = MakeWorkflow();

        Assert.Equal(
            WorkflowConflictDetector.ComputeHash(wf1),
            WorkflowConflictDetector.ComputeHash(wf2));
    }

    [Fact]
    public void ComputeHash_DiffersForDifferentContent()
    {
        var wf1 = MakeWorkflow(description: "A");
        var wf2 = MakeWorkflow(description: "B");

        Assert.NotEqual(
            WorkflowConflictDetector.ComputeHash(wf1),
            WorkflowConflictDetector.ComputeHash(wf2));
    }

    [Fact]
    public void Detect_NullArguments_Throws()
    {
        var wf = MakeWorkflow();
        Assert.Throws<ArgumentNullException>(() => WorkflowConflictDetector.Detect(null!, wf, wf));
        Assert.Throws<ArgumentNullException>(() => WorkflowConflictDetector.Detect(wf, null!, wf));
        Assert.Throws<ArgumentNullException>(() => WorkflowConflictDetector.Detect(wf, wf, null!));
    }
}
