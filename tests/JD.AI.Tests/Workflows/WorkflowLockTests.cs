using JD.AI.Workflows.Consensus;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowLockTests
{
    [Fact]
    public void TryAcquire_SucceedsForNewWorkflow()
    {
        var sut = new WorkflowLock();
        Assert.True(sut.TryAcquire("my-wf", "alice"));
    }

    [Fact]
    public void TryAcquire_FailsWhenHeldByAnother()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.False(sut.TryAcquire("my-wf", "bob"));
    }

    [Fact]
    public void TryAcquire_SucceedsForSameOwner()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.True(sut.TryAcquire("my-wf", "alice"));
    }

    [Fact]
    public void TryAcquire_SucceedsAfterExpiry()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice", TimeSpan.Zero);

        // Lock has expired (zero duration)
        Assert.True(sut.TryAcquire("my-wf", "bob"));
    }

    [Fact]
    public void Release_SucceedsForOwner()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.True(sut.Release("my-wf", "alice"));
        Assert.Null(sut.GetLock("my-wf"));
    }

    [Fact]
    public void Release_FailsForNonOwner()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.False(sut.Release("my-wf", "bob"));
    }

    [Fact]
    public void GetLock_ReturnsNullForExpired()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice", TimeSpan.Zero);

        Assert.Null(sut.GetLock("my-wf"));
    }

    [Fact]
    public void IsLockedBy_ReturnsTrueForOwner()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.True(sut.IsLockedBy("my-wf", "alice"));
        Assert.False(sut.IsLockedBy("my-wf", "bob"));
    }

    [Fact]
    public void ForceRelease_RemovesRegardlessOfOwner()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("my-wf", "alice");

        Assert.True(sut.ForceRelease("my-wf"));
        Assert.Null(sut.GetLock("my-wf"));
    }

    [Fact]
    public void ListActiveLocks_ExcludesExpired()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("wf-1", "alice");
        sut.TryAcquire("wf-2", "bob", TimeSpan.Zero); // expired

        var active = sut.ListActiveLocks();
        Assert.Single(active);
        Assert.Equal("wf-1", active[0].WorkflowName);
    }

    [Fact]
    public void ActiveLockCount_OnlyCountsNonExpired()
    {
        var sut = new WorkflowLock();
        sut.TryAcquire("wf-1", "alice");
        sut.TryAcquire("wf-2", "bob", TimeSpan.Zero);

        Assert.Equal(1, sut.ActiveLockCount);
    }
}
