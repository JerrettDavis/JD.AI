using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class NotebookToolsSteps
{
    private readonly ScenarioContext _context;

    public NotebookToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I execute bash code ""(.*)""")]
    public async Task WhenIExecuteBashCode(string code)
    {
        var result = await NotebookTools.ExecuteCodeAsync("bash", code);
        _context.Set(result, "NotebookResult");
    }

    [When(@"I execute powershell code ""(.*)""")]
    public async Task WhenIExecutePowershellCode(string code)
    {
        var result = await NotebookTools.ExecuteCodeAsync("powershell", code);
        _context.Set(result, "NotebookResult");
    }

    [When(@"I execute ""(.*)"" code ""(.*)""")]
    public async Task WhenIExecuteCode(string language, string code)
    {
        var result = await NotebookTools.ExecuteCodeAsync(language, code);
        _context.Set(result, "NotebookResult");
    }

    [Then(@"the notebook result should contain ""(.*)""")]
    public void ThenTheNotebookResultShouldContain(string expected)
    {
        var result = _context.Get<string>("NotebookResult");
        result.Should().Contain(expected);
    }

    [Then(@"the notebook result should not contain ""(.*)""")]
    public void ThenTheNotebookResultShouldNotContain(string expected)
    {
        var result = _context.Get<string>("NotebookResult");
        result.Should().NotContain(expected);
    }
}
