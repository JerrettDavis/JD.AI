using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Spectre Agent Output")]
public sealed class SpectreAgentOutputBddTests : TinyBddXunitBase
{
    public SpectreAgentOutputBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Construction sets Style and ModelName"), Fact]
    public async Task Construction_SetsStyleAndModelName()
    {
        SpinnerStyle? capturedStyle = null;
        string? capturedModel = null;

        await Given("a SpinnerStyle and model name", () => (style: SpinnerStyle.Rich, model: "gpt-4o"))
            .When("SpectreAgentOutput is constructed", input =>
            {
                using var output = new SpectreAgentOutput(input.style, input.model);
                capturedStyle = output.Style;
                capturedModel = output.ModelName;
                return input;
            })
            .Then("Style and ModelName are set correctly", _ =>
                capturedStyle == SpinnerStyle.Rich && capturedModel == "gpt-4o")
            .AssertPassed();
    }

    [Scenario("Style property can be updated"), Fact]
    public async Task Style_CanBeUpdated()
    {
        SpinnerStyle? updatedStyle = null;

        await Given("a SpectreAgentOutput with Normal style", () => new SpectreAgentOutput(SpinnerStyle.Normal))
            .When("Style is changed to Rich", output =>
            {
                output.Style = SpinnerStyle.Rich;
                updatedStyle = output.Style;
                output.Dispose();
                return output;
            })
            .Then("Style reflects the new value", _ => updatedStyle == SpinnerStyle.Rich)
            .AssertPassed();
    }

    [Scenario("BeginTurn does not throw"), Fact]
    public async Task BeginTurn_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a SpectreAgentOutput instance", () => new SpectreAgentOutput(SpinnerStyle.Normal))
            .When("BeginTurn is called", output =>
            {
                try
                {
                    output.BeginTurn();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                output.Dispose();
                return output;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("EndTurn after BeginTurn does not throw"), Fact]
    public async Task EndTurn_AfterBeginTurn_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a SpectreAgentOutput after BeginTurn", () =>
            {
                var output = new SpectreAgentOutput(SpinnerStyle.Normal);
                output.BeginTurn();
                return output;
            })
            .When("EndTurn is called with metrics", output =>
            {
                try
                {
                    var metrics = new TurnMetrics(1000, 100, 500);
                    output.EndTurn(metrics);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                output.Dispose();
                return output;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Dispose does not throw"), Fact]
    public async Task Dispose_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a SpectreAgentOutput instance", () => new SpectreAgentOutput(SpinnerStyle.Normal))
            .When("disposed", output =>
            {
                try
                {
                    output.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return output;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }

    [Scenario("Dispose called twice does not throw"), Fact]
    public async Task DisposeCalledTwice_DoesNotThrow()
    {
        Exception? caught = null;

        await Given("a SpectreAgentOutput instance", () => new SpectreAgentOutput(SpinnerStyle.Normal))
            .When("disposed twice", output =>
            {
                try
                {
                    output.Dispose();
                    output.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return output;
            })
            .Then("no exception is thrown", _ => caught == null)
            .AssertPassed();
    }
}
