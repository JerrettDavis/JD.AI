using JD.AI.Core.Channels;
using JD.AI.Core.Commands;

namespace JD.AI.Channels.Queue;

/// <summary>
/// Admin commands for inspecting and managing the durable queue.
/// Registered via <c>ICommandRegistry</c> alongside existing channel commands.
/// </summary>
public sealed class QueuePeekCommand(DiscordMessageBuffer queue) : IChannelCommand
{
    public string Name => "queue-peek";
    public string Description => "Shows pending and failed messages in the durable queue.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var stats = await queue.GetStatsAsync(ct);
        var lines = new List<string>
        {
            "**Queue Overview**",
            $"  Total:    {stats.Total}",
            $"  Pending:  {stats.Pending}",
            $"  Active:   {stats.Processing}",
            $"  Completed:{stats.Completed}",
            $"  Failed:   {stats.Failed}",
            ""
        };

        var pending = await queue.GetPendingMessagesAsync(10, ct);
        if (pending.Count > 0)
        {
            lines.Add("**Pending / In-Retry Messages (up to 10):**");
            foreach (var m in pending)
            {
                var status = m.Status switch
                {
                    QueueStatus.Pending    => "⏳ pending",
                    QueueStatus.Processing => "🔄 processing",
                    QueueStatus.Failed     => $"❌ failed ({m.AttemptCount} attempts)",
                    _                      => $"#{m.Status}"
                };
                var preview = m.Content.Length > 60 ? m.Content[..60] + "…" : m.Content;
                var retryInfo = m.NextRetryAfter != default
                    ? $" (retry after {m.NextRetryAfter:h:mm:ss})"
                    : "";
                lines.Add($"  [{m.RowId}] {status}{retryInfo}");
                lines.Add($"    From: {m.SenderDisplayName ?? m.SenderId} | {preview}");
            }
        }
        else
        {
            lines.Add("Queue is empty.");
        }

        return new CommandResult { Success = true, Content = string.Join("\n", lines) };
    }
}

public sealed class QueueRetryCommand(DiscordMessageBuffer queue) : IChannelCommand
{
    public string Name => "queue-retry";
    public string Description => "Retries a specific failed or pending message by row ID. Usage: queue-retry <row_id>";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new() { Name = "row_id", Description = "The row ID of the message to retry", IsRequired = true, Type = CommandParameterType.Number }
    ];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        if (!long.TryParse(context.Arguments.GetValueOrDefault("row_id"), out var rowId))
            return new CommandResult { Success = false, Content = "❌ Invalid row ID. Provide a numeric row ID from `queue-peek`." };

        try
        {
            await queue.ResetAsync(rowId, ct);
            return new CommandResult { Success = true, Content = $"✅ Message {rowId} has been requeued for immediate retry." };
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Content = $"❌ Failed to retry message {rowId}: {ex.Message}" };
        }
    }
}

public sealed class QueuePurgeCommand(DiscordMessageBuffer queue) : IChannelCommand
{
    public string Name => "queue-purge";
    public string Description =>
        "Purges completed messages older than the given duration. " +
        "Usage: queue-purge <duration> [include_failed] " +
        "Examples: queue-purge 7d, queue-purge 24h true";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new() { Name = "duration", Description = "Age threshold, e.g. 7d, 24h, 1h (default: 7d)", IsRequired = false, Type = CommandParameterType.Text },
        new() { Name = "include_failed", Description = "Also purge failed messages (default: false)", IsRequired = false, Type = CommandParameterType.Boolean }
    ];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var durationStr = context.Arguments.GetValueOrDefault("duration", "7d");
        var includeFailed = bool.TryParse(context.Arguments.GetValueOrDefault("include_failed"), out var v) && v;

        if (!TryParseDuration(durationStr, out var maxAge))
            return new CommandResult { Success = false, Content = $"❌ Unknown duration format: {durationStr}. Use something like 7d, 24h, 1h." };

        var deleted = await queue.PurgeAsync(maxAge, includeFailed, ct);
        var what = includeFailed ? "completed and failed" : "completed";
        return new CommandResult { Success = true, Content = $"🗑️ Purged {deleted} {what} messages older than {durationStr}." };
    }

    private static bool TryParseDuration(string s, out TimeSpan result)
    {
        result = default;
        if (string.IsNullOrEmpty(s)) return false;
        var lastChar = char.ToLowerInvariant(s[^1]);
        if (!char.IsDigit(lastChar)) s = s[..^1];
        if (!double.TryParse(s, out var value)) return false;
        result = lastChar switch
        {
            'd' => TimeSpan.FromDays(value),
            'h' => TimeSpan.FromHours(value),
            'm' => TimeSpan.FromMinutes(value),
            's' => TimeSpan.FromSeconds(value),
            _ => TimeSpan.FromDays(value) // bare number = days
        };
        return true;
    }
}
