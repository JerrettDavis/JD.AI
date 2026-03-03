using JD.AI.Core.Questions;

namespace JD.AI.Tests;

public sealed class AskQuestionsTests
{
    // ── AskQuestionsRequest tests ────────────────────────────────────────────

    [Fact]
    public void AskQuestionsRequest_Defaults_AreCorrect()
    {
        var request = new AskQuestionsRequest();

        Assert.NotNull(request.Id);
        Assert.NotEmpty(request.Id);
        Assert.Equal("Input Required", request.Title);
        Assert.Null(request.Context);
        Assert.Empty(request.Questions);
        Assert.True(request.AllowCancel);
        Assert.Equal("Continue", request.SubmitLabel);
    }

    [Fact]
    public void AskQuestionsRequest_Id_IsUniquePerInstance()
    {
        var a = new AskQuestionsRequest();
        var b = new AskQuestionsRequest();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void AskQuestionsRequest_CanSetAllProperties()
    {
        var questions = new List<Question>
        {
            new() { Key = "q1", Prompt = "What is your name?", Type = QuestionType.Text, Required = true },
        };

        var request = new AskQuestionsRequest
        {
            Id = "custom-id",
            Title = "Gather Requirements",
            Context = "To scaffold the project correctly",
            Questions = questions,
            AllowCancel = false,
            SubmitLabel = "Generate",
        };

        Assert.Equal("custom-id", request.Id);
        Assert.Equal("Gather Requirements", request.Title);
        Assert.Equal("To scaffold the project correctly", request.Context);
        Assert.Single(request.Questions);
        Assert.False(request.AllowCancel);
        Assert.Equal("Generate", request.SubmitLabel);
    }

    // ── Question tests ───────────────────────────────────────────────────────

    [Fact]
    public void Question_Defaults_AreCorrect()
    {
        var q = new Question();

        Assert.Equal(string.Empty, q.Key);
        Assert.Equal(string.Empty, q.Prompt);
        Assert.Equal(QuestionType.Text, q.Type);
        Assert.False(q.Required);
        Assert.Null(q.DefaultValue);
        Assert.Empty(q.Options);
        Assert.Null(q.Validation);
    }

    [Theory]
    [InlineData(QuestionType.Text)]
    [InlineData(QuestionType.Confirm)]
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.MultiSelect)]
    [InlineData(QuestionType.Number)]
    public void Question_CanSetAllTypes(QuestionType type)
    {
        var q = new Question { Type = type };
        Assert.Equal(type, q.Type);
    }

    [Fact]
    public void Question_WithOptions_StoresOptions()
    {
        var options = new[] { "Option A", "Option B", "Option C" };
        var q = new Question
        {
            Key = "choice",
            Prompt = "Pick one",
            Type = QuestionType.SingleSelect,
            Options = options,
        };

        Assert.Equal(3, q.Options.Count);
        Assert.Equal("Option A", q.Options[0]);
        Assert.Equal("Option B", q.Options[1]);
        Assert.Equal("Option C", q.Options[2]);
    }

    // ── QuestionValidation tests ─────────────────────────────────────────────

    [Fact]
    public void QuestionValidation_Defaults_AreNull()
    {
        var v = new QuestionValidation();

        Assert.Null(v.Pattern);
        Assert.Null(v.MaxLength);
        Assert.Null(v.Min);
        Assert.Null(v.Max);
        Assert.Null(v.ErrorMessage);
    }

    [Fact]
    public void QuestionValidation_CanSetAllProperties()
    {
        var v = new QuestionValidation
        {
            Pattern = @"^\d{4}$",
            MaxLength = 100,
            Min = 1.0,
            Max = 999.0,
            ErrorMessage = "Invalid input",
        };

        Assert.Equal(@"^\d{4}$", v.Pattern);
        Assert.Equal(100, v.MaxLength);
        Assert.Equal(1.0, v.Min);
        Assert.Equal(999.0, v.Max);
        Assert.Equal("Invalid input", v.ErrorMessage);
    }

    // ── AskQuestionsResult tests ─────────────────────────────────────────────

    [Fact]
    public void AskQuestionsResult_Defaults_AreCorrect()
    {
        var result = new AskQuestionsResult();

        Assert.Equal(string.Empty, result.Id);
        Assert.False(result.Completed);
        Assert.Empty(result.Answers);
    }

    [Fact]
    public void AskQuestionsResult_CanSetAllProperties()
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "Alice",
            ["confirm"] = "yes",
        };

        var result = new AskQuestionsResult
        {
            Id = "abc123",
            Completed = true,
            Answers = answers,
        };

        Assert.Equal("abc123", result.Id);
        Assert.True(result.Completed);
        Assert.Equal(2, result.Answers.Count);
        Assert.Equal("Alice", result.Answers["name"]);
        Assert.Equal("yes", result.Answers["confirm"]);
    }

    [Fact]
    public void AskQuestionsResult_Answers_IsCaseSensitive()
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Key"] = "value1",
            ["key"] = "value2",
        };

        var result = new AskQuestionsResult { Answers = answers };

        Assert.Equal("value1", result.Answers["Key"]);
        Assert.Equal("value2", result.Answers["key"]);
    }

    // ── QuestionType enum coverage ───────────────────────────────────────────

    [Fact]
    public void QuestionType_HasExpectedValues()
    {
        var values = Enum.GetValues<QuestionType>();

        Assert.Contains(QuestionType.Text, values);
        Assert.Contains(QuestionType.Confirm, values);
        Assert.Contains(QuestionType.SingleSelect, values);
        Assert.Contains(QuestionType.MultiSelect, values);
        Assert.Contains(QuestionType.Number, values);
        Assert.Equal(5, values.Length);
    }
}
