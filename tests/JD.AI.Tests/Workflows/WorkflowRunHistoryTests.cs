using JD.AI.Workflows.Consensus;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowRunHistoryTests : IDisposable
{
    private readonly string _historyPath;
    private readonly WorkflowRunHistory _sut;

    public WorkflowRunHistoryTests()
    {
        _historyPath = Path.Combine(
            Path.GetTempPath(),
            $"jdai-history-{Guid.NewGuid():N}",
            "history.jsonl");
        _sut = new WorkflowRunHistory(_historyPath);
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_historyPath);
        if (dir is not null && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private static RunHistoryEntry MakeEntry(
        string workflow = "my-wf",
        ExecutionStatus status = ExecutionStatus.Completed,
        string initiator = "alice",
        DateTimeOffset? startedAt = null) =>
        new()
        {
            RunId = Guid.NewGuid().ToString("N")[..8],
            WorkflowName = workflow,
            WorkflowVersion = "1.0",
            Initiator = initiator,
            StartedAt = startedAt ?? DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Status = status,
            StepsCompleted = 3,
            StepsTotal = 3,
            DurationMs = 1500,
        };

    [Fact]
    public async Task RecordAndQuery_RoundTrips()
    {
        await _sut.RecordAsync(MakeEntry());

        var entries = await _sut.QueryAsync();
        Assert.Single(entries);
        Assert.Equal("my-wf", entries[0].WorkflowName);
    }

    [Fact]
    public async Task Query_FiltersByWorkflowName()
    {
        await _sut.RecordAsync(MakeEntry(workflow: "wf-a"));
        await _sut.RecordAsync(MakeEntry(workflow: "wf-b"));

        var entries = await _sut.QueryAsync(workflowName: "wf-a");
        Assert.Single(entries);
    }

    [Fact]
    public async Task Query_FiltersByStatus()
    {
        await _sut.RecordAsync(MakeEntry(status: ExecutionStatus.Completed));
        await _sut.RecordAsync(MakeEntry(status: ExecutionStatus.Failed));

        var completed = await _sut.QueryAsync(status: ExecutionStatus.Completed);
        Assert.Single(completed);
    }

    [Fact]
    public async Task Query_FiltersByInitiator()
    {
        await _sut.RecordAsync(MakeEntry(initiator: "alice"));
        await _sut.RecordAsync(MakeEntry(initiator: "bob"));

        var aliceRuns = await _sut.QueryAsync(initiator: "alice");
        Assert.Single(aliceRuns);
    }

    [Fact]
    public async Task Query_FiltersBySince()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-7);
        var recent = DateTimeOffset.UtcNow;

        await _sut.RecordAsync(MakeEntry(startedAt: old));
        await _sut.RecordAsync(MakeEntry(startedAt: recent));

        var entries = await _sut.QueryAsync(since: DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Single(entries);
    }

    [Fact]
    public async Task Query_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
            await _sut.RecordAsync(MakeEntry());

        var entries = await _sut.QueryAsync(limit: 2);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task CountAsync_ReturnsTotal()
    {
        await _sut.RecordAsync(MakeEntry(workflow: "wf-a"));
        await _sut.RecordAsync(MakeEntry(workflow: "wf-a"));
        await _sut.RecordAsync(MakeEntry(workflow: "wf-b"));

        Assert.Equal(2, await _sut.CountAsync("wf-a"));
        Assert.Equal(3, await _sut.CountAsync());
    }

    [Fact]
    public async Task Query_EmptyHistory_ReturnsEmpty()
    {
        var entries = await _sut.QueryAsync();
        Assert.Empty(entries);
    }
}
