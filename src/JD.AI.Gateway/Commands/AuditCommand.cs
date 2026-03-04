using System.Globalization;
using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Core.Governance.Audit;

namespace JD.AI.Gateway.Commands;

/// <summary>Shows recent audit events with optional severity filter.</summary>
public sealed class AuditCommand(IQueryableAuditSink store) : IChannelCommand
{
    public string Name => "audit";
    public string Description => "Shows recent audit events (use: /audit [severity])";

    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "severity",
            Description = "Minimum severity to show (debug/info/warning/error/critical)",
            IsRequired = false,
            Type = CommandParameterType.Text,
            Choices = ["debug", "info", "warning", "error", "critical"],
        },
        new CommandParameter
        {
            Name = "limit",
            Description = "Number of events to show (default 20)",
            IsRequired = false,
            Type = CommandParameterType.Number,
        },
    ];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        AuditSeverity? minSeverity = null;
        if (context.Arguments.TryGetValue("severity", out var sevStr) &&
            Enum.TryParse<AuditSeverity>(sevStr, ignoreCase: true, out var parsed))
        {
            minSeverity = parsed;
        }

        var limit = 20;
        if (context.Arguments.TryGetValue("limit", out var limitStr) &&
            int.TryParse(limitStr, CultureInfo.InvariantCulture, out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 100);
        }

        var result = await store.QueryAsync(new AuditQuery
        {
            MinSeverity = minSeverity,
            Limit = limit,
        }, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"**Audit Log** ({result.TotalCount} total events, showing {result.Events.Count})");
        sb.AppendLine();

        if (result.Events.Count == 0)
        {
            sb.AppendLine("No audit events found.");
        }
        else
        {
            foreach (var evt in result.Events)
            {
                var icon = evt.Severity switch
                {
                    AuditSeverity.Critical => "!!",
                    AuditSeverity.Error => "**",
                    AuditSeverity.Warning => "~~",
                    AuditSeverity.Info => "--",
                    _ => "..",
                };

                var time = evt.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                var resource = evt.Resource is not null ? $" [{evt.Resource}]" : "";
                sb.AppendLine($"`{icon}` `{time}` **{evt.Action}**{resource} — {evt.Severity}");
            }
        }

        return new CommandResult { Success = true, Content = sb.ToString() };
    }
}
