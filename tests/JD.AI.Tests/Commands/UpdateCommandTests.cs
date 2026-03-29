using JD.AI.Commands;
using Xunit;

namespace JD.AI.Tests.Commands;

public sealed class UpdateCommandTests
{
    [Theory]
    [InlineData(null, UpdateAction.Check, null)]
    [InlineData("", UpdateAction.Check, null)]
    [InlineData("status", UpdateAction.Status, null)]
    [InlineData("check", UpdateAction.Check, null)]
    [InlineData("plan", UpdateAction.Plan, "latest")]
    [InlineData("plan latest", UpdateAction.Plan, "latest")]
    [InlineData("plan 1.2.3", UpdateAction.Plan, "1.2.3")]
    [InlineData("apply", UpdateAction.Apply, "latest")]
    [InlineData("apply 2.0.0", UpdateAction.Apply, "2.0.0")]
    public void Parse_ParsesExpectedActionAndTarget(string? input, UpdateAction expectedAction, string? expectedTarget)
    {
        var command = UpdateCommand.Parse(input);

        Assert.Equal(expectedAction, command.Action);
        Assert.Equal(expectedTarget, command.Target);
    }

    [Theory]
    [InlineData("please check for updates", UpdateAction.Check, null)]
    [InlineData("show update status", UpdateAction.Status, null)]
    [InlineData("plan the update to latest", UpdateAction.Plan, "latest")]
    [InlineData("apply update 2.3.4", UpdateAction.Apply, "2.3.4")]
    public void TryParsePromptIntent_RecognizesNaturalLanguage(string input, UpdateAction expectedAction, string? expectedTarget)
    {
        var parsed = UpdateCommand.TryParsePromptIntent(input, out var command);

        Assert.True(parsed);
        Assert.Equal(expectedAction, command.Action);
        Assert.Equal(expectedTarget, command.Target);
    }

    [Fact]
    public void TryParsePromptIntent_IgnoresNonUpdatePrompts()
    {
        var parsed = UpdateCommand.TryParsePromptIntent("write a poem", out _);

        Assert.False(parsed);
    }
}
