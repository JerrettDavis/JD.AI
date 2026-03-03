namespace JD.AI.Core.Questions;

/// <summary>
/// The result produced after the user completes (or cancels) an <see cref="AskQuestionsRequest"/>.
/// </summary>
public sealed class AskQuestionsResult
{
    /// <summary>Matches the <see cref="AskQuestionsRequest.Id"/> that triggered this result.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// True when the user confirmed and submitted all required answers;
    /// false when the flow was cancelled via Esc or Ctrl+C.
    /// </summary>
    public bool Completed { get; init; }

    /// <summary>
    /// Collected answers keyed by <see cref="Question.Key"/>.
    /// For <see cref="QuestionType.MultiSelect"/> answers are joined with a comma.
    /// Missing optional questions retain their <see cref="Question.DefaultValue"/> here.
    /// </summary>
    public IReadOnlyDictionary<string, string> Answers { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
