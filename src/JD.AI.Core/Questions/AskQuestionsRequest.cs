namespace JD.AI.Core.Questions;

/// <summary>
/// A structured request for the TUI to present a sequence of questions to the user
/// and collect their answers before resuming agent execution.
/// </summary>
public sealed class AskQuestionsRequest
{
    /// <summary>Unique identifier for correlating the request with its result.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];

    /// <summary>Short title displayed at the top of the questionnaire panel (e.g., "Need a bit more info").</summary>
    public string Title { get; init; } = "Input Required";

    /// <summary>
    /// Optional rationale shown below the title to help the user understand why these
    /// questions are being asked (e.g., "to choose the correct scaffold and avoid rework").
    /// </summary>
    public string? Context { get; init; }

    /// <summary>Ordered list of questions to present to the user.</summary>
    public IReadOnlyList<Question> Questions { get; init; } = [];

    /// <summary>When true an Esc/Cancel gesture is offered; false forces the user to answer.</summary>
    public bool AllowCancel { get; init; } = true;

    /// <summary>Label for the final submission button (defaults to "Continue").</summary>
    public string SubmitLabel { get; init; } = "Continue";
}
