using FluentAssertions;
using JD.AI.Agent;
using JD.AI.Startup;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Turn Monitor Box")]
public sealed class TurnMonitorBoxBddTests : TinyBddXunitBase
{
    public TurnMonitorBoxBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Box starts empty before a turn monitor is assigned"), Fact]
    public async Task Value_DefaultIsNull()
    {
        TurnMonitorBox? box = null;

        await Given("a new monitor box", () => box = new TurnMonitorBox())
            .When("reading its value", _ => box)
            .Then("no monitor is assigned yet", _ =>
            {
                box!.Value.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Assigning and replacing monitor instances updates the stored value"), Fact]
    public async Task Value_CanBeSetAndReplaced()
    {
        TurnInputMonitor? first = null;
        TurnInputMonitor? second = null;
        TurnMonitorBox? box = null;

        await Given("two monitor instances and a box", () =>
            {
                box = new TurnMonitorBox();
                first = new TurnInputMonitor(CancellationToken.None);
                second = new TurnInputMonitor(CancellationToken.None);
                return box;
            })
            .When("the second monitor replaces the first", b =>
            {
                b.Value = first;
                b.Value = second;
                return b;
            })
            .Then("the latest monitor is returned", _ =>
            {
                box!.Value.Should().BeSameAs(second);
                box.Value.Should().NotBeSameAs(first);
                return true;
            })
            .AssertPassed();

        first?.Dispose();
        second?.Dispose();
    }
}
