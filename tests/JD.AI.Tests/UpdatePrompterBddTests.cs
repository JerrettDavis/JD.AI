using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests;

[Feature("Update Prompter")]
public sealed class UpdatePrompterBddTests : TinyBddXunitBase
{
    public UpdatePrompterBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("FormatNotification includes current version"), Fact]
    public async Task FormatNotification_IncludesCurrentVersion()
    {
        string? result = null;

        await Given("an UpdateInfo from 1.0.0 to 2.0.0", () => new UpdateInfo("1.0.0", "2.0.0"))
            .When("FormatNotification is called", info =>
            {
                result = UpdatePrompter.FormatNotification(info);
                return info;
            })
            .Then("the result contains the current version '1.0.0'", _ =>
                result != null && result.Contains("1.0.0"))
            .AssertPassed();
    }

    [Scenario("FormatNotification includes latest version"), Fact]
    public async Task FormatNotification_IncludesLatestVersion()
    {
        string? result = null;

        await Given("an UpdateInfo from 1.0.0 to 2.0.0", () => new UpdateInfo("1.0.0", "2.0.0"))
            .When("FormatNotification is called", info =>
            {
                result = UpdatePrompter.FormatNotification(info);
                return info;
            })
            .Then("the result contains the latest version '2.0.0'", _ =>
                result != null && result.Contains("2.0.0"))
            .AssertPassed();
    }

    [Scenario("FormatNotification includes /update command reference"), Fact]
    public async Task FormatNotification_IncludesUpdateCommand()
    {
        string? result = null;

        await Given("an UpdateInfo from 1.0.0 to 2.0.0", () => new UpdateInfo("1.0.0", "2.0.0"))
            .When("FormatNotification is called", info =>
            {
                result = UpdatePrompter.FormatNotification(info);
                return info;
            })
            .Then("the result contains '/update'", _ =>
                result != null && result.Contains("/update"))
            .AssertPassed();
    }

    [Scenario("FormatNotification includes Update available text"), Fact]
    public async Task FormatNotification_IncludesUpdateAvailable()
    {
        string? result = null;

        await Given("an UpdateInfo from 1.0.0 to 2.0.0", () => new UpdateInfo("1.0.0", "2.0.0"))
            .When("FormatNotification is called", info =>
            {
                result = UpdatePrompter.FormatNotification(info);
                return info;
            })
            .Then("the result contains 'Update available'", _ =>
                result != null && result.Contains("Update available"))
            .AssertPassed();
    }
}
