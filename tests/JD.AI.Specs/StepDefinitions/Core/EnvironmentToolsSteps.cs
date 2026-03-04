using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class EnvironmentToolsSteps
{
    private readonly ScenarioContext _context;

    public EnvironmentToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I get environment info")]
    public async Task WhenIGetEnvironmentInfo()
    {
        var result = await EnvironmentTools.GetEnvironmentAsync(includeEnvVars: false);
        _context.Set(result, "EnvResult");
    }

    [When(@"I get environment info with env vars")]
    public async Task WhenIGetEnvironmentInfoWithEnvVars()
    {
        var result = await EnvironmentTools.GetEnvironmentAsync(includeEnvVars: true);
        _context.Set(result, "EnvResult");
    }

    [Then(@"the environment result should contain ""(.*)""")]
    public void ThenTheEnvironmentResultShouldContain(string expected)
    {
        var result = _context.Get<string>("EnvResult");
        result.Should().Contain(expected);
    }

    [Then(@"any variable containing ""(.*)"" should show ""(.*)""")]
    public void ThenAnyVariableContainingShouldShow(string varNamePart, string maskedValue)
    {
        var result = _context.Get<string>("EnvResult");
        // The environment tool masks variables containing KEY, SECRET, TOKEN, PASSWORD
        // We verify the masking logic by checking that the output contains "***" somewhere
        // if any such env vars exist
        var lines = result.Split('\n');
        var sensitiveLines = lines.Where(l =>
            l.Contains(varNamePart, StringComparison.OrdinalIgnoreCase) &&
            l.Contains('='));

        foreach (var line in sensitiveLines)
        {
            var value = line[(line.IndexOf('=') + 1)..].Trim();
            value.Should().Be(maskedValue);
        }
    }
}
