using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class DiffToolsSteps
{
    private readonly ScenarioContext _context;

    public DiffToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I create a patch replacing ""(.*)"" with ""(.*)"" in ""(.*)""")]
    public void WhenICreateAPatch(string oldText, string newText, string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = filePath, oldText, newText }
        });
        var result = DiffTools.CreatePatch(edits);
        _context.Set(result, "PatchResult");
    }

    [When(@"I apply a patch replacing ""(.*)"" with ""(.*)"" in ""(.*)""")]
    public void WhenIApplyAPatch(string oldText, string newText, string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = filePath, oldText, newText }
        });
        var result = DiffTools.ApplyPatch(edits);
        _context.Set(result, "PatchResult");
    }

    [When(@"I apply a patch with edits that fail on ""(.*)""")]
    public void WhenIApplyAPatchWithFailingEdits(string failFileName)
    {
        var dir = _context.Get<string>("TempDir");
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = Path.Combine(dir, "a.cs"), oldText = "aaa", newText = "xxx" },
            new { path = Path.Combine(dir, failFileName), oldText = "NOTFOUND", newText = "yyy" }
        });
        var result = DiffTools.ApplyPatch(edits);
        _context.Set(result, "PatchResult");
    }

    [When(@"I apply a patch with a missing path field")]
    public void WhenIApplyAPatchWithMissingPath()
    {
        var edits = JsonSerializer.Serialize(new[]
        {
            new { path = (string?)null, oldText = "x", newText = "y" }
        });
        var result = DiffTools.ApplyPatch(edits);
        _context.Set(result, "PatchResult");
    }

    [Then(@"the patch result should contain ""(.*)""")]
    public void ThenThePatchResultShouldContain(string expected)
    {
        var result = _context.Get<string>("PatchResult");
        result.Should().Contain(expected);
    }
}
