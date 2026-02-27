using JD.AI.Tui.Agent;

namespace JD.AI.Tui.Tests;

public sealed class SubagentRunnerTests
{
    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void SubagentType_AllValuesAreDefined(SubagentType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Fact]
    public void SubagentType_HasFiveValues()
    {
        var values = Enum.GetValues<SubagentType>();
        Assert.Equal(5, values.Length);
    }
}
