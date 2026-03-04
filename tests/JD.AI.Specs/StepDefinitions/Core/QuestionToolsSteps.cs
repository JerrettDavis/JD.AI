using FluentAssertions;
using JD.AI.Core.Questions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class QuestionToolsSteps
{
    private readonly ScenarioContext _context;

    public QuestionToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a questionnaire runner that returns answers:")]
    public void GivenAQuestionnaireRunnerThatReturnsAnswers(Table table)
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            answers[row["key"]] = row["answer"];
        }

        Func<AskQuestionsRequest, AskQuestionsResult> runner = _ => new AskQuestionsResult
        {
            Id = Guid.NewGuid().ToString("N"),
            Completed = true,
            Answers = answers,
        };

        _context.Set(new QuestionTools(runner), "QuestionTools");
    }

    [Given(@"a questionnaire runner that cancels")]
    public void GivenAQuestionnaireRunnerThatCancels()
    {
        Func<AskQuestionsRequest, AskQuestionsResult> runner = _ => new AskQuestionsResult
        {
            Id = Guid.NewGuid().ToString("N"),
            Completed = false,
            Answers = new Dictionary<string, string>(StringComparer.Ordinal),
        };

        _context.Set(new QuestionTools(runner), "QuestionTools");
    }

    [When(@"I ask questions with title ""(.*)"":")]
    public void WhenIAskQuestionsWithTitle(string title, string questionsJson)
    {
        var tools = _context.Get<QuestionTools>("QuestionTools");
        var result = tools.AskQuestions(title, questionsJson);
        _context.Set(result, "QuestionResult");
    }

    [Then(@"the question result should contain ""([^""]+)""")]
    public void ThenTheQuestionResultShouldContain(string expected)
    {
        var result = _context.Get<string>("QuestionResult");
        result.Should().Contain(expected);
    }

    [Then(@"the question result should indicate cancellation")]
    public void ThenTheQuestionResultShouldIndicateCancellation()
    {
        var result = _context.Get<string>("QuestionResult");
        // JSON output has "completed":false when cancelled
        result.Should().Contain("\"completed\":false");
    }
}
