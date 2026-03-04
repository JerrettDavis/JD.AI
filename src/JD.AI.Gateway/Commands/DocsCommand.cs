using System.Text;
using JD.AI.Core.Commands;

namespace JD.AI.Gateway.Commands;

/// <summary>
/// <c>/docs [topic]</c> command — returns links to JD.AI documentation.
/// Without a topic, lists all major documentation sections.
/// With a topic, shows the most relevant links for that topic.
/// </summary>
public sealed class DocsCommand : IChannelCommand
{
    private const string BaseUrl = "https://jerrettdavis.github.io/JD.AI/articles/";

    /// <summary>Known documentation topics with their article slug and description.</summary>
    private static readonly (string Key, string Slug, string Title, string Summary)[] Topics =
    [
        ("observability", "observability",  "Observability",       "OpenTelemetry tracing, metrics, health checks, and /doctor"),
        ("health",        "observability",  "Health Checks",       "Health endpoints (/health/ready, /health/live) and check configuration"),
        ("telemetry",     "observability",  "Telemetry",           "ActivitySource traces, metrics instruments, and OTel exporters"),
        ("gateway",       "gateway-api",    "Gateway API",         "REST endpoints, SignalR hubs, authentication, and rate limiting"),
        ("config",        "configuration",  "Configuration",       "appsettings.json, environment variables, and instruction files"),
        ("providers",     "providers",      "Providers",           "Claude Code, GitHub Copilot, Ollama, and Codex setup"),
        ("channels",      "channels",       "Channel Adapters",    "Discord, Slack, Signal, Telegram, and WebChat setup"),
        ("commands",      "commands-reference", "Commands Reference", "All slash commands and gateway commands"),
        ("deployment",    "service-deployment", "Service Deployment", "Running JD.AI as a Windows Service or systemd unit"),
        ("plugins",       "plugins",        "Plugin SDK",          "Building and loading custom plugins"),
        ("local",         "local-models",   "Local Models",        "GGUF models via LLamaSharp"),
        ("quickstart",    "quickstart",     "Quickstart",          "Get up and running in 5 minutes"),
    ];

    public string Name => "docs";
    public string Description => "Shows links to JD.AI documentation. Optionally filter by topic.";

    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "topic",
            Description = "Documentation topic to look up (e.g. observability, health, gateway, config). Omit to list all.",
            IsRequired = false,
            Type = CommandParameterType.Text,
            Choices = Topics.Select(t => t.Key).ToList(),
        }
    ];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        context.Arguments.TryGetValue("topic", out var topicArg);

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(topicArg))
        {
            var matches = Topics
                .Where(t => t.Key.Contains(topicArg, StringComparison.OrdinalIgnoreCase)
                         || t.Title.Contains(topicArg, StringComparison.OrdinalIgnoreCase)
                         || t.Summary.Contains(topicArg, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                sb.AppendLine($"No documentation found for **{topicArg}**.");
                sb.AppendLine();
                sb.AppendLine($"Run **/docs** (without a topic) to list all available sections.");
                return Task.FromResult(new CommandResult { Success = false, Content = sb.ToString() });
            }

            sb.AppendLine($"**JD.AI Docs — {topicArg}**");
            sb.AppendLine();
            foreach (var (_, slug, title, summary) in matches)
            {
                sb.AppendLine($"• **[{title}]({BaseUrl}{slug}.html)** — {summary}");
            }
        }
        else
        {
            sb.AppendLine("**JD.AI Documentation**");
            sb.AppendLine($"<{BaseUrl}>");
            sb.AppendLine();
            sb.AppendLine("**Sections:**");
            foreach (var (_, slug, title, summary) in Topics)
            {
                sb.AppendLine($"• **[{title}]({BaseUrl}{slug}.html)** — {summary}");
            }

            sb.AppendLine();
            sb.AppendLine("Use **/docs <topic>** to filter (e.g. `/docs observability`).");
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }
}
