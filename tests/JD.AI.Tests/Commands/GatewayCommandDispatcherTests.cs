using FluentAssertions;
using JD.AI.Core.Commands;
using NSubstitute;

namespace JD.AI.Tests.Commands;

public sealed class GatewayCommandDispatcherTests
{
    [Theory]
    [InlineData("!model list", "models")]
    [InlineData("!model current", "status")]
    [InlineData("!model", "status")]
    [InlineData("/model list", "models")]
    [InlineData("/model current", "status")]
    [InlineData("<@123456789> !model list", "models")]
    [InlineData("<@!123456789> !model set gpt-4o", "switch")]
    public async Task TryDispatchAsync_DiscordModelFastPath_ParsesAndExecutes(string input, string expectedCommand)
    {
        var registry = new CommandRegistry();
        var cmd = StubCommand(expectedCommand);
        registry.Register(cmd);

        var result = await GatewayCommandDispatcher.TryDispatchAsync(
            registry,
            channelType: "discord",
            message: input,
            invokerId: "u1",
            channelId: "c1");

        result.Handled.Should().BeTrue();
        result.CommandName.Should().Be(expectedCommand);
        await cmd.Received(1).ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_DiscordModelSet_MapsArgumentToSwitchModelParameter()
    {
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("switch");
        command.Description.Returns("switch model");
        command.Parameters.Returns([new CommandParameter { Name = "model", Description = "model", IsRequired = true }]);
        CommandContext? captured = null;
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.ArgAt<CommandContext>(0);
                return Task.FromResult(new CommandResult { Success = true, Content = "ok" });
            });

        var registry = new CommandRegistry();
        registry.Register(command);

        var result = await GatewayCommandDispatcher.TryDispatchAsync(
            registry,
            channelType: "discord",
            message: "<@123> !model set gpt-4o-mini",
            invokerId: "u1",
            channelId: "c1");

        result.Handled.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Arguments.Should().ContainKey("model");
        captured.Arguments["model"].Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task TryDispatchAsync_JdaiPrefix_ParsesAndExecutes()
    {
        var registry = new CommandRegistry();
        var help = StubCommand("help");
        registry.Register(help);

        var result = await GatewayCommandDispatcher.TryDispatchAsync(
            registry,
            channelType: "discord",
            message: "/jdai-help",
            invokerId: "u1",
            channelId: "c1");

        result.Handled.Should().BeTrue();
        result.CommandName.Should().Be("help");
    }

    [Fact]
    public async Task TryDispatchAsync_NonFastPathMessage_NotHandled()
    {
        var result = await GatewayCommandDispatcher.TryDispatchAsync(
            new CommandRegistry(),
            channelType: "discord",
            message: "hello there",
            invokerId: "u1",
            channelId: "c1");

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryDispatchAsync_UnknownFastPathCommand_HandledWithHelp()
    {
        var result = await GatewayCommandDispatcher.TryDispatchAsync(
            new CommandRegistry(),
            channelType: "discord",
            message: "!model list",
            invokerId: "u1",
            channelId: "c1");

        result.Handled.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.Response.Should().Contain("Command not found");
    }

    private static IChannelCommand StubCommand(string name)
    {
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns(name);
        command.Description.Returns($"{name} command");
        command.Parameters.Returns(Array.Empty<CommandParameter>());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandResult { Success = true, Content = "ok" }));
        return command;
    }
}
