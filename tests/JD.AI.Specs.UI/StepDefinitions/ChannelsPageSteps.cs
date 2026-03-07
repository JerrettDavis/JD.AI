using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class ChannelsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private ChannelsPage _channelsPage = null!;

    public ChannelsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupChannelsPage()
    {
        _page = _context.Get<IPage>();
        _channelsPage = new ChannelsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    // ── Navigation ──────────────────────────────────────────

    [Given(@"I am on the channels page")]
    public async Task GivenIAmOnTheChannelsPage()
    {
        await _channelsPage.NavigateToChannels();
        await _channelsPage.WaitForLoadAsync();
    }

    // ── Rendering: Sync OpenClaw button ─────────────────────
    // Note: "I should see the Sync OpenClaw button with sync icon" is defined in SharedSteps.cs

    // ── Loading states ──────────────────────────────────────

    [Given(@"the channels are loading")]
    public async Task GivenTheChannelsAreLoading()
    {
        // Navigate fresh so we catch the loading state before data arrives
        await _channelsPage.NavigateToChannels();
    }

    [Then(@"I should see (\d+) skeleton channel cards")]
    public async Task ThenIShouldSeeSkeletonChannelCards(int count)
    {
        var skeletons = _channelsPage.SkeletonCards;
        await Expect(skeletons).ToHaveCountAsync(count, new() { Timeout = 3000 });
    }

    // ── Empty state ─────────────────────────────────────────

    [Given(@"there are no configured channels")]
    public async Task GivenThereAreNoConfiguredChannels()
    {
        // If the API returns no channels, the empty state renders
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the channels empty state")]
    public async Task ThenIShouldSeeTheChannelsEmptyState()
    {
        await Expect(_channelsPage.EmptyState).ToBeVisibleAsync();
    }

    // ── Data display ────────────────────────────────────────

    [Given(@"there are configured channels")]
    public async Task GivenThereAreConfiguredChannels()
    {
        // Wait for channel data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see channel cards")]
    public async Task ThenIShouldSeeChannelCards()
    {
        var count = await _channelsPage.ChannelCards.CountAsync();
        Assert.True(count > 0, "Expected at least one channel card");
    }

    [Then(@"each channel card should display a display name")]
    public async Task ThenEachChannelCardShouldDisplayADisplayName()
    {
        var names = _channelsPage.ChannelNames;
        var count = await names.CountAsync();
        Assert.True(count > 0, "Expected at least one channel name");

        for (var i = 0; i < count; i++)
        {
            await Expect(names.Nth(i)).ToBeVisibleAsync();
            var text = await names.Nth(i).TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Channel card {i} should have a display name");
        }
    }

    [Then(@"each channel card should display the channel type")]
    public async Task ThenEachChannelCardShouldDisplayTheChannelType()
    {
        var types = _channelsPage.ChannelTypeTexts;
        var count = await types.CountAsync();
        Assert.True(count > 0, "Expected at least one channel type text");

        for (var i = 0; i < count; i++)
        {
            await Expect(types.Nth(i)).ToBeVisibleAsync();
            var text = await types.Nth(i).TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Channel card {i} should display a channel type");
        }
    }

    // ── Status badges ───────────────────────────────────────

    [Then(@"each channel card should show a status badge")]
    public async Task ThenEachChannelCardShouldShowAStatusBadge()
    {
        var badges = _channelsPage.ChannelStatusBadges;
        var count = await badges.CountAsync();
        Assert.True(count > 0, "Expected status badges on channel cards");
    }

    [Then(@"status badges should display ""Online"" or ""Offline""")]
    public async Task ThenStatusBadgesShouldDisplayOnlineOrOffline()
    {
        var badges = _channelsPage.ChannelStatusBadges;
        var count = await badges.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var text = await badges.Nth(i).TextContentAsync();
            Assert.NotNull(text);
            Assert.True(
                text.Contains("Online", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Offline", StringComparison.OrdinalIgnoreCase),
                $"Expected status badge to show 'Online' or 'Offline', got '{text}'");
        }
    }

    // ── Avatar color checks ─────────────────────────────────

    [Then(@"connected channel avatars should be green")]
    public async Task ThenConnectedChannelAvatarsShouldBeGreen()
    {
        var cards = _channelsPage.ChannelCards;
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var statusText = await cards.Nth(i).Locator("[data-testid='channel-status']").TextContentAsync();
            if (statusText != null && statusText.Contains("Online", StringComparison.OrdinalIgnoreCase))
            {
                var avatar = cards.Nth(i).Locator(".mud-avatar");
                // MudBlazor applies color classes like mud-avatar-outlined-success
                var cssClass = await avatar.GetAttributeAsync("class") ?? "";
                Assert.Contains("success", cssClass, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Then(@"disconnected channel avatars should be default color")]
    public async Task ThenDisconnectedChannelAvatarsShouldBeDefaultColor()
    {
        var cards = _channelsPage.ChannelCards;
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var statusText = await cards.Nth(i).Locator("[data-testid='channel-status']").TextContentAsync();
            if (statusText != null && statusText.Contains("Offline", StringComparison.OrdinalIgnoreCase))
            {
                var avatar = cards.Nth(i).Locator(".mud-avatar");
                var cssClass = await avatar.GetAttributeAsync("class") ?? "";
                Assert.Contains("default", cssClass, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ── Connect/Disconnect buttons by status ────────────────

    [Then(@"connected channels should show a ""Disconnect"" button")]
    public async Task ThenConnectedChannelsShouldShowADisconnectButton()
    {
        var cards = _channelsPage.ChannelCards;
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var statusText = await cards.Nth(i).Locator("[data-testid='channel-status']").TextContentAsync();
            if (statusText != null && statusText.Contains("Online", StringComparison.OrdinalIgnoreCase))
            {
                var disconnectBtn = cards.Nth(i).Locator("[data-testid='channel-disconnect-button']");
                await Expect(disconnectBtn).ToBeVisibleAsync();
            }
        }
    }

    [Then(@"disconnected channels should show a ""Connect"" button")]
    public async Task ThenDisconnectedChannelsShouldShowAConnectButton()
    {
        var cards = _channelsPage.ChannelCards;
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var statusText = await cards.Nth(i).Locator("[data-testid='channel-status']").TextContentAsync();
            if (statusText != null && statusText.Contains("Offline", StringComparison.OrdinalIgnoreCase))
            {
                var connectBtn = cards.Nth(i).Locator("[data-testid='channel-connect-button']");
                await Expect(connectBtn).ToBeVisibleAsync();
            }
        }
    }

    // ── Connect channel ─────────────────────────────────────

    [Given(@"there is a disconnected channel")]
    public async Task GivenThereIsADisconnectedChannel()
    {
        await _page.WaitForTimeoutAsync(500);
        var connectButtons = _channelsPage.ConnectButtons;
        var count = await connectButtons.CountAsync();
        Assert.True(count > 0, "Expected at least one disconnected channel with a Connect button");
    }

    [When(@"I click the ""Connect"" button on the channel")]
    public async Task WhenIClickTheConnectButtonOnTheChannel()
    {
        var connectButton = _channelsPage.ConnectButtons.First;
        await connectButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    // ── Disconnect channel ──────────────────────────────────

    [Given(@"there is a connected channel")]
    public async Task GivenThereIsAConnectedChannel()
    {
        await _page.WaitForTimeoutAsync(500);
        var disconnectButtons = _channelsPage.DisconnectButtons;
        var count = await disconnectButtons.CountAsync();
        Assert.True(count > 0, "Expected at least one connected channel with a Disconnect button");
    }

    [When(@"I click the ""Disconnect"" button on the channel")]
    public async Task WhenIClickTheDisconnectButtonOnTheChannel()
    {
        var disconnectButton = _channelsPage.DisconnectButtons.First;
        await disconnectButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    // ── Override button ─────────────────────────────────────

    [Then(@"each channel card should have an ""Override"" button with edit icon")]
    public async Task ThenEachChannelCardShouldHaveAnOverrideButtonWithEditIcon()
    {
        var overrideButtons = _channelsPage.OverrideButtons;
        var count = await overrideButtons.CountAsync();
        Assert.True(count > 0, "Expected override buttons on channel cards");

        for (var i = 0; i < count; i++)
        {
            await Expect(overrideButtons.Nth(i)).ToBeVisibleAsync();
            // Verify it contains an icon (edit icon)
            var icon = overrideButtons.Nth(i).Locator("svg, .mud-icon-root");
            await Expect(icon.First).ToBeVisibleAsync();
        }
    }

    // ── Override dialog ─────────────────────────────────────

    [When(@"I click the ""Override"" button on a channel")]
    public async Task WhenIClickTheOverrideButtonOnAChannel()
    {
        var overrideButton = _channelsPage.OverrideButtons.First;
        await overrideButton.ClickAsync();
        // Wait for dialog animation
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the override dialog should be visible")]
    public async Task ThenTheOverrideDialogShouldBeVisible()
    {
        await Expect(_channelsPage.OverrideDialog).ToBeVisibleAsync();
    }

    [Then(@"the dialog title should contain the channel name")]
    public async Task ThenTheDialogTitleShouldContainTheChannelName()
    {
        var dialogTitle = _channelsPage.OverrideDialog.Locator(".mud-dialog-title");
        await Expect(dialogTitle).ToBeVisibleAsync();
        var titleText = await dialogTitle.TextContentAsync();
        Assert.NotNull(titleText);
        Assert.Contains("Override:", titleText);
    }

    [Then(@"the dialog should contain an ""Agent ID"" field")]
    public async Task ThenTheDialogShouldContainAnAgentIdField()
    {
        await Expect(_channelsPage.OverrideAgentIdField).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a ""Model"" field")]
    public async Task ThenTheDialogShouldContainAModelField()
    {
        await Expect(_channelsPage.OverrideModelField).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a ""Routing Mode"" dropdown")]
    public async Task ThenTheDialogShouldContainARoutingModeDropdown()
    {
        await Expect(_channelsPage.OverrideRoutingModeDropdown).ToBeVisibleAsync();
    }

    [Then(@"the ""Routing Mode"" dropdown should have options ""Passthrough"", ""Sidecar"", ""Intercept""")]
    public async Task ThenTheRoutingModeDropdownShouldHaveOptions()
    {
        // Click the dropdown to open it
        var dropdown = _channelsPage.OverrideRoutingModeDropdown;
        await dropdown.ClickAsync();
        await _page.WaitForTimeoutAsync(300);

        // Verify each option is present in the dropdown popover
        var popover = _page.Locator(".mud-popover-open");
        await Expect(popover.Locator("text=Passthrough")).ToBeVisibleAsync();
        await Expect(popover.Locator("text=Sidecar")).ToBeVisibleAsync();
        await Expect(popover.Locator("text=Intercept")).ToBeVisibleAsync();

        // Close the dropdown by pressing Escape
        await _page.Keyboard.PressAsync("Escape");
        await _page.WaitForTimeoutAsync(200);
    }

    [Then(@"the dialog should contain an ""Override Enabled"" switch")]
    public async Task ThenTheDialogShouldContainAnOverrideEnabledSwitch()
    {
        await Expect(_channelsPage.OverrideEnabledSwitch).ToBeVisibleAsync();
    }

    [Then(@"the dialog should have ""Cancel"" and ""Save"" buttons")]
    public async Task ThenTheDialogShouldHaveCancelAndSaveButtons()
    {
        await Expect(_channelsPage.OverrideCancelButton).ToBeVisibleAsync();
        await Expect(_channelsPage.OverrideSaveButton).ToBeVisibleAsync();
    }

    // ── Override dialog cancel ──────────────────────────────

    [When(@"I click ""Cancel"" in the override dialog")]
    public async Task WhenIClickCancelInTheOverrideDialog()
    {
        await _channelsPage.OverrideCancelButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the override dialog should close")]
    public async Task ThenTheOverrideDialogShouldClose()
    {
        await Expect(_channelsPage.OverrideDialog).ToBeHiddenAsync(new() { Timeout = 3000 });
    }

    // ── Override dialog save ────────────────────────────────

    [When(@"I fill in the agent ID")]
    public async Task WhenIFillInTheAgentId()
    {
        var agentIdInput = _channelsPage.OverrideAgentIdField.Locator("input");
        await agentIdInput.FillAsync("test-agent-01");
    }

    [When(@"I click ""Save"" in the override dialog")]
    public async Task WhenIClickSaveInTheOverrideDialog()
    {
        await _channelsPage.OverrideSaveButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    // ── Channel icons ───────────────────────────────────────

    [Then(@"each channel card should display an icon matching its type")]
    public async Task ThenEachChannelCardShouldDisplayAnIconMatchingItsType()
    {
        var cards = _channelsPage.ChannelCards;
        var count = await cards.CountAsync();
        Assert.True(count > 0, "Expected at least one channel card");

        for (var i = 0; i < count; i++)
        {
            var avatar = cards.Nth(i).Locator(".mud-avatar");
            await Expect(avatar).ToBeVisibleAsync();
            // Each avatar should contain an icon (SVG or MudIcon)
            var icon = avatar.Locator("svg, .mud-icon-root");
            await Expect(icon.First).ToBeVisibleAsync();
        }
    }
}
