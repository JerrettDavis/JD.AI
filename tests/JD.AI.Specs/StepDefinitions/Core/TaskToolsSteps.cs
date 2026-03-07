using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class TaskToolsSteps
{
    private readonly ScenarioContext _context;

    public TaskToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a new task tracker")]
    public void GivenANewTaskTracker()
    {
        _context.Set(new TaskTools(), "TaskTools");
    }

    [Given(@"I have created task ""([^""]+)""")]
    public void GivenIHaveCreatedTask(string title)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        tools.CreateTask(title);
    }

    [Given(@"I have created task ""([^""]+)"" with id ""([^""]+)""")]
    public void GivenIHaveCreatedTaskWithId(string title, string expectedId)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.CreateTask(title);
        // The task-1 id is generated automatically via incrementing counter
        // We trust the first task gets id "task-1"
        result.Should().Contain(expectedId);
    }

    [Given(@"I have created and completed task ""(.*)""")]
    public void GivenIHaveCreatedAndCompletedTask(string title)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var createResult = tools.CreateTask(title);
        // Extract task ID from result like "Created task task-2: Done task"
        var id = createResult.Split(':')[0].Replace("Created task ", "").Trim();
        tools.CompleteTask(id);
    }

    [When(@"I create a task ""(.*)"" with priority ""(.*)""")]
    public void WhenICreateATaskWithPriority(string title, string priority)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.CreateTask(title, priority: priority);
        _context.Set(result, "TaskResult");
    }

    [When(@"I list all tasks")]
    public void WhenIListAllTasks()
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.ListTasks();
        _context.Set(result, "TaskResult");
    }

    [When(@"I list tasks with status ""(.*)""")]
    public void WhenIListTasksWithStatus(string status)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.ListTasks(status);
        _context.Set(result, "TaskResult");
    }

    [When(@"I update task ""(.*)"" status to ""(.*)""")]
    public void WhenIUpdateTaskStatus(string id, string status)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.UpdateTask(id, status: status);
        _context.Set(result, "TaskResult");
    }

    [When(@"I complete task ""(.*)""")]
    public void WhenICompleteTask(string id)
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.CompleteTask(id);
        _context.Set(result, "TaskResult");
    }

    [When(@"I export tasks")]
    public void WhenIExportTasks()
    {
        var tools = _context.Get<TaskTools>("TaskTools");
        var result = tools.ExportTasks();
        _context.Set(result, "TaskResult");
    }

    [Then(@"the task result should contain ""(.*)""")]
    public void ThenTheTaskResultShouldContain(string expected)
    {
        var result = _context.Get<string>("TaskResult");
        result.Should().Contain(expected);
    }

    [Then(@"the task result should not contain ""(.*)""")]
    public void ThenTheTaskResultShouldNotContain(string expected)
    {
        var result = _context.Get<string>("TaskResult");
        result.Should().NotContain(expected);
    }

    [Then(@"the task result should be valid JSON")]
    public void ThenTheTaskResultShouldBeValidJson()
    {
        var result = _context.Get<string>("TaskResult");
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }
}
