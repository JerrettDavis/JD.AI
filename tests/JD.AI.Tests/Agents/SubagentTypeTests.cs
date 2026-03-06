using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class SubagentTypeTests
{
    [Theory]
    [InlineData(SubagentType.Explore, 0)]
    [InlineData(SubagentType.Task, 1)]
    [InlineData(SubagentType.Plan, 2)]
    [InlineData(SubagentType.Review, 3)]
    [InlineData(SubagentType.General, 4)]
    public void SubagentType_Values(SubagentType type, int expected) =>
        ((int)type).Should().Be(expected);
}
