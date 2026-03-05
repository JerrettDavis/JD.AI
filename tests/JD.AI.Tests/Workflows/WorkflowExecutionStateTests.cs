using JD.AI.Workflows;
using JD.AI.Workflows.Consensus;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowExecutionStateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WorkflowExecutionState _sut;

    public WorkflowExecutionStateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-exec-{Guid.NewGuid():N}");
        _sut = new WorkflowExecutionState(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void StartRun_CreatesRunningState()
    {
        var run = _sut.StartRun("my-workflow", "1.0");

        Assert.Equal(ExecutionStatus.Running, run.Status);
        Assert.Equal("my-workflow", run.WorkflowName);
        Assert.NotEmpty(run.RunId);
    }

    [Fact]
    public void Checkpoint_RecordsStepCompletion()
    {
        var run = _sut.StartRun("wf", "1.0");
        _sut.Checkpoint(run.RunId, "step-1", "result-data");

        var loaded = _sut.GetRun(run.RunId);
        Assert.Single(loaded!.Checkpoints);
        Assert.Equal("step-1", loaded.Checkpoints[0].StepCorrelationId);
        Assert.Equal("result-data", loaded.Checkpoints[0].Result);
    }

    [Fact]
    public void Complete_SetsCompletedStatus()
    {
        var run = _sut.StartRun("wf", "1.0");
        _sut.Complete(run.RunId);

        var loaded = _sut.GetRun(run.RunId);
        Assert.Equal(ExecutionStatus.Completed, loaded!.Status);
        Assert.NotNull(loaded.CompletedAt);
    }

    [Fact]
    public void Fail_SetsFailedStatusWithError()
    {
        var run = _sut.StartRun("wf", "1.0");
        _sut.Fail(run.RunId, "Something broke");

        var loaded = _sut.GetRun(run.RunId);
        Assert.Equal(ExecutionStatus.Failed, loaded!.Status);
        Assert.Equal("Something broke", loaded.Error);
    }

    [Fact]
    public void GetResumePoint_ReturnsFirstIncompleteStep()
    {
        var steps = new List<AgentStepDefinition>
        {
            new() { CorrelationId = "s1", Name = "Step 1", Kind = AgentStepKind.Tool },
            new() { CorrelationId = "s2", Name = "Step 2", Kind = AgentStepKind.Tool },
            new() { CorrelationId = "s3", Name = "Step 3", Kind = AgentStepKind.Tool },
        };

        var run = _sut.StartRun("wf", "1.0");
        _sut.Checkpoint(run.RunId, "s1");
        _sut.Fail(run.RunId, "failed at s2");

        var resumePoint = _sut.GetResumePoint(run.RunId, steps);

        Assert.Equal("s2", resumePoint);
    }

    [Fact]
    public void ListRuns_FiltersByWorkflowName()
    {
        _sut.StartRun("workflow-a", "1.0");
        _sut.StartRun("workflow-b", "1.0");
        _sut.StartRun("workflow-a", "2.0");

        var runs = _sut.ListRuns("workflow-a");

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public async Task PersistAndLoad_RoundTrips()
    {
        var run = _sut.StartRun("wf", "1.0", "testuser");
        _sut.Checkpoint(run.RunId, "s1", "data");

        await _sut.PersistAsync(run.RunId);

        // Create new instance to verify disk persistence
        var sut2 = new WorkflowExecutionState(_tempDir);
        var loaded = await sut2.LoadAsync(run.RunId);

        Assert.NotNull(loaded);
        Assert.Equal("wf", loaded.WorkflowName);
        Assert.Single(loaded.Checkpoints);
    }

    [Fact]
    public void Checkpoint_NonexistentRun_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => _sut.Checkpoint("nope", "s1"));
    }

    [Fact]
    public void GetRun_NotFound_ReturnsNull()
    {
        Assert.Null(_sut.GetRun("nonexistent"));
    }
}
