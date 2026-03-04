using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class BatchEditToolsSteps
{
    private readonly ScenarioContext _context;

    public BatchEditToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I batch edit replacing ""(.*)"" with ""(.*)"" in ""(.*)""")]
    public void WhenIBatchEditReplacing(string oldText, string newText, string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = filePath, oldText, newText }
        });
        var result = BatchEditTools.BatchEditFiles(edits);
        _context.Set(result, "BatchResult");
    }

    [When(@"I batch edit with multiple replacements in ""(.*)"":")]
    public void WhenIBatchEditWithMultipleReplacements(string fileName, Table table)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var edits = table.Rows.Select(row => new
        {
            path = filePath,
            oldText = row["oldText"],
            newText = row["newText"]
        }).ToArray();
        var json = JsonSerializer.Serialize(edits);
        var result = BatchEditTools.BatchEditFiles(json);
        _context.Set(result, "BatchResult");
    }

    [When(@"I batch edit with an invalid replacement in ""(.*)""")]
    public void WhenIBatchEditWithInvalidReplacement(string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = Path.Combine(dir, "a.cs"), oldText = "original-a", newText = "changed-a" },
            new { path = Path.Combine(dir, fileName), oldText = "NOTFOUND", newText = "changed-b" }
        });
        var result = BatchEditTools.BatchEditFiles(edits);
        _context.Set(result, "BatchResult");
    }

    [When(@"I batch edit with no edits")]
    public void WhenIBatchEditWithNoEdits()
    {
        var result = BatchEditTools.BatchEditFiles("[]");
        _context.Set(result, "BatchResult");
    }

    [Then(@"the batch result should contain ""(.*)""")]
    public void ThenTheBatchResultShouldContain(string expected)
    {
        var result = _context.Get<string>("BatchResult");
        result.Should().Contain(expected);
    }

    [Then(@"the batch result should be ""(.*)""")]
    public void ThenTheBatchResultShouldBe(string expected)
    {
        var result = _context.Get<string>("BatchResult");
        result.Should().Be(expected);
    }
}
