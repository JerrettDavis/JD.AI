using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Scheduled task management tools — create, list, update, and remove recurring or one-shot tasks.
/// Tasks are stored in-memory for the current session; persistence is optional via SessionStore.
/// </summary>
[ToolPlugin("scheduler", RequiresInjection = true)]
public sealed class SchedulerTools
{
    private readonly Lock _lock = new();
    private readonly List<ScheduledTask> _tasks = [];
    private int _nextId = 1;

    [KernelFunction("cron_list")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all scheduled tasks and their status.")]
    public string ListTasks()
    {
        lock (_lock)
        {
            if (_tasks.Count == 0)
                return "No scheduled tasks. Use cron_add to create one.";

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Scheduled Tasks ({_tasks.Count})");
            sb.AppendLine();
            sb.AppendLine("| ID | Name | Schedule | Status | Last Run | Runs |");
            sb.AppendLine("|----|------|----------|--------|----------|------|");

            foreach (var t in _tasks)
            {
                var lastRun = t.LastRunUtc.HasValue
                    ? t.LastRunUtc.Value.ToString("u", CultureInfo.InvariantCulture)
                    : "never";
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {t.Id} | {t.Name} | {t.Schedule} | {t.Status} | {lastRun} | {t.RunCount} |");
            }

            return sb.ToString();
        }
    }

    [KernelFunction("cron_add")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Add a new scheduled task. Schedule can be a cron expression (e.g. '*/5 * * * *') " +
                 "or a simple interval like '5m', '1h', '30s'.")]
    public string AddTask(
        [Description("Task name")] string name,
        [Description("Cron expression or interval (e.g. '*/5 * * * *', '5m', '1h')")] string schedule,
        [Description("Command or action to execute when triggered")] string command,
        [Description("Optional description")] string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Error: Task name cannot be empty.";
        if (string.IsNullOrWhiteSpace(schedule))
            return "Error: Schedule cannot be empty.";
        if (string.IsNullOrWhiteSpace(command))
            return "Error: Command cannot be empty.";

        lock (_lock)
        {
            var id = _nextId++;
            var task = new ScheduledTask
            {
                Id = id,
                Name = name,
                Schedule = schedule,
                Command = command,
                Description = description ?? "",
                Status = "active",
                CreatedUtc = DateTime.UtcNow,
            };
            _tasks.Add(task);
            return $"Scheduled task #{id} '{name}' created with schedule '{schedule}'.";
        }
    }

    [KernelFunction("cron_remove")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Remove a scheduled task by ID.")]
    public string RemoveTask(
        [Description("Task ID to remove")] int taskId)
    {
        lock (_lock)
        {
            var idx = _tasks.FindIndex(t => t.Id == taskId);
            if (idx < 0)
                return $"Error: Task #{taskId} not found.";

            var name = _tasks[idx].Name;
            _tasks.RemoveAt(idx);
            return $"Task #{taskId} '{name}' removed.";
        }
    }

    [KernelFunction("cron_update")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Update an existing scheduled task's schedule, command, or status.")]
    public string UpdateTask(
        [Description("Task ID to update")] int taskId,
        [Description("New schedule (optional)")] string? schedule = null,
        [Description("New command (optional)")] string? command = null,
        [Description("New status: 'active' or 'paused' (optional)")] string? status = null)
    {
        lock (_lock)
        {
            var task = _tasks.Find(t => t.Id == taskId);
            if (task is null)
                return $"Error: Task #{taskId} not found.";

            if (!string.IsNullOrWhiteSpace(schedule))
                task.Schedule = schedule;
            if (!string.IsNullOrWhiteSpace(command))
                task.Command = command;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "paused", StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: Status must be 'active' or 'paused'.";
                }

                task.Status = status.ToLowerInvariant();
            }

            return $"Task #{taskId} '{task.Name}' updated.";
        }
    }

    [KernelFunction("cron_run")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Manually trigger a scheduled task immediately.")]
    public string RunTask(
        [Description("Task ID to run now")] int taskId)
    {
        lock (_lock)
        {
            var task = _tasks.Find(t => t.Id == taskId);
            if (task is null)
                return $"Error: Task #{taskId} not found.";

            task.LastRunUtc = DateTime.UtcNow;
            task.RunCount++;
            return $"Task #{taskId} '{task.Name}' triggered. Command: {task.Command}";
        }
    }

    [KernelFunction("cron_history")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get execution history for a specific scheduled task.")]
    public string GetTaskHistory(
        [Description("Task ID")] int taskId)
    {
        lock (_lock)
        {
            var task = _tasks.Find(t => t.Id == taskId);
            if (task is null)
                return $"Error: Task #{taskId} not found.";

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Task #{task.Id}: {task.Name}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Schedule**: {task.Schedule}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Command**: {task.Command}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Status**: {task.Status}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Created**: {task.CreatedUtc:u}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Last run**: {(task.LastRunUtc.HasValue ? task.LastRunUtc.Value.ToString("u", CultureInfo.InvariantCulture) : "never")}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Total runs**: {task.RunCount}");

            if (!string.IsNullOrEmpty(task.Description))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Description**: {task.Description}");

            return sb.ToString();
        }
    }

    private sealed class ScheduledTask
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Schedule { get; set; } = "";
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "active";
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastRunUtc { get; set; }
        public int RunCount { get; set; }
    }
}
