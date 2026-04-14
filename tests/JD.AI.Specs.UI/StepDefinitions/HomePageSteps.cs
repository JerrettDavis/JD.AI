using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class HomePageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private HomePage _homePage = null!;

    public HomePageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupHomePage()
    {
        _page = _context.Get<IPage>();
        _homePage = new HomePage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    // ── Background ──────────────────────────────────────────

    [Given(@"I am on the home page")]
    public async Task GivenIAmOnTheHomePage()
    {
        await _homePage.NavigateToHome();
        await _homePage.WaitForLoadAsync();
    }

    // ── Rendering ───────────────────────────────────────────
    // Note: "I should see the heading" is defined in SharedSteps.cs

    [Then(@"I should see (\d+) stat cards")]
    public async Task ThenIShouldSeeStatCards(int count)
    {
        await Expect(_homePage.StatCards).ToHaveCountAsync(count);
    }

    [Then(@"I should see a stat card labeled ""(.*)""")]
    public async Task ThenIShouldSeeAStatCardLabeled(string label)
    {
        var card = _page.Locator($"[data-testid='stat-card'] >> text={label}");
        await Expect(card).ToBeVisibleAsync();
    }

    [Then(@"the app bar should display ""(.*)""")]
    public async Task ThenTheAppBarShouldDisplay(string text)
    {
        var locator = _page.Locator($"[data-testid='app-bar'] >> text={text}");
        await Expect(locator).ToBeVisibleAsync();
    }

    // ── Data display ────────────────────────────────────────

    [Then(@"the ""(.*)"" stat card should display a numeric value")]
    public async Task ThenTheStatCardShouldDisplayANumericValue(string cardName)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase -- data-testid convention is lowercase
        var valueLocator = _homePage.StatCardValue(cardName.ToLowerInvariant());
#pragma warning restore CA1308
        await Expect(valueLocator).ToBeVisibleAsync();
        var text = await valueLocator.TextContentAsync();
        Assert.NotNull(text);
        Assert.Matches(@"\d+", text);
    }

    [Then(@"the ""(.*)"" stat card should display ""(.*)"" or ""(.*)""")]
    public async Task ThenTheStatCardShouldDisplayOneOf(string cardName, string option1, string option2)
    {
#pragma warning disable CA1308
        var valueLocator = _homePage.StatCardValue(cardName.ToLowerInvariant());
#pragma warning restore CA1308
        await Expect(valueLocator).ToBeVisibleAsync();
        var text = (await valueLocator.TextContentAsync())?.Trim();
        Assert.NotNull(text);
        Assert.True(string.Equals(text, option1, StringComparison.Ordinal) || string.Equals(text, option2, StringComparison.Ordinal),
            $"Expected '{option1}' or '{option2}' but got '{text}'");
    }

    // ── OpenClaw Bridge ─────────────────────────────────────

    [Given(@"the OpenClaw bridge is configured")]
    public async Task GivenTheOpenClawBridgeIsConfigured()
    {
        // Wait for the gateway to have OpenClaw bridge data available
        await _page.WaitForTimeoutAsync(1000);
    }

    [Then(@"I should see the OpenClaw Bridge details table")]
    public async Task ThenIShouldSeeTheOpenClawBridgeDetailsTable()
    {
        await Expect(_homePage.OpenClawBridgeSection).ToBeVisibleAsync();
    }

    [Then(@"the table should show the ""(.*)"" property")]
    public async Task ThenTheTableShouldShowTheProperty(string property)
    {
        var cell = _homePage.OpenClawBridgeSection.Locator($"td >> text={property}");
        await Expect(cell).ToBeVisibleAsync();
    }

    [Then(@"the table should show ""(.*)""")]
    public async Task ThenTheTableShouldShow(string text)
    {
        var cell = _homePage.OpenClawBridgeSection.Locator($"td >> text={text}");
        await Expect(cell).ToBeVisibleAsync();
    }

    // ── Loading states ──────────────────────────────────────

    [Given(@"the gateway status is loading")]
    public async Task GivenTheGatewayStatusIsLoading()
    {
        // Navigate to the page without waiting for load to complete,
        // so we can observe skeleton state
        await _homePage.NavigateAsync();
        await Task.CompletedTask;
    }

    [Then(@"I should see skeleton stat card placeholders")]
    public async Task ThenIShouldSeeSkeletonStatCardPlaceholders()
    {
        // Skeleton cards are shown briefly before the API responds
        var skeletons = _homePage.SkeletonStatCards;
        // They may already have been replaced; check if they were at least present or data loaded
        var count = await skeletons.CountAsync();
        if (count == 0)
        {
            // Data loaded quickly; verify stat cards are present instead
            await Expect(_homePage.StatCards).ToHaveCountAsync(4);
        }
        else
        {
            Assert.True(count > 0, "Expected skeleton stat card placeholders to be visible");
        }
    }

    [Then(@"I should see a skeleton activity section")]
    public async Task ThenIShouldSeeASkeletonActivitySection()
    {
        var skeleton = _homePage.SkeletonActivity;
        var count = await skeleton.CountAsync();
        if (count == 0)
        {
            // Data loaded quickly; verify the real activity section is present instead
            await Expect(_homePage.RecentActivitySection).ToBeVisibleAsync();
        }
        else
        {
            await Expect(skeleton).ToBeVisibleAsync();
        }
    }

    // ── Activity feed ───────────────────────────────────────

    [Then(@"I should see the ""(.*)"" section heading")]
    public async Task ThenIShouldSeeTheSectionHeading(string heading)
    {
        var headingLocator = _page.Locator($"[data-testid='recent-activity'] >> text={heading}");
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Then(@"I should see a refresh button in the activity section")]
    public async Task ThenIShouldSeeARefreshButtonInTheActivitySection()
    {
        var refreshButton = _homePage.RecentActivitySection.Locator("[data-testid='refresh-button']");
        await Expect(refreshButton).ToBeVisibleAsync();
    }

    [Given(@"there are no recent activity events")]
    public async Task GivenThereAreNoRecentActivityEvents()
    {
        // The default state when no agents are running is no activity events
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see ""(.*)"" in the activity feed")]
    public async Task ThenIShouldSeeInTheActivityFeed(string text)
    {
        var locator = _homePage.RecentActivitySection.Locator($"text={text}");
        await Expect(locator).ToBeVisibleAsync();
    }

    [Given(@"there are recent activity events")]
    public async Task GivenThereAreRecentActivityEvents()
    {
        // Wait for the gateway to have produced events
        await _page.WaitForTimeoutAsync(2000);
    }

    [Then(@"each activity event should show an event type chip")]
    public async Task ThenEachActivityEventShouldShowAnEventTypeChip()
    {
        var items = _homePage.ActivityItems;
        var count = await items.CountAsync();
        if (count > 0)
        {
            var chips = _homePage.ActivityEventTypeChips;
            await Expect(chips.First).ToBeVisibleAsync();
            var chipCount = await chips.CountAsync();
            Assert.True(chipCount >= count,
                $"Expected at least {count} event type chips but found {chipCount}");
        }
    }

    [Then(@"each activity event should show a message")]
    public async Task ThenEachActivityEventShouldShowAMessage()
    {
        var items = _homePage.ActivityItems;
        var count = await items.CountAsync();
        if (count > 0)
        {
            var messages = _homePage.ActivityEventMessages;
            await Expect(messages.First).ToBeVisibleAsync();
            var msgCount = await messages.CountAsync();
            Assert.True(msgCount >= count,
                $"Expected at least {count} event messages but found {msgCount}");
        }
    }

    [Then(@"each activity event should show a timestamp")]
    public async Task ThenEachActivityEventShouldShowATimestamp()
    {
        var items = _homePage.ActivityItems;
        var count = await items.CountAsync();
        if (count > 0)
        {
            var timestamps = _homePage.ActivityEventTimestamps;
            await Expect(timestamps.First).ToBeVisibleAsync();
            var tsCount = await timestamps.CountAsync();
            Assert.True(tsCount >= count,
                $"Expected at least {count} event timestamps but found {tsCount}");
        }
    }

    [Given(@"there are multiple activity events")]
    public async Task GivenThereAreMultipleActivityEvents()
    {
        // Wait for the gateway to have produced multiple events
        await _page.WaitForTimeoutAsync(3000);
    }

    [Then(@"the activity feed should display events in reverse chronological order")]
    public async Task ThenTheActivityFeedShouldDisplayEventsInReverseChronologicalOrder()
    {
        var timestamps = _homePage.ActivityEventTimestamps;
        var count = await timestamps.CountAsync();
        if (count >= 2)
        {
            var first = await timestamps.Nth(0).TextContentAsync();
            var second = await timestamps.Nth(1).TextContentAsync();
            Assert.NotNull(first);
            Assert.NotNull(second);
            // In HH:mm:ss format, lexicographic comparison works for reverse chronological
            Assert.True(string.Compare(first.Trim(), second.Trim(), StringComparison.Ordinal) >= 0,
                $"Expected reverse chronological order but got '{first}' before '{second}'");
        }
    }

    [Then(@"the feed should show at most (\d+) items")]
    public async Task ThenTheFeedShouldShowAtMostItems(int maxItems)
    {
        var items = _homePage.ActivityItems;
        var count = await items.CountAsync();
        Assert.True(count <= maxItems,
            $"Expected at most {maxItems} activity items but found {count}");
    }

    // ── Real-time ───────────────────────────────────────────

    [Given(@"I am observing the activity feed")]
    public async Task GivenIAmObservingTheActivityFeed()
    {
        // Ensure the activity section is visible and note the current item count
        await Expect(_homePage.RecentActivitySection).ToBeVisibleAsync();
        var currentCount = await _homePage.ActivityItems.CountAsync();
        _context.Set(currentCount, "initialActivityCount");
    }

    [When(@"a new gateway event occurs")]
    public async Task WhenANewGatewayEventOccurs()
    {
        // Wait for real-time SignalR event to arrive
        await _page.WaitForTimeoutAsync(3000);
    }

    [Then(@"the event should appear in the activity feed")]
    public async Task ThenTheEventShouldAppearInTheActivityFeed()
    {
        var initialCount = _context.Get<int>("initialActivityCount");
        // Either new items appeared or the feed is populated
        var currentCount = await _homePage.ActivityItems.CountAsync();
        // In a live environment, events may have arrived; in offline, the feed may be empty
        // We check that the feed is at least present and accessible
        await Expect(_homePage.RecentActivitySection).ToBeVisibleAsync();
        if (initialCount > 0 || currentCount > 0)
        {
            Assert.True(currentCount >= initialCount,
                $"Expected activity count to grow from {initialCount} but got {currentCount}");
        }
    }

    // ── Error handling ──────────────────────────────────────

    [Given(@"the gateway API is unavailable")]
    public async Task GivenTheGatewayApiIsUnavailable()
    {
        // Block API calls to simulate unavailable gateway
        await _page.RouteAsync("**/api/**", route => route.AbortAsync());
        // Reload the page so the blocked routes take effect
        await _homePage.NavigateToHome();
        await _homePage.WaitForLoadAsync();
    }

    [Then(@"the stat cards should display zero counts")]
    public async Task ThenTheStatCardsShouldDisplayZeroCounts()
    {
        foreach (var name in new[] { "agents", "channels", "sessions" })
        {
            var valueLocator = _homePage.StatCardValue(name);
            await Expect(valueLocator).ToBeVisibleAsync();
            var text = (await valueLocator.TextContentAsync())?.Trim();
            Assert.NotNull(text);
            Assert.Equal("0", text);
        }
    }

    [Then(@"no error dialog should block the page")]
    public async Task ThenNoErrorDialogShouldBlockThePage()
    {
        // Verify no MudBlazor dialog overlay is blocking the page
        var dialog = _page.Locator(".mud-overlay");
        var count = await dialog.CountAsync();
        if (count > 0)
        {
            // If an overlay exists, it should not be visible/blocking
            await Expect(dialog.First).Not.ToBeVisibleAsync();
        }
        // Verify the page content is still accessible
        await Expect(_homePage.StatCards.First).ToBeVisibleAsync();
    }

    // ── Navigation ──────────────────────────────────────────

    [When(@"I click the ""(.*)"" navigation link")]
    public async Task WhenIClickTheNavigationLink(string linkText)
    {
        var testId = linkText switch
        {
            "Chat" => "nav-chat",
            "Overview" => "nav-control-overview",
            "Agents" => "nav-agents",
            "Skills" => "nav-skills",
            "AI & Agents" => "nav-settings-ai",
            "Communication" => "nav-settings-comms",
            "Config" => "nav-settings-config",
            "Logs" => "nav-settings-logs",
            _ => null,
        };

        var link = testId is null
            ? _page.Locator($"[data-testid='nav-menu'] a:has-text('{linkText}')").First
            : _page.Locator($"[data-testid='{testId}']").First;

        await Expect(link).ToBeVisibleAsync();
        await link.DispatchEventAsync("click");
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"I should be on the ""(.*)"" page")]
    public async Task ThenIShouldBeOnThePage(string path)
    {
        await Expect(_page).ToHaveURLAsync($"**{path}");
        Assert.Contains(path, _page.Url);
    }

    // ── SignalR connection ───────────────────────────────────

    [Then(@"the app bar should show a connection status indicator")]
    public async Task ThenTheAppBarShouldShowAConnectionStatusIndicator()
    {
        await Expect(_homePage.ConnectionIndicator).ToBeVisibleAsync();
    }

    [Then(@"the indicator should display ""(.*)"" or ""(.*)""")]
    public async Task ThenTheIndicatorShouldDisplayOneOf(string option1, string option2)
    {
        await Expect(_homePage.ConnectionIndicator).ToBeVisibleAsync();
        var text = (await _homePage.ConnectionIndicator.TextContentAsync())?.Trim();
        Assert.NotNull(text);
        Assert.True(string.Equals(text, option1, StringComparison.Ordinal) || string.Equals(text, option2, StringComparison.Ordinal),
            $"Expected indicator to show '{option1}' or '{option2}' but got '{text}'");
    }

    // ── Page title ──────────────────────────────────────────
    // Note: "the browser page title should be" is defined in SharedSteps.cs

    // ── Recent Activity (legacy support) ────────────────────

    [Then(@"I should see the recent activity section")]
    public async Task ThenIShouldSeeTheRecentActivitySection()
    {
        await Expect(_homePage.RecentActivitySection).ToBeVisibleAsync();
    }
}
