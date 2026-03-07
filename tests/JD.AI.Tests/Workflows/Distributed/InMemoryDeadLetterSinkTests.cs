using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for InMemoryDeadLetterSink and the DeadLetteredItem record.
/// Covers thread safety, item retention, and property semantics.
/// </summary>
public sealed class InMemoryDeadLetterSinkTests
{
    // ── Basic recording ───────────────────────────────────────────────────────

    [Fact]
    public void Items_Initially_IsEmpty()
    {
        var sink = new InMemoryDeadLetterSink();

        sink.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DeadLetterAsync_SingleItem_AppearsInItems()
    {
        var sink = new InMemoryDeadLetterSink();
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        await sink.DeadLetterAsync(item, "test reason");

        sink.Items.Should().ContainSingle();
        sink.Items[0].Item.Should().Be(item);
        sink.Items[0].Reason.Should().Be("test reason");
    }

    [Fact]
    public async Task DeadLetterAsync_WithException_RecordsException()
    {
        var sink = new InMemoryDeadLetterSink();
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var ex = new InvalidOperationException("something broke");

        await sink.DeadLetterAsync(item, "error reason", ex);

        sink.Items[0].Exception.Should().Be(ex);
        sink.Items[0].Exception!.Message.Should().Be("something broke");
    }

    [Fact]
    public async Task DeadLetterAsync_WithNullException_ExceptionIsNull()
    {
        var sink = new InMemoryDeadLetterSink();
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        await sink.DeadLetterAsync(item, "reason", exception: null);

        sink.Items[0].Exception.Should().BeNull();
    }

    [Fact]
    public async Task DeadLetterAsync_RecordsDeadLetteredAt_Timestamp()
    {
        var sink = new InMemoryDeadLetterSink();
        var before = DateTimeOffset.UtcNow;
        await sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = "wf" }, "r");
        var after = DateTimeOffset.UtcNow;

        sink.Items[0].DeadLetteredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task DeadLetterAsync_MultipleItems_RetainsInsertionOrder()
    {
        var sink = new InMemoryDeadLetterSink();

        for (var i = 1; i <= 5; i++)
            await sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = $"wf-{i}" }, $"reason-{i}");

        sink.Items.Should().HaveCount(5);
        sink.Items.Select(d => d.Item.WorkflowName).Should().Equal("wf-1", "wf-2", "wf-3", "wf-4", "wf-5");
    }

    // ── Returns completed Task ────────────────────────────────────────────────

    [Fact]
    public async Task DeadLetterAsync_ReturnsCompletedTask()
    {
        var sink = new InMemoryDeadLetterSink();

        var task = sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = "wf" }, "r");

        task.IsCompleted.Should().BeTrue();
        await task; // No exception
    }

    // ── Items snapshot is independent ─────────────────────────────────────────

    [Fact]
    public async Task Items_ReturnsSnapshot_NotLiveReference()
    {
        var sink = new InMemoryDeadLetterSink();
        await sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = "wf-1" }, "r1");

        var snapshot1 = sink.Items;

        await sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = "wf-2" }, "r2");

        var snapshot2 = sink.Items;

        // First snapshot should not reflect the second write
        snapshot1.Should().HaveCount(1);
        snapshot2.Should().HaveCount(2);
    }

    // ── Cancellation token is accepted (and ignored gracefully) ───────────────

    [Fact]
    public async Task DeadLetterAsync_WithCancellationToken_CompletesNormally()
    {
        var sink = new InMemoryDeadLetterSink();
        using var cts = new CancellationTokenSource();

        await sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = "wf" }, "r", ct: cts.Token);

        sink.Items.Should().ContainSingle();
    }

    // ── Thread safety ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeadLetterAsync_ConcurrentCalls_RecordsAllItems()
    {
        var sink = new InMemoryDeadLetterSink();
        const int count = 100;

        var tasks = Enumerable.Range(0, count)
            .Select(i => sink.DeadLetterAsync(new WorkflowWorkItem { WorkflowName = $"wf-{i}" }, "r"))
            .ToArray();

        await Task.WhenAll(tasks);

        sink.Items.Should().HaveCount(count);
    }
}

/// <summary>
/// Tests for the DeadLetteredItem record.
/// </summary>
public sealed class DeadLetteredItemTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var ex = new InvalidOperationException("err");
        var now = DateTimeOffset.UtcNow;

        var dead = new DeadLetteredItem(item, "my reason", ex, now);

        dead.Item.Should().BeSameAs(item);
        dead.Reason.Should().Be("my reason");
        dead.Exception.Should().BeSameAs(ex);
        dead.DeadLetteredAt.Should().Be(now);
    }

    [Fact]
    public void Constructor_WithNullException_IsValid()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        var dead = new DeadLetteredItem(item, "reason", null, DateTimeOffset.UtcNow);

        dead.Exception.Should().BeNull();
    }

    [Fact]
    public void TwoItemsWithSameValues_AreEqual()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", Id = "same-id" };
        var now = DateTimeOffset.UtcNow;
        var ex = new InvalidOperationException("err");

        var a = new DeadLetteredItem(item, "reason", ex, now);
        var b = new DeadLetteredItem(item, "reason", ex, now);

        a.Should().Be(b);
    }

    [Fact]
    public void TwoItemsWithDifferentReasons_AreNotEqual()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", Id = "same-id" };
        var now = DateTimeOffset.UtcNow;

        var a = new DeadLetteredItem(item, "reason-a", null, now);
        var b = new DeadLetteredItem(item, "reason-b", null, now);

        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_CreatesUpdatedCopy()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var original = new DeadLetteredItem(item, "original", null, DateTimeOffset.UtcNow);

        var updated = original with { Reason = "updated" };

        updated.Reason.Should().Be("updated");
        updated.Item.Should().BeSameAs(item);
    }
}
