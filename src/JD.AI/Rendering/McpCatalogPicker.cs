using System.Diagnostics.CodeAnalysis;
using JD.AI.Core.Mcp;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Interactive MCP server selection using Spectre.Console multi-selection.
/// Entries are displayed with their category as a prefix for visual grouping.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class McpCatalogPicker
{
    /// <summary>
    /// Shows the curated MCP catalog as a multi-selection prompt.
    /// Already-installed servers are pre-selected and labeled.
    /// </summary>
    /// <param name="catalog">Entries to show.</param>
    /// <param name="alreadyInstalled">Set of server IDs already present in the JD.AI config.</param>
    /// <param name="categoryFilter">Optional category name to restrict the displayed entries.</param>
    /// <returns>The entries the user selected, or empty if cancelled.</returns>
    public static IReadOnlyList<CuratedMcpEntry> Pick(
        IReadOnlyList<CuratedMcpEntry> catalog,
        IReadOnlySet<string> alreadyInstalled,
        string? categoryFilter = null)
    {
        var entries = (categoryFilter is null
            ? catalog
            : catalog.Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
                     .ToList())
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
        {
            var filterMsg = categoryFilter is null
                ? "The curated MCP catalog is empty."
                : $"No catalog entries found for category '{categoryFilter}'.";
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(filterMsg)}[/]");
            return [];
        }

        var prompt = new MultiSelectionPrompt<CuratedMcpEntry>()
            .Title("[bold]Select MCP servers to install[/] [dim](Space to toggle, Enter to confirm)[/]")
            .WithAdaptivePaging(preferredPageSize: 20, totalChoices: entries.Count, singularNoun: "server")
            .InstructionsText("[dim]<space> toggle · <enter> confirm · <a> all · <n> none[/]")
            .UseConverter(e =>
            {
                var installedBadge = alreadyInstalled.Contains(e.Id) ? " [dim][installed][/]" : string.Empty;
                return $"[dim]{Markup.Escape(e.Category),-18}[/] [bold]{Markup.Escape(e.DisplayName)}[/]{installedBadge} [dim]— {Markup.Escape(e.Description)}[/]";
            })
            .AddChoices(entries);

        // Pre-select already-installed entries
        foreach (var entry in entries.Where(e => alreadyInstalled.Contains(e.Id)))
            prompt.Select(entry);

        try
        {
            return AnsiConsole.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>
    /// Prompts the user to confirm whether to proceed with the MCP catalog step.
    /// Returns <c>false</c> if the user declines or the terminal is non-interactive.
    /// </summary>
    public static bool ConfirmCatalogStep()
    {
        try
        {
            return AnsiConsole.Confirm(
                "[bold]Would you like to install MCP servers?[/] [dim](adds integrations like GitHub, Azure DevOps, windows-mcp, and more)[/]",
                defaultValue: true);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
