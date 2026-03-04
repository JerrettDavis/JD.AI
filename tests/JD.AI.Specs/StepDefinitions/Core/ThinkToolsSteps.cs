using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ThinkToolsSteps
{
    private readonly ScenarioContext _context;

    public ThinkToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I think ""(.*)""")]
    public void WhenIThink(string thought)
    {
        var result = ThinkTools.Think(thought);
        _context.Set(result, "ThinkResult");
    }

    [When(@"I think:")]
    public void WhenIThinkMultiline(string thought)
    {
        var result = ThinkTools.Think(thought);
        _context.Set(result, "ThinkResult");
    }

    [Then(@"the think result should contain ""(.*)""")]
    public void ThenTheThinkResultShouldContain(string expected)
    {
        var result = _context.Get<string>("ThinkResult");
        result.Should().Contain(expected);
    }
}
