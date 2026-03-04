using JD.AI.Core.Agents.Orchestration;
using JD.AI.Rendering;
using Spectre.Console;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Team Progress Panel")]
public sealed class TeamProgressPanelBddTests : TinyBddXunitBase
{
    public TeamProgressPanelBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("OnProgress stores agent state and Render shows agent name"), Fact]
    public async Task OnProgress_StoresState()
    {
        Panel? rendered = null;

        await Given("a new panel with a progress event", () =>
            {
                var panel = new TeamProgressPanel();
                var progress = new SubagentProgress("test-agent", SubagentStatus.Pending, "Starting");
                panel.OnProgress(progress);
                return panel;
            })
            .When("rendering the panel", p =>
            {
                rendered = p.Render();
                return p;
            })
            .Then("rendered output contains agent name", _ =>
            {
                var writer = new StringWriter();
                var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
                console.Write(rendered!);
                var output = writer.ToString();
                return output.Contains("test-agent", StringComparison.Ordinal);
            })
            .AssertPassed();
    }

    [Scenario("OnProgress with update changes state and Render shows new detail"), Fact]
    public async Task OnProgress_UpdatesState()
    {
        Panel? rendered = null;

        await Given("a panel with an existing agent that gets updated", () =>
            {
                var panel = new TeamProgressPanel();
                panel.OnProgress(new SubagentProgress("agent-1", SubagentStatus.Pending, "Starting"));
                panel.OnProgress(new SubagentProgress("agent-1", SubagentStatus.Thinking, "Analyzing code"));
                return panel;
            })
            .When("rendering the panel", p =>
            {
                rendered = p.Render();
                return p;
            })
            .Then("rendered output shows new detail", _ =>
            {
                var writer = new StringWriter();
                var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
                console.Write(rendered!);
                var output = writer.ToString();
                return output.Contains("Analyzing code", StringComparison.Ordinal);
            })
            .AssertPassed();
    }

    [Scenario("Panel with no agents shows waiting message"), Fact]
    public async Task NoAgents_ShowsWaitingMessage()
    {
        Panel? rendered = null;

        await Given("a panel with no agents", () => new TeamProgressPanel())
            .When("rendering the panel", p =>
            {
                rendered = p.Render();
                return p;
            })
            .Then("shows 'Waiting for agents' text", _ =>
            {
                var writer = new StringWriter();
                var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
                console.Write(rendered!);
                var output = writer.ToString();
                return output.Contains("Waiting for agents", StringComparison.Ordinal);
            })
            .AssertPassed();
    }

    [Scenario("Panel with agents has 'Team Execution' header"), Fact]
    public async Task WithAgents_HeaderContainsTeamExecution()
    {
        Panel? rendered = null;

        await Given("a panel with agents", () =>
            {
                var panel = new TeamProgressPanel();
                panel.OnProgress(new SubagentProgress("worker", SubagentStatus.Started, "Working"));
                return panel;
            })
            .When("rendering the panel", p =>
            {
                rendered = p.Render();
                return p;
            })
            .Then("panel header contains 'Team Execution'", _ =>
            {
                var writer = new StringWriter();
                var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
                console.Write(rendered!);
                var output = writer.ToString();
                return output.Contains("Team Execution", StringComparison.Ordinal);
            })
            .AssertPassed();
    }

    [Scenario("Pending status icon is circle outline"), Fact]
    public async Task PendingStatus_IconIsCircle()
    {
        string? icon = null;

        await Given("Pending status", () => SubagentStatus.Pending)
            .When("getting status icon", status =>
            {
                icon = TeamProgressPanel.GetStatusIcon(status);
                return status;
            })
            .Then("icon is open circle", _ => icon == "\u25cb")
            .AssertPassed();
    }

    [Scenario("Completed status icon is filled circle"), Fact]
    public async Task CompletedStatus_IconIsFilledCircle()
    {
        string? icon = null;

        await Given("Completed status", () => SubagentStatus.Completed)
            .When("getting status icon", status =>
            {
                icon = TeamProgressPanel.GetStatusIcon(status);
                return status;
            })
            .Then("icon is filled circle", _ => icon == "\u25cf")
            .AssertPassed();
    }

    [Scenario("Failed status icon is cross mark"), Fact]
    public async Task FailedStatus_IconIsCross()
    {
        string? icon = null;

        await Given("Failed status", () => SubagentStatus.Failed)
            .When("getting status icon", status =>
            {
                icon = TeamProgressPanel.GetStatusIcon(status);
                return status;
            })
            .Then("icon is cross mark", _ => icon == "\u2717")
            .AssertPassed();
    }

    [Scenario("Completed status color is green"), Fact]
    public async Task CompletedStatus_ColorIsGreen()
    {
        string? color = null;

        await Given("Completed status", () => SubagentStatus.Completed)
            .When("getting status color", status =>
            {
                color = TeamProgressPanel.GetStatusColor(status);
                return status;
            })
            .Then("color is green", _ => color == "green")
            .AssertPassed();
    }

    [Scenario("Failed status color is red"), Fact]
    public async Task FailedStatus_ColorIsRed()
    {
        string? color = null;

        await Given("Failed status", () => SubagentStatus.Failed)
            .When("getting status color", status =>
            {
                color = TeamProgressPanel.GetStatusColor(status);
                return status;
            })
            .Then("color is red", _ => color == "red")
            .AssertPassed();
    }

    [Scenario("Cancelled status color is yellow"), Fact]
    public async Task CancelledStatus_ColorIsYellow()
    {
        string? color = null;

        await Given("Cancelled status", () => SubagentStatus.Cancelled)
            .When("getting status color", status =>
            {
                color = TeamProgressPanel.GetStatusColor(status);
                return status;
            })
            .Then("color is yellow", _ => color == "yellow")
            .AssertPassed();
    }
}
