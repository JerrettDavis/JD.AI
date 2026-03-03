using System.Text.Json;
using JD.AI.Core.Questions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class QuestionToolsTests
{

    [Fact]
    public void AskQuestions_ValidJson_CallsRunnerAndReturnsResult()
    {
        var called = false;
        var tools = new QuestionTools(req =>
        {
            called = true;
            Assert.Equal("Test Title", req.Title);
            Assert.Equal("Some context", req.Context);
            Assert.Single(req.Questions);
            Assert.Equal("name", req.Questions[0].Key);
            return new AskQuestionsResult
            {
                Id = req.Id,
                Completed = true,
                Answers = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Alice" },
            };
        });

        var questionsJson = """[{"key":"name","prompt":"What is your name?","type":"text","required":true}]""";

        var resultJson = tools.AskQuestions("Test Title", questionsJson, context: "Some context");

        Assert.True(called);
        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.GetProperty("completed").GetBoolean());
        Assert.Equal("Alice", doc.RootElement.GetProperty("answers").GetProperty("name").GetString());
    }

    [Fact]
    public void AskQuestions_InvalidJson_ReturnsError()
    {
        var tools = new QuestionTools(_ => throw new InvalidOperationException("Should not be called"));

        var result = tools.AskQuestions("Title", "not valid json");

        Assert.Contains("error", result);
        Assert.Contains("Invalid questions JSON", result);
    }

    [Fact]
    public void AskQuestions_EmptyArray_ReturnsError()
    {
        var tools = new QuestionTools(_ => throw new InvalidOperationException("Should not be called"));

        var result = tools.AskQuestions("Title", "[]");

        Assert.Contains("error", result);
        Assert.Contains("No questions provided", result);
    }

    [Fact]
    public void AskQuestions_CancelledResult_ReturnsFalseCompleted()
    {
        var tools = new QuestionTools(req => new AskQuestionsResult
        {
            Id = req.Id,
            Completed = false,
        });

        var questionsJson = """[{"key":"q1","prompt":"Question?","type":"text"}]""";

        var resultJson = tools.AskQuestions("Title", questionsJson);

        using var doc = JsonDocument.Parse(resultJson);
        Assert.False(doc.RootElement.GetProperty("completed").GetBoolean());
    }

    [Fact]
    public void AskQuestions_AllowCancelFalse_PassesToRequest()
    {
        var tools = new QuestionTools(req =>
        {
            Assert.False(req.AllowCancel);
            return new AskQuestionsResult { Id = req.Id, Completed = true };
        });

        var questionsJson = """[{"key":"q1","prompt":"Question?","type":"text"}]""";

        tools.AskQuestions("Title", questionsJson, allowCancel: false);
    }

    [Fact]
    public void AskQuestions_MultipleQuestions_AllPassedToRunner()
    {
        var tools = new QuestionTools(req =>
        {
            Assert.Equal(3, req.Questions.Count);
            Assert.Equal("q1", req.Questions[0].Key);
            Assert.Equal("q2", req.Questions[1].Key);
            Assert.Equal("q3", req.Questions[2].Key);
            return new AskQuestionsResult
            {
                Id = req.Id,
                Completed = true,
                Answers = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["q1"] = "a1",
                    ["q2"] = "a2",
                    ["q3"] = "a3",
                },
            };
        });

        var questionsJson = """
            [
                {"key":"q1","prompt":"First?","type":"text"},
                {"key":"q2","prompt":"Second?","type":"confirm"},
                {"key":"q3","prompt":"Third?","type":"number"}
            ]
            """;

        var resultJson = tools.AskQuestions("Multi", questionsJson);

        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.GetProperty("completed").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("answers").EnumerateObject().Count());
    }

    [Fact]
    public void Constructor_NullDelegate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new QuestionTools(null!));
    }
}
