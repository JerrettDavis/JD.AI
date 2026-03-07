using System.ComponentModel;
using System.Text.Json;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Questions;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tool for presenting structured questions to the user and collecting validated answers.
/// </summary>
[ToolPlugin("questions", RequiresInjection = true)]
public sealed class QuestionTools
{
    private readonly Func<AskQuestionsRequest, AskQuestionsResult> _runQuestionnaire;

    /// <summary>
    /// Initializes a new instance with the specified questionnaire runner.
    /// </summary>
    /// <param name="runQuestionnaire">Delegate that drives the interactive TUI questionnaire.</param>
    public QuestionTools(Func<AskQuestionsRequest, AskQuestionsResult> runQuestionnaire)
    {
        _runQuestionnaire = runQuestionnaire ?? throw new ArgumentNullException(nameof(runQuestionnaire));
    }

    [KernelFunction("ask_questions")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Present a structured set of questions to the user and collect their answers. " +
        "Use this when you need specific information from the user before proceeding — " +
        "requirements gathering, configuration choices, confirmations, etc. " +
        "Returns a JSON object with the user's answers keyed by each question's key, " +
        "or indicates cancellation if the user aborted.")]
    public string AskQuestions(
        [Description("Short title for the questionnaire panel (e.g., 'Need a bit more info')")] string title,
        [Description(
            "JSON array of question objects. Each object must have: " +
            "\"key\" (stable identifier), \"prompt\" (text shown to user), \"type\" (Text|Confirm|SingleSelect|MultiSelect|Number). " +
            "Optional: \"required\" (bool), \"defaultValue\" (string), \"options\" (string array for selects), " +
            "\"validation\" (object with optional pattern, maxLength, min, max, errorMessage).")]
        string questionsJson,
        [Description("Optional rationale shown to the user explaining why these questions are needed")] string? context = null,
        [Description("Whether the user can cancel the questionnaire (default: true)")] bool allowCancel = true)
    {
        List<Question> questions;
        try
        {
            questions = JsonSerializer.Deserialize<List<Question>>(questionsJson, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid questions JSON: {ex.Message}" });
        }

        if (questions.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No questions provided." });
        }

        var request = new AskQuestionsRequest
        {
            Title = title,
            Context = context,
            Questions = questions,
            AllowCancel = allowCancel,
        };

        var result = _runQuestionnaire(request);

        return JsonSerializer.Serialize(new
        {
            id = result.Id,
            completed = result.Completed,
            answers = result.Answers,
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Compact;
}
