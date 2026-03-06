using JD.AI.Rendering;
using JD.AI.Startup;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Welcome Service Status Probe")]
public sealed class WelcomeServiceStatusProbeBddTests : TinyBddXunitBase
{
    public WelcomeServiceStatusProbeBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Windows daemon output with RUNNING maps to healthy"), Fact]
    public async Task ParseWindowsDaemonProbe_Running_IsHealthy()
    {
        WelcomeIndicator? indicator = null;

        await Given("a successful sc query output containing RUNNING", () =>
                "SERVICE_NAME: JDAIDaemon\nSTATE              : 4  RUNNING")
            .When("parsing the daemon probe output", output =>
            {
                indicator = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(0, output, timedOut: false);
                return output;
            })
            .Then("the daemon is reported as running and healthy", _ =>
                indicator is { Name: "Daemon", Value: "running", State: IndicatorState.Healthy })
            .AssertPassed();
    }

    [Scenario("Windows daemon output with 1060 maps to not installed"), Fact]
    public async Task ParseWindowsDaemonProbe_NotInstalled_IsWarning()
    {
        WelcomeIndicator? indicator = null;

        await Given("an sc query output indicating missing service", () =>
                "[SC] OpenService FAILED 1060: The specified service does not exist as an installed service.")
            .When("parsing the daemon probe output", output =>
            {
                indicator = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(1, output, timedOut: false);
                return output;
            })
            .Then("the daemon is reported as not installed", _ =>
                indicator is { Name: "Daemon", Value: "not installed", State: IndicatorState.Warning })
            .AssertPassed();
    }

    [Scenario("Systemd active output maps to healthy"), Fact]
    public async Task ParseSystemdDaemonProbe_Active_IsHealthy()
    {
        WelcomeIndicator? indicator = null;

        await Given("a systemctl output of active", () => "active")
            .When("parsing the daemon probe output", output =>
            {
                indicator = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(0, output, timedOut: false);
                return output;
            })
            .Then("the daemon is reported as running and healthy", _ =>
                indicator is { Name: "Daemon", Value: "running", State: IndicatorState.Healthy })
            .AssertPassed();
    }

    [Scenario("Systemd failed output maps to error"), Fact]
    public async Task ParseSystemdDaemonProbe_Failed_IsError()
    {
        WelcomeIndicator? indicator = null;

        await Given("a systemctl output of failed", () => "failed")
            .When("parsing the daemon probe output", output =>
            {
                indicator = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(3, output, timedOut: false);
                return output;
            })
            .Then("the daemon is reported as failed", _ =>
                indicator is { Name: "Daemon", Value: "failed", State: IndicatorState.Error })
            .AssertPassed();
    }

    [Scenario("Systemd inactive output maps to stopped warning"), Fact]
    public async Task ParseSystemdDaemonProbe_Inactive_IsWarning()
    {
        WelcomeIndicator? indicator = null;

        await Given("a systemctl output of inactive", () => "inactive")
            .When("parsing the daemon probe output", output =>
            {
                indicator = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(3, output, timedOut: false);
                return output;
            })
            .Then("the daemon is reported as stopped", _ =>
                indicator is { Name: "Daemon", Value: "stopped", State: IndicatorState.Warning })
            .AssertPassed();
    }

    [Scenario("Gateway URI uses configured JDAI_GATEWAY_URL when present"), Fact]
    public async Task ResolveGatewayHealthUri_UsesConfiguredBaseUrl()
    {
        Uri? healthUri = null;
        var previous = Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL");

        try
        {
            await Given("a configured gateway base URL", () =>
                {
                    Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", "http://127.0.0.1:7777");
                    return new CliOptions();
                })
                .When("resolving the gateway health URI", opts =>
                {
                    healthUri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(opts);
                    return opts;
                })
                .Then("the health endpoint is derived from the configured URL", _ =>
                    healthUri is not null
                    && string.Equals(healthUri.ToString(), "http://127.0.0.1:7777/health", StringComparison.Ordinal))
                .AssertPassed();
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", previous);
        }
    }
}
