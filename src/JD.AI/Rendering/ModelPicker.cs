using System.Diagnostics.CodeAnalysis;
using JD.AI.Core.Providers;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace JD.AI.Rendering;

/// <summary>
/// Interactive model selection using Spectre.Console selection prompt.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ModelPicker
{
    private const int FilterActivationThreshold = 20;
    private const int PageSize = 20;

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

        var ordered = OrderModels(models, currentModel);

        if (ordered.Count > FilterActivationThreshold &&
            !Console.IsInputRedirected &&
            !Console.IsOutputRedirected)
        {
            return PickWithFilter(ordered, currentModel);
        }

        return PickWithSelectionPrompt(ordered, currentModel);
    }

    private static ProviderModelInfo? PickWithSelectionPrompt(
        List<ProviderModelInfo> models,
        ProviderModelInfo? currentModel)
    {
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

    private static ProviderModelInfo? PickWithFilter(
        List<ProviderModelInfo> models,
        ProviderModelInfo? currentModel)
    {
        var filter = string.Empty;
        var selectedIndex = 0;
        ProviderModelInfo? selected = null;
        var done = false;

        try
        {
            AnsiConsole.Live(RenderFilterView(models, filter, selectedIndex, currentModel))
                .Overflow(VerticalOverflow.Crop)
                .Start(ctx =>
                {
                    while (!done)
                    {
                        var filtered = ApplyFilter(models, filter);
                        if (filtered.Count == 0)
                        {
                            selectedIndex = 0;
                        }
                        else
                        {
                            selectedIndex = Math.Clamp(selectedIndex, 0, filtered.Count - 1);
                        }

                        var key = Console.ReadKey(intercept: true);
                        switch (key.Key)
                        {
                            case ConsoleKey.UpArrow:
                                if (filtered.Count > 0)
                                    selectedIndex = (selectedIndex - 1 + filtered.Count) % filtered.Count;
                                break;
                            case ConsoleKey.DownArrow:
                                if (filtered.Count > 0)
                                    selectedIndex = (selectedIndex + 1) % filtered.Count;
                                break;
                            case ConsoleKey.PageUp:
                                selectedIndex = Math.Max(0, selectedIndex - PageSize);
                                break;
                            case ConsoleKey.PageDown:
                                if (filtered.Count > 0)
                                    selectedIndex = Math.Min(filtered.Count - 1, selectedIndex + PageSize);
                                break;
                            case ConsoleKey.Backspace:
                                if (filter.Length > 0)
                                {
                                    filter = filter[..^1];
                                    selectedIndex = 0;
                                }
                                break;
                            case ConsoleKey.Escape:
                                if (filter.Length > 0)
                                {
                                    filter = string.Empty;
                                    selectedIndex = 0;
                                }
                                else
                                {
                                    done = true;
                                }
                                break;
                            case ConsoleKey.Enter:
                                if (filtered.Count > 0)
                                {
                                    selected = filtered[selectedIndex];
                                    done = true;
                                }
                                break;
                            default:
                                if (!char.IsControl(key.KeyChar))
                                {
                                    filter += key.KeyChar;
                                    selectedIndex = 0;
                                }
                                break;
                        }

                        if (!done)
                        {
                            var clamped = ApplyFilter(models, filter).Count == 0 ? 0 : selectedIndex;
                            ctx.UpdateTarget(RenderFilterView(models, filter, clamped, currentModel));
                            ctx.Refresh();
                        }
                    }
                });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return selected;
    }

    private static IReadOnlyList<ProviderModelInfo> ApplyFilter(
        IReadOnlyList<ProviderModelInfo> models,
        string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return models;

        return models
            .Where(m =>
                m.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                m.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                m.ProviderName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static Panel RenderFilterView(
        IReadOnlyList<ProviderModelInfo> allModels,
        string filter,
        int selectedIndex,
        ProviderModelInfo? currentModel)
    {
        var filtered = ApplyFilter(allModels, filter);
        if (filtered.Count > 0)
            selectedIndex = Math.Clamp(selectedIndex, 0, filtered.Count - 1);
        else
            selectedIndex = 0;

        var start = filtered.Count <= PageSize
            ? 0
            : Math.Clamp(selectedIndex - (PageSize / 2), 0, filtered.Count - PageSize);

        var visible = filtered.Skip(start).Take(PageSize).ToList();
        var previousCount = start;
        var moreCount = Math.Max(0, filtered.Count - (start + visible.Count));

        var rows = new List<IRenderable>
        {
            new Markup("[bold]Select a model[/] [dim](type to filter, ↑/↓ move, Enter select, Esc clear/cancel)[/]"),
            new Markup($"[grey]Filter:[/] [yellow]{Markup.Escape(filter)}[/]"),
            new Markup($"[dim]({filtered.Count} models matching)[/]"),
        };

        if (filtered.Count == 0)
        {
            rows.Add(new Markup("[yellow]No models found — try a different search term.[/]"));
            return new Panel(new Rows(rows)).Border(BoxBorder.Rounded);
        }

        if (previousCount > 0)
            rows.Add(new Markup($"[dim]({previousCount} previous {Pluralize("model", previousCount)})[/]"));

        for (var i = 0; i < visible.Count; i++)
        {
            var model = visible[i];
            var absoluteIndex = start + i;
            var isSelected = absoluteIndex == selectedIndex;
            var isActive = currentModel is not null &&
                           string.Equals(model.Id, currentModel.Id, StringComparison.Ordinal);
            var prefix = isSelected ? "[aqua]>[/]" : " ";
            var badge = model.Capabilities.ToBadge();
            var active = isActive ? " [green](active)[/]" : string.Empty;
            var line = $"{prefix} {badge} [dim][[{Markup.Escape(model.ProviderName)}]][/] {Markup.Escape(model.DisplayName)}{active}";
            rows.Add(new Markup(line));
        }

        if (moreCount > 0)
            rows.Add(new Markup($"[dim]({moreCount} more {Pluralize("model", moreCount)})[/]"));

        return new Panel(new Rows(rows)).Border(BoxBorder.Rounded);
    }

    private static List<ProviderModelInfo> OrderModels(
        IReadOnlyList<ProviderModelInfo> models,
        ProviderModelInfo? currentModel)
    {
        var grouped = models
            .GroupBy(m => m.ProviderName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currentModel != null)
        {
            var currentGroup = grouped.FirstOrDefault(g =>
                string.Equals(g.Key, currentModel.ProviderName, StringComparison.Ordinal));

            if (currentGroup != null)
            {
                grouped.Remove(currentGroup);
                grouped.Insert(0, currentGroup);
            }
        }

        return grouped
            .SelectMany(g => g.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static string Pluralize(string noun, int count) =>
        count == 1 ? noun : noun + "s";
}
