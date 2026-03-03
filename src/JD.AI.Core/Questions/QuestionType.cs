namespace JD.AI.Core.Questions;

/// <summary>
/// The type of input expected for a structured question.
/// </summary>
public enum QuestionType
{
    /// <summary>Free-text string input.</summary>
    Text,

    /// <summary>Yes/no confirmation.</summary>
    Confirm,

    /// <summary>Single choice from a list of options.</summary>
    SingleSelect,

    /// <summary>Multiple choices from a list of options.</summary>
    MultiSelect,

    /// <summary>Numeric input.</summary>
    Number,
}
