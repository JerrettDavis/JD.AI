using JD.AI.Core.Config;
using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Chat Renderer")]
public sealed class ChatRendererBddTests : TinyBddXunitBase, IDisposable
{
    public ChatRendererBddTests(ITestOutputHelper output) : base(output) { }

    public new void Dispose()
    {
        ChatRenderer.ApplyTheme(TuiTheme.DefaultDark);
        ChatRenderer.SetOutputStyle(OutputStyle.Rich);
        base.Dispose();
    }

    // ── FormatElapsedMetric tests ──────────────────────────────────────

    [Scenario("FormatElapsedMetric returns seconds for 45000ms"), Fact]
    public async Task FormatElapsedMetric_45000ms_Returns_45_0s()
    {
        string? result = null;

        await Given("an elapsed time of 45000 milliseconds", () => 45000L)
            .When("formatting the elapsed metric", ms => { result = ChatRenderer.FormatElapsedMetric(ms); return ms; })
            .Then("the result is '45.0s'", _ => string.Equals(result, "45.0s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsedMetric returns minutes and seconds for 90000ms"), Fact]
    public async Task FormatElapsedMetric_90000ms_Returns_1m_30s()
    {
        string? result = null;

        await Given("an elapsed time of 90000 milliseconds", () => 90000L)
            .When("formatting the elapsed metric", ms => { result = ChatRenderer.FormatElapsedMetric(ms); return ms; })
            .Then("the result is '1m 30s'", _ => string.Equals(result, "1m 30s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsedMetric returns sub-second for 500ms"), Fact]
    public async Task FormatElapsedMetric_500ms_Returns_0_5s()
    {
        string? result = null;

        await Given("an elapsed time of 500 milliseconds", () => 500L)
            .When("formatting the elapsed metric", ms => { result = ChatRenderer.FormatElapsedMetric(ms); return ms; })
            .Then("the result is '0.5s'", _ => string.Equals(result, "0.5s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsedMetric returns minutes with zero seconds for 60000ms"), Fact]
    public async Task FormatElapsedMetric_60000ms_Returns_1m_0s()
    {
        string? result = null;

        await Given("an elapsed time of 60000 milliseconds", () => 60000L)
            .When("formatting the elapsed metric", ms => { result = ChatRenderer.FormatElapsedMetric(ms); return ms; })
            .Then("the result is '1m 0s'", _ => string.Equals(result, "1m 0s", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatElapsedMetric returns zero for 0ms"), Fact]
    public async Task FormatElapsedMetric_0ms_Returns_0_0s()
    {
        string? result = null;

        await Given("an elapsed time of 0 milliseconds", () => 0L)
            .When("formatting the elapsed metric", ms => { result = ChatRenderer.FormatElapsedMetric(ms); return ms; })
            .Then("the result is '0.0s'", _ => string.Equals(result, "0.0s", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── FormatBytes tests ──────────────────────────────────────────────

    [Scenario("FormatBytes returns bytes for 500 bytes"), Fact]
    public async Task FormatBytes_500_Returns_500_B()
    {
        string? result = null;

        await Given("a byte count of 500", () => 500L)
            .When("formatting the bytes", bytes => { result = ChatRenderer.FormatBytes(bytes); return bytes; })
            .Then("the result is '500 B'", _ => string.Equals(result, "500 B", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatBytes returns KB for 1024 bytes"), Fact]
    public async Task FormatBytes_1024_Returns_1_0_KB()
    {
        string? result = null;

        await Given("a byte count of 1024", () => 1024L)
            .When("formatting the bytes", bytes => { result = ChatRenderer.FormatBytes(bytes); return bytes; })
            .Then("the result is '1.0 KB'", _ => string.Equals(result, "1.0 KB", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatBytes returns KB for 2048 bytes"), Fact]
    public async Task FormatBytes_2048_Returns_2_0_KB()
    {
        string? result = null;

        await Given("a byte count of 2048", () => 2048L)
            .When("formatting the bytes", bytes => { result = ChatRenderer.FormatBytes(bytes); return bytes; })
            .Then("the result is '2.0 KB'", _ => string.Equals(result, "2.0 KB", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatBytes returns MB for 1048576 bytes"), Fact]
    public async Task FormatBytes_1048576_Returns_1_0_MB()
    {
        string? result = null;

        await Given("a byte count of 1048576", () => 1048576L)
            .When("formatting the bytes", bytes => { result = ChatRenderer.FormatBytes(bytes); return bytes; })
            .Then("the result is '1.0 MB'", _ => string.Equals(result, "1.0 MB", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("FormatBytes returns MB for 5242880 bytes"), Fact]
    public async Task FormatBytes_5242880_Returns_5_0_MB()
    {
        string? result = null;

        await Given("a byte count of 5242880", () => 5242880L)
            .When("formatting the bytes", bytes => { result = ChatRenderer.FormatBytes(bytes); return bytes; })
            .Then("the result is '5.0 MB'", _ => string.Equals(result, "5.0 MB", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── EscapeJsonString tests ─────────────────────────────────────────

    [Scenario("EscapeJsonString returns empty for empty string"), Fact]
    public async Task EscapeJsonString_Empty_Returns_Empty()
    {
        string? result = null;

        await Given("an empty string", () => string.Empty)
            .When("escaping for JSON", value => { result = ChatRenderer.EscapeJsonString(value); return value; })
            .Then("the result is empty", _ => result != null && result.Length == 0)
            .AssertPassed();
    }

    [Scenario("EscapeJsonString returns empty for null-equivalent input"), Fact]
    public async Task EscapeJsonString_NullOrEmpty_Returns_Empty()
    {
        string? result = null;

        await Given("a null-equivalent empty string", () => "")
            .When("escaping for JSON", value => { result = ChatRenderer.EscapeJsonString(value); return value; })
            .Then("the result is empty", _ => result != null && result.Length == 0)
            .AssertPassed();
    }

    [Scenario("EscapeJsonString escapes quotes"), Fact]
    public async Task EscapeJsonString_Quotes_Escapes_Properly()
    {
        string? result = null;

        await Given("a string containing double quotes", () => "He said \"hello\"")
            .When("escaping for JSON", value => { result = ChatRenderer.EscapeJsonString(value); return value; })
            .Then("the result contains escaped quotes", _ => result != null && result.Contains("\\u0022"))
            .AssertPassed();
    }

    [Scenario("EscapeJsonString escapes newlines"), Fact]
    public async Task EscapeJsonString_Newlines_Escapes_Properly()
    {
        string? result = null;

        await Given("a string containing newlines", () => "line1\nline2")
            .When("escaping for JSON", value => { result = ChatRenderer.EscapeJsonString(value); return value; })
            .Then("the result contains escaped newlines", _ => result != null && result.Contains("\\n"))
            .AssertPassed();
    }

    // ── ApplyTheme / SetOutputStyle tests ──────────────────────────────

    [Scenario("ApplyTheme sets CurrentTheme to Dracula"), Fact]
    public async Task ApplyTheme_Dracula_Sets_CurrentTheme()
    {
        await Given("the default theme is active", () => { ChatRenderer.ApplyTheme(TuiTheme.DefaultDark); return true; })
            .When("applying the Dracula theme", _ => { ChatRenderer.ApplyTheme(TuiTheme.Dracula); return true; })
            .Then("CurrentTheme is Dracula", _ => ChatRenderer.CurrentTheme == TuiTheme.Dracula)
            .AssertPassed();
    }

    [Scenario("SetOutputStyle sets CurrentOutputStyle to Plain"), Fact]
    public async Task SetOutputStyle_Plain_Sets_CurrentOutputStyle()
    {
        await Given("the default output style is active", () => { ChatRenderer.SetOutputStyle(OutputStyle.Rich); return true; })
            .When("setting output style to Plain", _ => { ChatRenderer.SetOutputStyle(OutputStyle.Plain); return true; })
            .Then("CurrentOutputStyle is Plain", _ => ChatRenderer.CurrentOutputStyle == OutputStyle.Plain)
            .AssertPassed();
    }

    [Scenario("SetOutputStyle sets CurrentOutputStyle to Json"), Fact]
    public async Task SetOutputStyle_Json_Sets_CurrentOutputStyle()
    {
        await Given("the default output style is active", () => { ChatRenderer.SetOutputStyle(OutputStyle.Rich); return true; })
            .When("setting output style to Json", _ => { ChatRenderer.SetOutputStyle(OutputStyle.Json); return true; })
            .Then("CurrentOutputStyle is Json", _ => ChatRenderer.CurrentOutputStyle == OutputStyle.Json)
            .AssertPassed();
    }

    [Scenario("ApplyTheme resets CurrentTheme to DefaultDark"), Fact]
    public async Task ApplyTheme_DefaultDark_Resets_CurrentTheme()
    {
        await Given("the Dracula theme is active", () => { ChatRenderer.ApplyTheme(TuiTheme.Dracula); return true; })
            .When("applying the DefaultDark theme", _ => { ChatRenderer.ApplyTheme(TuiTheme.DefaultDark); return true; })
            .Then("CurrentTheme is DefaultDark", _ => ChatRenderer.CurrentTheme == TuiTheme.DefaultDark)
            .AssertPassed();
    }

    [Scenario("BuildIndicatorsLine renders daemon and gateway states"), Fact]
    public async Task BuildIndicatorsLine_RendersIndicators()
    {
        string? result = null;

        await Given("daemon and gateway indicators", () => (IReadOnlyList<WelcomeIndicator>)
            [
                new("Daemon", "running", IndicatorState.Healthy),
                new("Gateway", "offline", IndicatorState.Warning),
            ])
            .When("building the indicator line", indicators =>
            {
                result = ChatRenderer.BuildIndicatorsLine(indicators);
                return indicators;
            })
            .Then("the status line contains both indicators", _ =>
                result is not null
                && result.Contains("Services:", StringComparison.Ordinal)
                && result.Contains("Daemon:", StringComparison.Ordinal)
                && result.Contains("Gateway:", StringComparison.Ordinal)
                && result.Contains("[green]running[/]", StringComparison.Ordinal)
                && result.Contains("offline", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("BuildIndicatorsLine returns empty for null input"), Fact]
    public async Task BuildIndicatorsLine_Null_ReturnsEmpty()
    {
        string? result = "placeholder";

        await Given("no indicators", () => (IReadOnlyList<WelcomeIndicator>?)null)
            .When("building the indicator line", indicators =>
            {
                result = ChatRenderer.BuildIndicatorsLine(indicators);
                return indicators;
            })
            .Then("the result is empty", _ => string.IsNullOrEmpty(result))
            .AssertPassed();
    }

    [Scenario("BuildWelcomeBody includes cwd version motd when enabled"), Fact]
    public async Task BuildWelcomeBody_IncludesConfiguredDetails()
    {
        string? result = null;

        await Given("welcome settings with all details enabled", () => new WelcomePanelSettings
        {
            ShowModelSummary = true,
            ShowServices = true,
            ShowWorkingDirectory = true,
            ShowVersion = true,
            ShowMotd = true,
        })
            .When("building the welcome body", settings =>
            {
                result = ChatRenderer.BuildWelcomeBody(
                    modelName: "gpt-5.3-codex",
                    providerName: "OpenAI Codex",
                    totalModels: 5,
                    indicators:
                    [
                        new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy),
                    ],
                    details: new WelcomeBannerDetails(
                        WorkingDirectory: "/repo/jdai",
                        Version: "0.1.23",
                        Motd: "Welcome operators"),
                    settings: settings);

                return settings;
            })
            .Then("the body includes all configured lines", _ =>
                result is not null
                && result.Contains("Provider:", StringComparison.Ordinal)
                && result.Contains("Services:", StringComparison.Ordinal)
                && result.Contains("CWD:", StringComparison.Ordinal)
                && result.Contains("Version:", StringComparison.Ordinal)
                && result.Contains("MoTD:", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("BuildWelcomeBody hides optional lines when disabled"), Fact]
    public async Task BuildWelcomeBody_HidesDisabledDetails()
    {
        string? result = null;

        await Given("welcome settings with optional lines disabled", () => new WelcomePanelSettings
        {
            ShowModelSummary = false,
            ShowServices = false,
            ShowWorkingDirectory = false,
            ShowVersion = false,
            ShowMotd = false,
        })
            .When("building the welcome body", settings =>
            {
                result = ChatRenderer.BuildWelcomeBody(
                    modelName: "gpt-5.3-codex",
                    providerName: "OpenAI Codex",
                    totalModels: 5,
                    indicators:
                    [
                        new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy),
                    ],
                    details: new WelcomeBannerDetails(
                        WorkingDirectory: "/repo/jdai",
                        Version: "0.1.23",
                        Motd: "Welcome operators"),
                    settings: settings);

                return settings;
            })
            .Then("the body omits optional lines", _ =>
                result is not null
                && !result.Contains("Provider:", StringComparison.Ordinal)
                && !result.Contains("Services:", StringComparison.Ordinal)
                && !result.Contains("CWD:", StringComparison.Ordinal)
                && !result.Contains("Version:", StringComparison.Ordinal)
                && !result.Contains("MoTD:", StringComparison.Ordinal))
            .AssertPassed();
    }
}
