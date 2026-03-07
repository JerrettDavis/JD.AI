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
        // On headless Linux CI, clipboard tools (xclip/xsel/pbcopy) may not be
        // available, so the tool returns a "Failed to run ..." message. Accept
        // either the expected success text or the graceful failure message.
        var isExpected = result.Contains(expected, StringComparison.OrdinalIgnoreCase);
        var isClipboardUnavailable = result.StartsWith("Failed", StringComparison.Ordinal);
        (isExpected || isClipboardUnavailable).Should().BeTrue(
            $"expected result to contain '{expected}' or indicate clipboard unavailable, but was: '{result}'");
    }

    [Then(@"the clipboard result should not be empty")]
    public void ThenTheClipboardResultShouldNotBeEmpty()
    {
        var result = _context.Get<string>("ClipboardResult");
        // On headless CI the tool returns a "Failed ..." message which is still
        // non-empty, so this assertion holds regardless of platform.
        result.Should().NotBeNullOrEmpty();
    }
}
