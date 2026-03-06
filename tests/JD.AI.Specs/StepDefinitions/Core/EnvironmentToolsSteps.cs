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
        // Only match against variable names; values can legitimately contain "key"
        // (for example branch names) and should not influence this assertion.
        var sensitiveLines = result
            .Split('\n')
            .Select(line => line.Split('=', 2))
            .Where(parts =>
                parts.Length == 2 &&
                parts[0].Contains(varNamePart, StringComparison.OrdinalIgnoreCase))
            .Select(parts => new { Name = parts[0], Value = parts[1].Trim() });

        foreach (var line in sensitiveLines)
        {
            line.Value.Should().Be(maskedValue, $"variable '{line.Name}' should be masked");
        }
    }
}
