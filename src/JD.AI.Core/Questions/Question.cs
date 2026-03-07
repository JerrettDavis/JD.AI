namespace JD.AI.Core.Questions;

/// <summary>
/// A single question within an <see cref="AskQuestionsRequest"/>.
/// </summary>
public sealed class Question
{
    /// <summary>Stable identifier used as the key in <see cref="AskQuestionsResult.Answers"/>.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Text shown to the user as the question prompt.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>Input type that determines how the TUI renders and captures the answer.</summary>
    public QuestionType Type { get; init; } = QuestionType.Text;

    /// <summary>When true the user must provide a non-empty answer before proceeding.</summary>
    public bool Required { get; init; }

    /// <summary>
    /// Pre-filled value shown to the user. If the user submits without typing, the default
    /// is used as the answer (only when <see cref="Required"/> is false or a default exists).
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Available options for <see cref="QuestionType.SingleSelect"/> and
    /// <see cref="QuestionType.MultiSelect"/> questions.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Optional validation rules applied before accepting the answer.</summary>
    public QuestionValidation? Validation { get; init; }
}
