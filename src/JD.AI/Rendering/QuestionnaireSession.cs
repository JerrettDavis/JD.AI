using System.Diagnostics.CodeAnalysis;
using JD.AI.Core.Questions;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Runs an interactive questionnaire in the TUI terminal, presenting each question
/// in turn, collecting validated answers, showing a review/summary screen, and
/// returning a completed <see cref="AskQuestionsResult"/>.
/// </summary>
public sealed class QuestionnaireSession
{
    /// <summary>
    /// Presents the questionnaire described by <paramref name="request"/> and returns the result.
    /// Returns a cancelled result when the user presses Esc (and <see cref="AskQuestionsRequest.AllowCancel"/> is true)
    /// or when there are no questions to answer.
    /// </summary>
    public static AskQuestionsResult Run(AskQuestionsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Questions.Count == 0)
        {
            // No questions to ask — the request is vacuously complete.
            return new AskQuestionsResult { Id = request.Id, Completed = true };
        }

        RenderHeader(request);

        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        var total = request.Questions.Count;

        for (var i = 0; i < total; i++)
        {
            var question = request.Questions[i];

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Question {i + 1}/{total}[/]");

            var answer = AskQuestion(question, request.AllowCancel);

            if (answer is null)
            {
                // User cancelled
                AnsiConsole.MarkupLine("[yellow]✗ Questionnaire cancelled.[/]");
                AnsiConsole.WriteLine();
                return new AskQuestionsResult { Id = request.Id, Completed = false };
            }

            answers[question.Key] = answer;
        }

        // Review + confirm screen
        if (!ShowReviewScreen(request, answers))
        {
            AnsiConsole.MarkupLine("[yellow]✗ Questionnaire cancelled.[/]");
            AnsiConsole.WriteLine();
            return new AskQuestionsResult { Id = request.Id, Completed = false };
        }

        AnsiConsole.MarkupLine("[green]✓ Answers confirmed.[/]");
        AnsiConsole.WriteLine();

        return new AskQuestionsResult
        {
            Id = request.Id,
            Completed = true,
            Answers = answers,
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    [ExcludeFromCodeCoverage]
    private static void RenderHeader(AskQuestionsRequest request)
    {
        var content = new Markup($"[bold]{Markup.Escape(request.Title)}[/]" +
            (request.Context is { Length: > 0 }
                ? $"\n[dim]{Markup.Escape(request.Context)}[/]"
                : string.Empty));

        var panel = new Panel(content)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Asks a single question and returns the validated answer, or null if cancelled.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static string? AskQuestion(Question question, bool allowCancel)
    {
        var promptSuffix = question.Required ? " [red]*[/]" : " [dim](optional)[/]";
        if (question.DefaultValue is { Length: > 0 })
            promptSuffix += $" [dim](default: {Markup.Escape(question.DefaultValue)})[/]";

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(question.Prompt)}[/]{promptSuffix}");

        return question.Type switch
        {
            QuestionType.Confirm => AskConfirm(question, allowCancel),
            QuestionType.SingleSelect => AskSingleSelect(question, allowCancel),
            QuestionType.MultiSelect => AskMultiSelect(question, allowCancel),
            QuestionType.Number => AskNumber(question, allowCancel),
            _ => AskText(question, allowCancel),
        };
    }

    [ExcludeFromCodeCoverage]
    private static string? AskText(Question question, bool allowCancel)
    {
        while (true)
        {
            AnsiConsole.Markup("[bold green]>[/] ");
            var line = Console.ReadLine();

            if (line is null)
            {
                if (allowCancel) return null;
                if (!question.Required) return string.Empty;
                AnsiConsole.MarkupLine("[red]A value is required. Please try again.[/]");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (question.DefaultValue is { Length: > 0 })
                    return question.DefaultValue;

                if (!question.Required)
                    return string.Empty;

                AnsiConsole.MarkupLine("[red]A value is required. Please try again.[/]");
                continue;
            }

            var error = ValidateText(question.Validation, line);
            if (error is not null)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
                continue;
            }

            return line.Trim();
        }
    }

    [ExcludeFromCodeCoverage]
    private static string? AskNumber(Question question, bool allowCancel)
    {
        while (true)
        {
            AnsiConsole.Markup("[bold green]>[/] ");
            var line = Console.ReadLine();

            if (line is null)
            {
                if (allowCancel) return null;
                if (!question.Required) return string.Empty;
                AnsiConsole.MarkupLine("[red]A value is required. Please try again.[/]");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (question.DefaultValue is { Length: > 0 })
                    return question.DefaultValue;

                if (!question.Required)
                    return string.Empty;

                AnsiConsole.MarkupLine("[red]A value is required. Please try again.[/]");
                continue;
            }

            if (!double.TryParse(line.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                AnsiConsole.MarkupLine("[red]Please enter a valid number.[/]");
                continue;
            }

            var error = ValidateNumber(question.Validation, num);
            if (error is not null)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
                continue;
            }

            return line.Trim();
        }
    }

