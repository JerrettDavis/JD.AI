using Spectre.Console;

namespace JD.AI.Tui;

/// <summary>
/// Renders update notifications and handles the interactive update flow.
/// </summary>
public static class UpdatePrompter
{
    /// <summary>
    /// Shows an update notification and optionally runs the update.
    /// Returns true if the user updated and should restart.
    /// </summary>
    public static async Task<bool> PromptAsync(UpdateInfo info, CancellationToken ct = default)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
            new Markup(
                $"[bold yellow]Update available![/] " +
                $"[dim]{Markup.Escape(info.CurrentVersion)}[/] → " +
                $"[bold green]{Markup.Escape(info.LatestVersion)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Header("[bold yellow]⬆ jdai[/]")
            .Padding(1, 0));

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Run [bold]dotnet tool update -g JD.AI.Tui[/] to update.[/]");
            return false;
        }

        var shouldUpdate = AnsiConsole.Confirm(
            "[yellow]Update now?[/]", defaultValue: true);

        if (!shouldUpdate) return false;

        var (success, output) = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Updating jdai...", async _ =>
                await UpdateChecker.ApplyUpdateAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (success)
        {
            AnsiConsole.MarkupLine("[green]✓ Updated successfully![/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(output)}[/]");
            AnsiConsole.MarkupLine("[bold yellow]Please restart jdai to use the new version.[/]");
            return true;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Update failed:[/] {Markup.Escape(output)}");
            return false;
        }
    }

    /// <summary>
    /// Non-interactive notification (used after background check completes).
    /// Returns the formatted message for display.
    /// </summary>
    public static string FormatNotification(UpdateInfo info) =>
        $"[yellow]⬆ Update available: {Markup.Escape(info.CurrentVersion)} → " +
        $"[bold]{Markup.Escape(info.LatestVersion)}[/]. Type [bold]/update[/] to update.[/]";
}
