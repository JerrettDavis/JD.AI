using JD.AI.Core.Config;
using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Turn Progress")]
public sealed class TurnProgressBddTests : TinyBddXunitBase
{
    public TurnProgressBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("SpinnerStyle.None does not throw on construction and dispose"), Fact]
    public async Task StyleNone_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("SpinnerStyle.None", () => SpinnerStyle.None)
            .When("constructing and disposing TurnProgress", style =>
            {
                try
                {
                    using var progress = new TurnProgress(style);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return style;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Stop sets TimeToFirstTokenMs to non-negative value"), Fact]
    public async Task Stop_SetsTimeToFirstTokenMs()
    {
        long ttft = -1;

        await Given("a started TurnProgress", () =>
            {
                var p = new TurnProgress(SpinnerStyle.Normal);
                return p;
            })
            .When("Stop is called", progress =>
            {
                progress.Stop();
                ttft = progress.TimeToFirstTokenMs;
                progress.Dispose();
                return progress;
            })
            .Then("TimeToFirstTokenMs is >= 0", _ => ttft >= 0)
            .AssertPassed();
    }

    [Scenario("Dispose does not throw"), Fact]
    public async Task Dispose_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a TurnProgress instance", () => new TurnProgress(SpinnerStyle.Normal))
            .When("disposed", progress =>
            {
                try
                {
                    progress.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return progress;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("FormatElapsed returns 30.0s for 30 seconds"), Fact]
    public async Task FormatElapsed_30Seconds_Returns30s()
    {
        string? result = null;

        await Given("a TimeSpan of 30 seconds", () => TimeSpan.FromSeconds(30))
            .When("FormatElapsed is called", ts =>
            {
                result = TurnProgress.FormatElapsed(ts);
                return ts;
            })
            .Then("the result is '30.0s'", _ => string.Equals(result, "30.0s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsed returns 2m 05s for 125 seconds"), Fact]
    public async Task FormatElapsed_125Seconds_Returns2m05s()
    {
        string? result = null;

        await Given("a TimeSpan of 125 seconds", () => TimeSpan.FromSeconds(125))
            .When("FormatElapsed is called", ts =>
            {
                result = TurnProgress.FormatElapsed(ts);
                return ts;
            })
            .Then("the result is '2m 05s'", _ => string.Equals(result, "2m 05s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsed returns 0.0s for 0 seconds"), Fact]
    public async Task FormatElapsed_0Seconds_Returns0s()
    {
        string? result = null;

        await Given("a TimeSpan of 0 seconds", () => TimeSpan.Zero)
            .When("FormatElapsed is called", ts =>
            {
                result = TurnProgress.FormatElapsed(ts);
                return ts;
            })
            .Then("the result is '0.0s'", _ => string.Equals(result, "0.0s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("BuildProgressBar returns a 10-character string"), Fact]
    public async Task BuildProgressBar_Frame0_Returns10CharString()
    {
        string? result = null;

        await Given("a TimeSpan of 500 milliseconds", () => TimeSpan.FromMilliseconds(500))
            .When("BuildProgressBar is called", ts =>
            {
                result = TurnProgress.BuildProgressBar(ts);
                return ts;
            })
            .Then("the result is a 10-character string", _ => result != null && result.Length == 10)
            .AssertPassed();
    }

    [Scenario("FormatMinimal returns string containing elapsed time"), Fact]
    public async Task FormatMinimal_ReturnsStringWithElapsed()
    {
        string? result = null;

        await Given("a TurnProgress with Minimal style", () => new TurnProgress(SpinnerStyle.Minimal))
            .When("FormatMinimal is called", progress =>
            {
                result = progress.FormatMinimal(TimeSpan.FromSeconds(5));
                progress.Dispose();
                return progress;
            })
            .Then("the result contains the elapsed time", _ => result != null && result.Contains("5.0s"))
            .AssertPassed();
    }

    [Scenario("FormatNormal returns string containing Thinking"), Fact]
    public async Task FormatNormal_ReturnsStringWithThinking()
    {
        string? result = null;

        await Given("a TurnProgress with Normal style", () => new TurnProgress(SpinnerStyle.Normal))
            .When("FormatNormal is called", progress =>
            {
                result = progress.FormatNormal(TimeSpan.FromSeconds(5));
                progress.Dispose();
                return progress;
            })
            .Then("the result contains 'Thinking'", _ => result != null && result.Contains("Thinking"))
            .AssertPassed();
    }

    [Scenario("FormatNerdy returns string containing model name"), Fact]
    public async Task FormatNerdy_ReturnsStringWithModelName()
    {
        string? result = null;

        await Given("a TurnProgress with Nerdy style and model name", () => new TurnProgress(SpinnerStyle.Nerdy, "gpt-4o"))
            .When("FormatNerdy is called", progress =>
            {
                result = progress.FormatNerdy(TimeSpan.FromSeconds(5));
                progress.Dispose();
                return progress;
            })
            .Then("the result contains the model name", _ => result != null && result.Contains("gpt-4o"))
            .AssertPassed();
    }
}
