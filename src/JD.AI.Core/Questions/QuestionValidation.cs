namespace JD.AI.Core.Questions;

/// <summary>
/// Optional validation rules applied to a <see cref="Question"/> answer before acceptance.
/// </summary>
public sealed class QuestionValidation
{
    /// <summary>
    /// Regular-expression pattern the answer must match (for <see cref="QuestionType.Text"/>).
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>Maximum number of characters allowed (for <see cref="QuestionType.Text"/>).</summary>
    public int? MaxLength { get; init; }

    /// <summary>Minimum numeric value (for <see cref="QuestionType.Number"/>).</summary>
    public double? Min { get; init; }

    /// <summary>Maximum numeric value (for <see cref="QuestionType.Number"/>).</summary>
    public double? Max { get; init; }

    /// <summary>Human-readable description of the validation rule shown on failure.</summary>
    public string? ErrorMessage { get; init; }
}
