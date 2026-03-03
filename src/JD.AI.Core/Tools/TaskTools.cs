using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Task/todo management tools for the agent to track work items within a session.
/// </summary>
public sealed class TaskTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConcurrentDictionary<string, TaskItem> _tasks = new(StringComparer.Ordinal);
    private int _nextId;

    [KernelFunction("create_task")]
    [Description("Create a new task/todo item to track work. Returns the task ID.")]
    public string CreateTask(
        [Description("Short title for the task")] string title,
        [Description("Detailed description (optional)")] string? description = null,
        [Description("Priority: low, medium, high (default: medium)")] string priority = "medium")
    {
        var id = $"task-{Interlocked.Increment(ref _nextId)}";
        var task = new TaskItem
        {
            Id = id,
            Title = title,
            Description = description ?? "",
            Priority = priority,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };
        _tasks[id] = task;
        return $"Created task {id}: {title}";
    }

    [KernelFunction("list_tasks")]
    [Description("List all tasks, optionally filtered by status.")]
    public string ListTasks(
        [Description("Filter by status: pending, in_progress, done, blocked (optional)")] string? status = null)
    {
        var tasks = _tasks.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            tasks = tasks.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var list = tasks.OrderBy(t => t.CreatedAt).ToList();
        if (list.Count == 0)
        {
            return status is null ? "No tasks found." : $"No tasks with status '{status}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Tasks ({list.Count}):");
        foreach (var t in list)
        {
            var icon = t.Status switch
            {
                "done" => "[x]",
                "in_progress" => "[~]",
                "blocked" => "[!]",
                _ => "[ ]",
            };
            sb.AppendLine($"  {icon} {t.Id}: {t.Title} ({t.Priority}) - {t.Status}");
        }

        return sb.ToString();
    }

    [KernelFunction("update_task")]
    [Description("Update a task's status or details.")]
    public string UpdateTask(
        [Description("Task ID (e.g. 'task-1')")] string id,
        [Description("New status: pending, in_progress, done, blocked (optional)")] string? status = null,
        [Description("Updated title (optional)")] string? title = null,
        [Description("Updated description (optional)")] string? description = null)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return $"Task '{id}' not found.";
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            task.Status = status;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            task.Title = title;
        }

        if (description is not null)
        {
            task.Description = description;
        }

        task.UpdatedAt = DateTime.UtcNow;
        return $"Updated task {id}: status={task.Status}, title={task.Title}";
    }

    [KernelFunction("complete_task")]
    [Description("Mark a task as done.")]
    public string CompleteTask(
        [Description("Task ID (e.g. 'task-1')")] string id)
    {
        return UpdateTask(id, status: "done");
    }

    [KernelFunction("export_tasks")]
    [Description("Export all tasks as JSON for persistence or sharing.")]
    public string ExportTasks()
    {
        var tasks = _tasks.Values.OrderBy(t => t.CreatedAt).ToList();
        return JsonSerializer.Serialize(tasks, JsonOptions);
    }

    private sealed class TaskItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Priority { get; set; } = "medium";
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
