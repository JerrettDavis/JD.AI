using JD.AI.Core.Mcp;
using JD.SemanticKernel.Extensions.Mcp;
using Spectre.Console;

namespace JD.AI.Startup;

/// <summary>
/// Installs selected curated MCP servers into the JD.AI-managed config,
/// prompting for any required environment variables or argument values first.
/// </summary>
internal static class McpInstaller
{
    /// <summary>
    /// Iterates <paramref name="selected"/> entries, collects required inputs from the user,
    /// then persists each server via the <paramref name="manager"/>.
    /// </summary>
    /// <returns>Number of servers successfully installed.</returns>
    public static async Task<int> InstallAsync(
        IReadOnlyList<CuratedMcpEntry> selected,
        McpManager manager,
        CancellationToken ct = default)
    {
        var installed = 0;

        foreach (var entry in selected)
        {
            AnsiConsole.MarkupLine($"\n[bold]Installing:[/] {Markup.Escape(entry.DisplayName)}");

            // Collect env vars ─────────────────────────────────────────────
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entry.RequiredEnvVars is { Count: > 0 })
            {
                foreach (var envVar in entry.RequiredEnvVars)
                {
                    // Check if the var is already set in the environment
                    var existing = Environment.GetEnvironmentVariable(envVar.Name);
                    if (!string.IsNullOrWhiteSpace(existing))
                    {
                        AnsiConsole.MarkupLine(
                            $"  [green]✓[/] Using existing env var [bold]{Markup.Escape(envVar.Name)}[/]");
                        env[envVar.Name] = existing;
                        continue;
                    }

                    string? value;
                    if (envVar.IsSecret)
                    {
                        value = AnsiConsole.Prompt(
                            new TextPrompt<string>($"  [bold]{Markup.Escape(envVar.Prompt)}[/]:")
                                .Secret('*')
                                .AllowEmpty()
                                .Validate(v => !string.IsNullOrWhiteSpace(v)
                                    ? ValidationResult.Success()
                                    : ValidationResult.Error("[red]Value cannot be empty.[/]")));
                    }
                    else
                    {
                        value = AnsiConsole.Prompt(
                            new TextPrompt<string>($"  [bold]{Markup.Escape(envVar.Prompt)}[/]:")
                                .AllowEmpty()
                                .Validate(v => !string.IsNullOrWhiteSpace(v)
                                    ? ValidationResult.Success()
                                    : ValidationResult.Error("[red]Value cannot be empty.[/]")));
                    }

                    env[envVar.Name] = value;
                }
            }

            // Collect arg prompts ───────────────────────────────────────────
            var args = entry.DefaultArgs?.ToList() ?? [];
            if (entry.PromptArgs is { Count: > 0 })
            {
                foreach (var argPrompt in entry.PromptArgs)
                {
                    var promptText = argPrompt.Example is not null
                        ? $"  [bold]{Markup.Escape(argPrompt.Prompt)}[/] [dim](e.g. {Markup.Escape(argPrompt.Example)})[/]:"
                        : $"  [bold]{Markup.Escape(argPrompt.Prompt)}[/]:";

                    var value = AnsiConsole.Prompt(
                        new TextPrompt<string>(promptText)
                            .AllowEmpty()
                            .Validate(v => !string.IsNullOrWhiteSpace(v)
                                ? ValidationResult.Success()
                                : ValidationResult.Error("[red]Value cannot be empty.[/]")));

                    // Replace placeholder in args list
                    var placeholder = $"{{{argPrompt.Placeholder}}}";
                    for (var i = 0; i < args.Count; i++)
                    {
                        if (string.Equals(args[i], placeholder, StringComparison.Ordinal))
                            args[i] = value;
                    }
                }
            }

            // Build McpServerDefinition ────────────────────────────────────
            McpServerDefinition definition;
            if (entry.Transport == CuratedMcpTransport.Http)
            {
                Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri);
                definition = new McpServerDefinition(
                    name: entry.Id,
                    displayName: entry.DisplayName,
                    transport: McpTransportType.Http,
                    scope: McpScope.User,
                    sourceProvider: "JD.AI",
                    sourcePath: null,
                    url: uri,
                    command: null,
                    args: null,
                    env: env.Count > 0 ? env : null,
                    isEnabled: true);
            }
            else
            {
                definition = new McpServerDefinition(
                    name: entry.Id,
                    displayName: entry.DisplayName,
                    transport: McpTransportType.Stdio,
                    scope: McpScope.User,
                    sourceProvider: "JD.AI",
                    sourcePath: null,
                    url: null,
                    command: entry.Command,
                    args: args.Count > 0 ? args : null,
                    env: env.Count > 0 ? env : null,
                    isEnabled: true);
            }

            try
            {
                await manager.AddOrUpdateAsync(definition, ct).ConfigureAwait(false);
                AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(entry.DisplayName)} installed");
                installed++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"  [red]✗[/] Failed to install {Markup.Escape(entry.DisplayName)}: {Markup.Escape(ex.Message)}");
            }
        }

        return installed;
    }
}
