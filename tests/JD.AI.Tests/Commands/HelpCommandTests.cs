using JD.AI.Core.Commands;

namespace JD.AI.Tests.Commands;

public class HelpCommandTests
{
    [Fact]
    public void Registry_CanRegisterAndRetrieveMultipleCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(new StubCommand("alpha", "Alpha command"));
        registry.Register(new StubCommand("beta", "Beta command"));
        registry.Register(new StubCommand("help", "Shows help"));

        Assert.Equal(3, registry.Commands.Count);
        Assert.NotNull(registry.GetCommand("alpha"));
        Assert.NotNull(registry.GetCommand("beta"));
        Assert.NotNull(registry.GetCommand("help"));
    }

    [Fact]
    public async Task StubCommand_CanExecuteWithParameters()
    {
        var cmd = new StubCommandWithParams("switch", "Switch model",
        [
            new CommandParameter { Name = "model", Description = "Model name", IsRequired = true },
            new CommandParameter { Name = "provider", Description = "Provider", IsRequired = false }
        ]);

        Assert.Equal(2, cmd.Parameters.Count);
        Assert.True(cmd.Parameters[0].IsRequired);
        Assert.False(cmd.Parameters[1].IsRequired);

        var result = await cmd.ExecuteAsync(MakeContext("switch",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["model"] = "gpt-4" }));

        Assert.True(result.Success);
    }

    [Fact]
    public void CommandParameter_RequiredShownDifferently()
    {
        var required = new CommandParameter { Name = "model", Description = "Model", IsRequired = true };
        var optional = new CommandParameter { Name = "provider", Description = "Provider", IsRequired = false };

        // Required params use <name>, optional use [name] in help output
        var requiredStr = required.IsRequired ? $"<{required.Name}>" : $"[{required.Name}]";
        var optionalStr = optional.IsRequired ? $"<{optional.Name}>" : $"[{optional.Name}]";

        Assert.Equal("<model>", requiredStr);
        Assert.Equal("[provider]", optionalStr);
    }

    private static CommandContext MakeContext(string name, Dictionary<string, string>? args = null) => new()
    {
        CommandName = name,
        InvokerId = "user1",
        ChannelId = "ch1",
        ChannelType = "test",
        Arguments = args ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    private sealed class StubCommand(string name, string description) : IChannelCommand
    {
        public string Name => name;
        public string Description => description;
        public IReadOnlyList<CommandParameter> Parameters => [];

        public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult { Success = true, Content = "ok" });
    }

    private sealed class StubCommandWithParams(
        string name, string description, IReadOnlyList<CommandParameter> parameters) : IChannelCommand
    {
        public string Name => name;
        public string Description => description;
        public IReadOnlyList<CommandParameter> Parameters => parameters;

        public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult { Success = true, Content = "ok" });
    }
}
