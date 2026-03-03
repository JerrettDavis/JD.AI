using JD.AI.Core.Commands;

namespace JD.AI.Tests.Commands;

public class CommandRegistryTests
{
    [Fact]
    public void Register_AddsCommand()
    {
        var registry = new CommandRegistry();
        var command = new StubCommand("help", "Shows help");

        registry.Register(command);

        Assert.Single(registry.Commands);
        Assert.Equal("help", registry.Commands[0].Name);
    }

    [Fact]
    public void GetCommand_FindsByNameCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.Register(new StubCommand("Help", "Shows help"));

        var result = registry.GetCommand("HELP");

        Assert.NotNull(result);
        Assert.Equal("Help", result.Name);
    }

    [Fact]
    public void GetCommand_ReturnsNullForUnknown()
    {
        var registry = new CommandRegistry();

        Assert.Null(registry.GetCommand("nonexistent"));
    }

    [Fact]
    public void Register_ReplacesExistingCommand()
    {
        var registry = new CommandRegistry();
        registry.Register(new StubCommand("help", "v1"));
        registry.Register(new StubCommand("help", "v2"));

        Assert.Single(registry.Commands);
        Assert.Equal("v2", registry.Commands[0].Description);
    }

    [Fact]
    public void Commands_ReturnsSnapshot()
    {
        var registry = new CommandRegistry();
        registry.Register(new StubCommand("a", "A"));
        registry.Register(new StubCommand("b", "B"));

        var snapshot = registry.Commands;

        Assert.Equal(2, snapshot.Count);
    }

    private sealed class StubCommand(string name, string description) : IChannelCommand
    {
        public string Name => name;
        public string Description => description;
        public IReadOnlyList<CommandParameter> Parameters => [];

        public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult { Success = true, Content = "ok" });
    }
}
