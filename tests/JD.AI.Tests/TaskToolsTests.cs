using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class TaskToolsTests
{
    [Fact]
    public void CreateTask_ReturnsId()
    {
        var tools = new TaskTools();
        var result = tools.CreateTask("Fix the bug", description: "Null reference in Parser.cs");

        Assert.Contains("task-1", result, StringComparison.Ordinal);
        Assert.Contains("Fix the bug", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ListTasks_ShowsCreatedTasks()
    {
        var tools = new TaskTools();
        tools.CreateTask("Task A");
        tools.CreateTask("Task B", priority: "high");

        var result = tools.ListTasks();

        Assert.Contains("Task A", result, StringComparison.Ordinal);
        Assert.Contains("Task B", result, StringComparison.Ordinal);
        Assert.Contains("(high)", result, StringComparison.Ordinal);
        Assert.Contains("Tasks (2)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ListTasks_FilterByStatus_ReturnsMatching()
    {
        var tools = new TaskTools();
        tools.CreateTask("Pending task");
        tools.CreateTask("In progress task");
        tools.UpdateTask("task-2", status: "in_progress");

        var result = tools.ListTasks(status: "in_progress");

        Assert.Contains("In progress task", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending task", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ListTasks_Empty_ReturnsMessage()
    {
        var tools = new TaskTools();
        var result = tools.ListTasks();

        Assert.Contains("No tasks found", result, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateTask_ChangesStatus()
    {
        var tools = new TaskTools();
        tools.CreateTask("My task");

        var result = tools.UpdateTask("task-1", status: "in_progress");

        Assert.Contains("status=in_progress", result, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateTask_NotFound_ReturnsError()
    {
        var tools = new TaskTools();
        var result = tools.UpdateTask("nonexistent");

        Assert.Contains("not found", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteTask_SetsStatusDone()
    {
        var tools = new TaskTools();
        tools.CreateTask("Finish report");
        tools.CompleteTask("task-1");

        var list = tools.ListTasks(status: "done");

        Assert.Contains("Finish report", list, StringComparison.Ordinal);
        Assert.Contains("[x]", list, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportTasks_ReturnsJson()
    {
        var tools = new TaskTools();
        tools.CreateTask("Export me", priority: "high");

        var json = tools.ExportTasks();

        Assert.Contains("\"title\": \"Export me\"", json, StringComparison.Ordinal);
        Assert.Contains("\"priority\": \"high\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleTasks_IncrementIds()
    {
        var tools = new TaskTools();
        tools.CreateTask("First");
        tools.CreateTask("Second");
        tools.CreateTask("Third");

        var list = tools.ListTasks();

        Assert.Contains("task-1", list, StringComparison.Ordinal);
        Assert.Contains("task-2", list, StringComparison.Ordinal);
        Assert.Contains("task-3", list, StringComparison.Ordinal);
    }
}
