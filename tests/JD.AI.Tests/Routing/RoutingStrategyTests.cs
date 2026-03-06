using FluentAssertions;
using JD.AI.Core.Routing;

namespace JD.AI.Tests.Routing;

public sealed class RoutingStrategyTests
{
    [Theory]
    [InlineData(RoutingStrategy.LocalFirst, 0)]
    [InlineData(RoutingStrategy.CostOptimized, 1)]
    [InlineData(RoutingStrategy.CapabilityDriven, 2)]
    [InlineData(RoutingStrategy.LatencyOptimized, 3)]
    public void RoutingStrategy_Values(RoutingStrategy strategy, int expected) =>
        ((int)strategy).Should().Be(expected);
}
