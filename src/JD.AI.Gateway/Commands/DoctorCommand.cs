using System.Reflection;
using System.Text;
using JD.AI.Core.Commands;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Gateway.Commands;

/// <summary>
/// <c>/doctor</c> command — runs all registered health checks and displays the
/// results in a human-readable summary. Surfaces provider reachability, session
/// store status, disk space, and memory usage.
/// </summary>
public sealed class DoctorCommand(HealthCheckService healthCheckService) : IChannelCommand
{
    public string Name => "doctor";
    public string Description => "Runs health checks and displays the system diagnostic report.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var report = await healthCheckService.CheckHealthAsync(ct).ConfigureAwait(false);

        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "unknown";

        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        var sb = new StringBuilder();
        sb.AppendLine("**=== JD.AI Doctor ===**");
        sb.AppendLine($"Version:  {version}");
        sb.AppendLine($"Runtime:  {runtime}");
        sb.AppendLine($"Health:   {FormatOverallStatus(report.Status)}");
        sb.AppendLine();
        sb.AppendLine("**Checks:**");

        foreach (var (name, entry) in report.Entries)
        {
            var icon = entry.Status switch
            {
                HealthStatus.Healthy => "✔",
                HealthStatus.Degraded => "⚠",
                _ => "✘",
            };
            var label = ToTitleCase(name.Replace('_', ' '));
            sb.Append($"  {icon} **{label}**");

            if (!string.IsNullOrEmpty(entry.Description))
                sb.Append($" — {entry.Description}");

            sb.AppendLine();

            if (entry.Exception is not null)
                sb.AppendLine($"    ⤷ {entry.Exception.Message}");
        }

        return new CommandResult { Success = true, Content = sb.ToString() };
    }

    private static string FormatOverallStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "✔ Healthy",
        HealthStatus.Degraded => "⚠ Degraded",
        _ => "✘ Unhealthy",
    };

    private static string ToTitleCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
