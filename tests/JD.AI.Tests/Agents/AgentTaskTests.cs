using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Tasks;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Tests for <see cref="AgentTask"/>, <see cref="AgentTaskType"/>, <see cref="AgentTaskStatus"/>
/// and <see cref="IAgentTaskRegistry"/> implementation.
/// </summary>
public sealed class AgentTaskTests
{
    // ── AgentTaskType enum values ────────────────────────────────────────────

    [Theory]
    [InlineData(AgentTaskType.LocalBash, 0)]
    [InlineData(AgentTaskType.LocalAgent, 1)]
    [InlineData(AgentTaskType.Workflow, 2)]
    [InlineData(AgentTaskType.Dream, 3)]
    public void AgentTaskType_ExpectedValues(AgentTaskType type, int expected) =>
        ((int)type).Should().Be(expected);

    // ── AgentTaskStatus enum values ─────────────────────────────────────────

    [Theory]
    [InlineData(AgentTaskStatus.Pending, 0)]
    [InlineData(AgentTaskStatus.Running, 1)]
    [InlineData(AgentTaskStatus.Completed, 2)]
    [InlineData(AgentTaskStatus.Failed, 3)]
    [InlineData(AgentTaskStatus.Cancelled, 4)]
    public void AgentTaskStatus_ExpectedValues(AgentTaskStatus status, int expected) =>
        ((int)status).Should().Be(expected);

    // ── AgentTask construction and interface ─────────────────────────────────

    [Fact]
    public void AgentTask_ImplementsIAgentTask()
    {
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-abc",
            AgentTaskType.LocalAgent,
            AgentTaskStatus.Running,
            "Test task",
            DateTimeOffset.UtcNow,
            async ct => { await Task.Yield(); return "result"; },
            cts.Token);

        ((IAgentTask)task).Id.Should().Be("task-abc");
        ((IAgentTask)task).Type.Should().Be(AgentTaskType.LocalAgent);
        ((IAgentTask)task).Status.Should().Be(AgentTaskStatus.Running);
        ((IAgentTask)task).Description.Should().Be("Test task");
        ((IAgentTask)task).Ct.Should().Be(cts.Token);
    }

    [Fact]
    public async Task AgentTask_ExecuteAsync_ReturnsFunctionResult()
    {
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-xyz",
            AgentTaskType.Workflow,
            AgentTaskStatus.Pending,
            null,
            DateTimeOffset.UtcNow,
            async ct => { await Task.Delay(1, ct); return "workflow-output"; },
            cts.Token);

        var result = await ((IAgentTask)task).ExecuteAsync(CancellationToken.None);

        result.Should().Be("workflow-output");
    }

    [Fact]
    public async Task AgentTask_ExecuteAsync_PropagatesCancellation()
    {
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-cancel",
            AgentTaskType.LocalBash,
            AgentTaskStatus.Running,
            null,
            DateTimeOffset.UtcNow,
            async ct => { await Task.Delay(Timeout.Infinite, ct); return "never"; },
            cts.Token);

        cts.Cancel();
        var act = async () => await ((IAgentTask)task).ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── AgentTaskRegistry ─────────────────────────────────────────────────────

    [Fact]
    public void AgentTaskRegistry_StartsEmpty()
    {
        var registry = new AgentTaskRegistry();

        registry.ActiveTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task AgentTaskRegistry_RegisterAsync_AddsToActiveTasks()
    {
        var registry = new AgentTaskRegistry();
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-reg-1",
            AgentTaskType.LocalAgent,
            AgentTaskStatus.Pending,
            null,
            DateTimeOffset.UtcNow,
            ct => Task.FromResult("done"),
            cts.Token);

        await registry.RegisterAsync(task);

        registry.ActiveTasks.Should().ContainSingle(t => t.Id == "task-reg-1");
    }

    [Fact]
    public async Task AgentTaskRegistry_CancelAsync_SetsStatusToCancelled()
    {
        var registry = new AgentTaskRegistry();
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-cancel-1",
            AgentTaskType.Dream,
            AgentTaskStatus.Running,
            null,
            DateTimeOffset.UtcNow,
            ct => Task.FromResult("done"),
            cts.Token);
        await registry.RegisterAsync(task);

        await registry.CancelAsync("task-cancel-1");

        registry.ActiveTasks.Single(t => t.Id == "task-cancel-1")
            .Status.Should().Be(AgentTaskStatus.Cancelled);
    }

    [Fact]
    public async Task AgentTaskRegistry_CancelAsync_UnknownId_DoesNotThrow()
    {
        var registry = new AgentTaskRegistry();

        var act = async () => await registry.CancelAsync("nonexistent");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AgentTaskRegistry_RegisterAsync_ReturnsTaskId()
    {
        var registry = new AgentTaskRegistry();
        var cts = new CancellationTokenSource();
        var task = new AgentTask(
            "task-return-1",
            AgentTaskType.LocalBash,
            AgentTaskStatus.Pending,
            null,
            DateTimeOffset.UtcNow,
            ct => Task.FromResult("ok"),
            cts.Token);

        var id = await registry.RegisterAsync(task);

        id.Should().Be("task-return-1");
    }

    [Fact]
    public async Task AgentTaskRegistry_MultipleTasks_TrackedSeparately()
    {
        var registry = new AgentTaskRegistry();
        var cts = new CancellationTokenSource();

        var task1 = new AgentTask("r1", AgentTaskType.LocalBash, AgentTaskStatus.Running, null,
            DateTimeOffset.UtcNow, ct => Task.FromResult("a"), cts.Token);
        var task2 = new AgentTask("r2", AgentTaskType.LocalAgent, AgentTaskStatus.Running, null,
            DateTimeOffset.UtcNow, ct => Task.FromResult("b"), cts.Token);
        var task3 = new AgentTask("r3", AgentTaskType.Workflow, AgentTaskStatus.Running, null,
            DateTimeOffset.UtcNow, ct => Task.FromResult("c"), cts.Token);

        await registry.RegisterAsync(task1);
        await registry.RegisterAsync(task2);
        await registry.RegisterAsync(task3);

        registry.ActiveTasks.Should().HaveCount(3);
    }
}