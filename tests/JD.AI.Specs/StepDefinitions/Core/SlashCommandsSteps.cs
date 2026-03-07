using FluentAssertions;
using JD.AI.Core.Commands;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class SlashCommandsSteps
{
    private readonly ScenarioContext _context;

    public SlashCommandsSteps(ScenarioContext context) => _context = context;

    [Given(@"a command registry")]
    public void GivenACommandRegistry()
    {
        _context.Set(new CommandRegistry(), "CommandRegistry");
    }

    [Given(@"a registered command ""(.*)"" with description ""(.*)""")]
    public void GivenARegisteredCommand(string name, string description)
    {
        var registry = _context.Get<CommandRegistry>("CommandRegistry");
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns(name);
        command.Description.Returns(description);
        command.Parameters.Returns([]);
        registry.Register(command);
    }

    [When(@"I look up command ""(.*)""")]
    public void WhenILookUpCommand(string name)
    {
        var registry = _context.Get<CommandRegistry>("CommandRegistry");
        var command = registry.GetCommand(name);
        _context.Set(command, "FoundCommand");
    }

    [When(@"I list all commands")]
    public void WhenIListAllCommands()
    {
        var registry = _context.Get<CommandRegistry>("CommandRegistry");
        _context.Set(registry.Commands, "CommandList");
    }

    [Then(@"the command should be found")]
    public void ThenTheCommandShouldBeFound()
    {
        var command = _context.Get<IChannelCommand?>("FoundCommand");
        command.Should().NotBeNull();
    }

    [Then(@"the command should not be found")]
    public void ThenTheCommandShouldNotBeFound()
    {
        var command = _context.Get<IChannelCommand?>("FoundCommand");
        command.Should().BeNull();
    }

    [Then(@"the command description should be ""(.*)""")]
    public void ThenTheCommandDescriptionShouldBe(string expected)
    {
        var command = _context.Get<IChannelCommand?>("FoundCommand");
        command!.Description.Should().Be(expected);
    }

    [Then(@"I should see (\d+) commands")]
    public void ThenIShouldSeeCommands(int count)
    {
        var commands = _context.Get<IReadOnlyList<IChannelCommand>>("CommandList");
        commands.Should().HaveCount(count);
    }
}
