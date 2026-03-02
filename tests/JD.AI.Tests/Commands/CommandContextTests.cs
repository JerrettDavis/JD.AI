using JD.AI.Core.Commands;

namespace JD.AI.Tests.Commands;

public class CommandContextTests
{
    [Fact]
    public void Context_DefaultArguments_IsEmpty()
    {
        var context = new CommandContext
        {
            CommandName = "test",
            InvokerId = "user1",
            ChannelId = "ch1",
            ChannelType = "discord"
        };

        Assert.Empty(context.Arguments);
    }

    [Fact]
    public void Context_WithArguments_AreAccessible()
    {
        var context = new CommandContext
        {
            CommandName = "switch",
            InvokerId = "user1",
            ChannelId = "ch1",
            ChannelType = "discord",
            Arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["model"] = "gpt-4",
                ["provider"] = "openai"
            }
        };

        Assert.Equal("gpt-4", context.Arguments["model"]);
        Assert.Equal("openai", context.Arguments["provider"]);
    }

    [Fact]
    public void Result_DefaultEphemeral_IsTrue()
    {
        var result = new CommandResult { Success = true, Content = "ok" };
        Assert.True(result.Ephemeral);
    }

    [Fact]
    public void Result_CanBeNonEphemeral()
    {
        var result = new CommandResult { Success = true, Content = "ok", Ephemeral = false };
        Assert.False(result.Ephemeral);
    }

    [Fact]
    public void CommandParameter_Defaults()
    {
        var param = new CommandParameter { Name = "test", Description = "A test param" };

        Assert.False(param.IsRequired);
        Assert.Equal(CommandParameterType.Text, param.Type);
        Assert.Empty(param.Choices);
    }

    [Fact]
    public void CommandParameter_WithChoices()
    {
        var param = new CommandParameter
        {
            Name = "color",
            Description = "Pick a color",
            Choices = ["red", "green", "blue"]
        };

        Assert.Equal(3, param.Choices.Count);
        Assert.Contains("green", param.Choices);
    }
}