    [ExcludeFromCodeCoverage]
    private static string? AskConfirm(Question question, bool allowCancel)
    {
        var defaultYes = string.Equals(question.DefaultValue, "yes", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(question.DefaultValue, "y", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(question.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);

        var hint = defaultYes ? "[dim]([green]Y[/]/n)[/]" : "[dim](y/[red]N[/])[/]";
        AnsiConsole.Markup($"{hint} [bold green]>[/] ");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Y:
                    AnsiConsole.MarkupLine("[green]yes[/]");
                    return "yes";

                case ConsoleKey.N:
                    AnsiConsole.MarkupLine("[red]no[/]");
                    return "no";

                case ConsoleKey.Enter:
                    var def = defaultYes ? "yes" : "no";
                    AnsiConsole.MarkupLine($"[dim]{def}[/]");
                    return def;

                case ConsoleKey.Escape when allowCancel:
                    AnsiConsole.WriteLine();
                    return null;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private static string? AskSingleSelect(Question question, bool allowCancel)
    {
        if (question.Options.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](no options available)[/]");
            return question.DefaultValue ?? string.Empty;
        }

        var choices = question.Options.ToList();
        if (allowCancel)
            choices.Add("(cancel)");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .WithAdaptivePaging(preferredPageSize: 10, totalChoices: choices.Count, singularNoun: "option")
                .AddChoices(choices)
                .HighlightStyle(Style.Parse("blue bold"))
                .Title(string.Empty));

        if (allowCancel && string.Equals(selected, "(cancel)", StringComparison.Ordinal))
            return null;

        return selected;
    }

    [ExcludeFromCodeCoverage]
    private static string? AskMultiSelect(Question question, bool allowCancel)
    {
        if (question.Options.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](no options available)[/]");
            return question.DefaultValue ?? string.Empty;
        }

        var prompt = new MultiSelectionPrompt<string>()
            .WithAdaptivePaging(preferredPageSize: 10, totalChoices: question.Options.Count, singularNoun: "option")
            .Title(string.Empty)
            .InstructionsText("[dim](Space to toggle, Enter to confirm)[/]")
            .AddChoices(question.Options);

        // Pre-select defaults if provided (comma-separated)
        if (question.DefaultValue is { Length: > 0 })
        {
            var defaults = question.DefaultValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var d in defaults)
            {
                if (question.Options.Contains(d))
                    prompt.Select(d);
            }
        }

        if (!question.Required)
            prompt.NotRequired();

        try
        {
            var selections = AnsiConsole.Prompt(prompt);
            return string.Join(",", selections);
        }
        catch (OperationCanceledException)
        {
            return allowCancel ? null : string.Empty;
        }
    }

    /// <summary>
    /// Shows the summary screen and returns true if the user confirms, false if they cancel.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static bool ShowReviewScreen(AskQuestionsRequest request, Dictionary<string, string> answers)
    {
        while (true)
        {
            AnsiConsole.WriteLine();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap());
            grid.AddColumn();

            foreach (var question in request.Questions)
            {
                var answerCell = answers.TryGetValue(question.Key, out var a)
                    ? Markup.Escape(a)
                    : "[dim](no answer)[/]";
                grid.AddRow(
                    $"[dim]{Markup.Escape(question.Prompt)}[/]",
                    answerCell);
            }

            var summaryPanel = new Panel(grid)
                .Header("[bold]Review your answers[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green)
                .Padding(1, 0);

            AnsiConsole.Write(summaryPanel);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(string.Empty)
                    .AddChoices(request.SubmitLabel, "Edit answers", "Cancel"));

            if (string.Equals(choice, "Cancel", StringComparison.Ordinal))
                return false;

            if (string.Equals(choice, "Edit answers", StringComparison.Ordinal))
            {
                var labels = request.Questions.Select(q => $"{q.Key}: {q.Prompt}").ToList();
                var toEdit = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which question would you like to edit?")
                        .WithAdaptivePaging(preferredPageSize: 12, totalChoices: labels.Count, singularNoun: "question")
                        .AddChoices(labels));

                var selectedKey = toEdit.Split(':', 2)[0];
                var questionToEdit = request.Questions.FirstOrDefault(q =>
                    string.Equals(q.Key, selectedKey, StringComparison.Ordinal));

                if (questionToEdit is not null)
                {
                    AnsiConsole.WriteLine();
                    var newAnswer = AskQuestion(questionToEdit, allowCancel: true);
                    if (newAnswer is not null)
                        answers[questionToEdit.Key] = newAnswer;
                }

                continue; // Loop back to show updated review
            }

            return true;
        }
    }

    // ── Validation helpers ───────────────────────────────────────────────────

    internal static string? ValidateText(QuestionValidation? v, string value)
    {
        if (v is null) return null;

        if (v.MaxLength.HasValue && value.Length > v.MaxLength.Value)
            return v.ErrorMessage ?? $"Answer must be at most {v.MaxLength.Value} characters.";

        if (v.Pattern is { Length: > 0 })
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    value, v.Pattern, System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(2)))
                {
                    return v.ErrorMessage ?? $"Answer must match the pattern: {v.Pattern}";
                }
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                return "Validation timed out. Please try a simpler answer.";
            }
        }

        return null;
    }

    internal static string? ValidateNumber(QuestionValidation? v, double value)
    {
        if (v is null) return null;

        if (v.Min.HasValue && value < v.Min.Value)
            return v.ErrorMessage ?? $"Value must be at least {v.Min.Value}.";

        if (v.Max.HasValue && value > v.Max.Value)
            return v.ErrorMessage ?? $"Value must be at most {v.Max.Value}.";

        return null;
    }
}
