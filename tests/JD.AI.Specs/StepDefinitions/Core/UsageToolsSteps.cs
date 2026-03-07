using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class UsageToolsSteps
{
    private readonly ScenarioContext _context;

    public UsageToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a new usage tracker")]
    public void GivenANewUsageTracker()
    {
        _context.Set(new UsageTools(), "UsageTools");
    }

    [Given(@"I have recorded (\d+) prompt tokens and (\d+) completion tokens with (\d+) tool calls")]
    public void GivenIHaveRecordedUsage(long promptTokens, long completionTokens, int toolCalls)
    {
        var tools = _context.Get<UsageTools>("UsageTools");
        tools.RecordUsage(promptTokens, completionTokens, toolCalls);
    }

    [When(@"I get usage statistics")]
    public void WhenIGetUsageStatistics()
    {
        var tools = _context.Get<UsageTools>("UsageTools");
        var result = tools.GetUsage();
        _context.Set(result, "UsageResult");
    }

    [When(@"I reset usage")]
    public void WhenIResetUsage()
    {
        var tools = _context.Get<UsageTools>("UsageTools");
        var result = tools.ResetUsage();
        _context.Set(result, "UsageResult");
    }

    [Then(@"the usage result should contain ""(.*)""")]
    public void ThenTheUsageResultShouldContain(string expected)
    {
        var result = _context.Get<string>("UsageResult");
        result.Should().Contain(expected);
    }

    [Then(@"the usage result should be ""(.*)""")]
    public void ThenTheUsageResultShouldBe(string expected)
    {
        var result = _context.Get<string>("UsageResult");
        result.Should().Be(expected);
    }
}
