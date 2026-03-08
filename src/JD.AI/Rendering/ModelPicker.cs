using System.Diagnostics.CodeAnalysis;
using JD.AI.Core.Providers;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Interactive model selection using Spectre.Console selection prompt.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ModelPicker
{
    /// <summary>
    /// Displays an interactive scrollable model list and returns the selected model,
    /// or null if the user cancels or no models are available.
    /// </summary>
    public static ProviderModelInfo? Pick(
        IReadOnlyList<ProviderModelInfo> models,
        ProviderModelInfo? currentModel = null)
    {
        if (models.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No models available. Check provider authentication.[/]");
            return null;
        }

        // Group models by provider for readability
        var grouped = models
            .GroupBy(m => m.ProviderName)
            .OrderBy(g => g.Key)
            .ToList();

        var prompt = new SelectionPrompt<ProviderModelInfo>()
            .Title("[bold]Select a model[/] [dim](💬=Chat 🔧=Tools 👁=Vision 📐=Embed | ↑/↓ navigate, Enter select)[/]")
            .WithAdaptivePaging(preferredPageSize: 20, totalChoices: models.Count, singularNoun: "model")
            .HighlightStyle(new Style(Color.Aqua, decoration: Decoration.Bold))
            .UseConverter(m =>
            {
                var badge = m.Capabilities.ToBadge();
                var active = currentModel != null &&
                             string.Equals(m.Id, currentModel.Id, StringComparison.Ordinal)
                    ? " ◄ active"
                    : "";
                return $"{badge} [dim][[{Markup.Escape(m.ProviderName)}]][/] {Markup.Escape(m.DisplayName)}{active}";
            });

        // Add choices grouped by provider, placing current model's provider first
        if (currentModel != null)
        {
            var currentGroup = grouped.FirstOrDefault(g =>
                string.Equals(g.Key, currentModel.ProviderName, StringComparison.Ordinal));

            if (currentGroup != null)
            {
                // Reorder so the current model's group is first
                grouped.Remove(currentGroup);
                grouped.Insert(0, currentGroup);
            }
        }

        foreach (var group in grouped)
        {
            prompt.AddChoices(group);
        }

        try
        {
            return AnsiConsole.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
