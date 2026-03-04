using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Turn Spinner")]
public sealed class TurnSpinnerBddTests : TinyBddXunitBase
{
    public TurnSpinnerBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Construction does not throw"), Fact]
    public async Task Construction_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a new TurnSpinner", () =>
            {
                try
                {
                    var spinner = new TurnSpinner();
                    spinner.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return true;
            })
            .When("checking for exceptions", state => state)
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Stop does not throw"), Fact]
    public async Task Stop_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a TurnSpinner instance", () => new TurnSpinner())
            .When("Stop is called", spinner =>
            {
                try
                {
                    spinner.Stop();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                spinner.Dispose();
                return spinner;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Dispose does not throw"), Fact]
    public async Task Dispose_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a TurnSpinner instance", () => new TurnSpinner())
            .When("disposed", spinner =>
            {
                try
                {
                    spinner.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return spinner;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Stop called twice does not throw"), Fact]
    public async Task StopCalledTwice_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a TurnSpinner instance", () => new TurnSpinner())
            .When("Stop is called twice", spinner =>
            {
                try
                {
                    spinner.Stop();
                    spinner.Stop();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                spinner.Dispose();
                return spinner;
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
                result = TurnSpinner.FormatElapsed(ts);
                return ts;
            })
            .Then("the result is '30.0s'", _ => result == "30.0s")
            .AssertPassed();
    }

    [Scenario("FormatElapsed returns 1m 30s for 90 seconds"), Fact]
    public async Task FormatElapsed_90Seconds_Returns1m30s()
    {
        string? result = null;

        await Given("a TimeSpan of 90 seconds", () => TimeSpan.FromSeconds(90))
            .When("FormatElapsed is called", ts =>
            {
                result = TurnSpinner.FormatElapsed(ts);
                return ts;
            })
            .Then("the result is '1m 30s'", _ => result == "1m 30s")
            .AssertPassed();
    }
}
