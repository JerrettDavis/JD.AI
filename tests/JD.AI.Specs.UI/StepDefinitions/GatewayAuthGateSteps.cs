using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

/// <summary>
/// Step definitions for the Gateway Auth Gate feature.
/// </summary>
[Binding]
public sealed class GatewayAuthGateSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private string _baseUrl = "";

    public GatewayAuthGateSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void Setup()
    {
        _page = _context.Get<IPage>();
        _baseUrl = _context.Get<DashboardFixture>().BaseUrl.TrimEnd('/');
    }

    // ── Gateway connectivity preconditions ─────────────────────────────────

    [Given(@"the gateway is disconnected")]
    public void GivenTheGatewayIsDisconnected()
    {
        // In CI there is no gateway running, so the SignalR service will never
        // connect.  This step is a no-op: the disconnected state is the default.
    }

    [Given(@"the gateway is connected")]
    public void GivenTheGatewayIsConnected()
    {
        // Requires a live gateway; skip when none is available.
        Xunit.Skip.If(true, "Gateway is not running in this environment.");
    }

    [Given(@"localStorage has ""(.*)"" set to ""(.*)""")]
    public async Task GivenLocalStorageHasSetTo(string key, string value)
    {
        await _page.EvaluateAsync($"localStorage.setItem('{key}', '{value}')");
    }

    // ── Navigation ─────────────────────────────────────────────────────────

    [When(@"I navigate to the home page")]
    public async Task WhenINavigateToTheHomePage()
    {
        await _page.GotoAsync($"{_baseUrl}/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(500);
    }

    // ── Auth gate visibility ────────────────────────────────────────────────

    [Then(@"I should see the auth gate overlay")]
    public async Task ThenIShouldSeeTheAuthGateOverlay()
    {
        await Expect(_page.Locator("[data-testid='auth-gate']")).ToBeVisibleAsync();
    }

    [Then(@"I should not see the auth gate overlay")]
    public async Task ThenIShouldNotSeeTheAuthGateOverlay()
    {
        await Expect(_page.Locator("[data-testid='auth-gate']")).ToBeHiddenAsync();
    }

    // ── Auth gate fields ────────────────────────────────────────────────────

    [Then(@"I should see a URL input prefilled with ""(.*)""")]
    public async Task ThenIShouldSeeAUrlInputPrefilledWith(string expectedValue)
    {
        var input = _page.Locator("[data-testid='gateway-url-input'] input");
        await Expect(input).ToBeVisibleAsync();
        var value = await input.InputValueAsync();
        Xunit.Assert.Equal(expectedValue, value);
    }

    [Then(@"the URL input should show ""(.*)""")]
    public async Task ThenTheUrlInputShouldShow(string expectedValue)
    {
        var input = _page.Locator("[data-testid='gateway-url-input'] input");
        await Expect(input).ToBeVisibleAsync();
        var value = await input.InputValueAsync();
        Xunit.Assert.Equal(expectedValue, value);
    }

    [Then(@"I should see a Connect button")]
    public async Task ThenIShouldSeeAConnectButton()
    {
        await Expect(_page.Locator("[data-testid='connect-button']")).ToBeVisibleAsync();
    }

    // ── Auth gate interaction ───────────────────────────────────────────────

    [When(@"I enter ""(.*)"" in the URL input")]
    public async Task WhenIEnterInTheUrlInput(string url)
    {
        var input = _page.Locator("[data-testid='gateway-url-input'] input");
        await input.FillAsync(url);
    }

    [When(@"I click the Connect button")]
    public async Task WhenIClickTheConnectButton()
    {
        await _page.Locator("[data-testid='connect-button']").ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [When(@"I click the Connect button with an invalid URL")]
    public async Task WhenIClickTheConnectButtonWithAnInvalidUrl()
    {
        var input = _page.Locator("[data-testid='gateway-url-input'] input");
        await input.FillAsync("invalid-url");
        await _page.Locator("[data-testid='connect-button']").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    [When(@"the gateway connects successfully")]
    public void WhenTheGatewayConnectsSuccessfully()
    {
        Xunit.Skip.If(true, "Gateway connection cannot be simulated without a live gateway.");
    }

    // ── Toast / error notification ──────────────────────────────────────────

    [Then(@"I should see a toast error notification")]
    public async Task ThenIShouldSeeAToastErrorNotification()
    {
        var errorAlert = _page.Locator("[data-testid='auth-gate-error'], .mud-snackbar-error");
        await Expect(errorAlert.First).ToBeVisibleAsync(new() { Timeout = 5000 });
    }
}
