using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ClipboardToolsSteps
{
    private readonly ScenarioContext _context;

    public ClipboardToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I write ""(.*)"" to the clipboard")]
    public async Task WhenIWriteToTheClipboard(string text)
    {
        var result = await ClipboardTools.WriteClipboardAsync(text);
        _context.Set(result, "ClipboardResult");
    }

    [When(@"I read the clipboard")]
    public async Task WhenIReadTheClipboard()
    {
        var result = await ClipboardTools.ReadClipboardAsync();
        _context.Set(result, "ClipboardResult");
    }

    [Then(@"the clipboard result should contain ""(.*)""")]
    public void ThenTheClipboardResultShouldContain(string expected)
    {
        var result = _context.Get<string>("ClipboardResult");
        result.Should().Contain(expected);
    }

    [Then(@"the clipboard result should not be empty")]
    public void ThenTheClipboardResultShouldNotBeEmpty()
    {
        var result = _context.Get<string>("ClipboardResult");
        result.Should().NotBeNullOrEmpty();
    }
}
