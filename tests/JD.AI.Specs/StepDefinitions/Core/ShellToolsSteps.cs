using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ShellToolsSteps
{
    private readonly ScenarioContext _context;

    public ShellToolsSteps(ScenarioContext context) => _context = context;

    [When(@"I run shell command ""(.*)""")]
    public async Task WhenIRunShellCommand(string command)
    {
        var result = await ShellTools.RunCommandAsync(command);
        _context.Set(result, "ShellResult");
    }

    [When(@"I run shell command ""(.*)"" in the temporary directory")]
    public async Task WhenIRunShellCommandInTempDir(string command)
    {
        var dir = _context.Get<string>("TempDir");
        var result = await ShellTools.RunCommandAsync(command, dir);
        _context.Set(result, "ShellResult");
    }

    [When(@"I run a command that produces very long output")]
    public async Task WhenIRunACommandWithLongOutput()
    {
        // Generate output longer than 10000 chars
        var command = OperatingSystem.IsWindows()
            ? "powershell -NoProfile -Command \"'X' * 15000\""
            : "python3 -c \"print('X' * 15000)\"";
        var result = await ShellTools.RunCommandAsync(command);
        _context.Set(result, "ShellResult");
    }

    [Then(@"the shell result should contain ""(.*)""")]
    public void ThenTheShellResultShouldContain(string expected)
    {
        var result = _context.Get<string>("ShellResult");
        result.Should().Contain(expected);
    }
}
