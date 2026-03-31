using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class ChatPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private ChatPage _chatPage = null!;

    public ChatPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupChatPage()
    {
        _page = _context.Get<IPage>();
        _chatPage = new ChatPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the chat page")]
    public async Task GivenIAmOnTheChatPage()
    {
        await _chatPage.NavigateToChat();
        await _chatPage.WaitForLoadAsync();
    }

    [Then(@"I should see the web chat header")]
    public async Task ThenIShouldSeeTheWebChatHeader()
    {
        var header = _page.Locator(".jd-chat-header");
        await Expect(header).ToBeVisibleAsync();
    }

    [Then(@"the header should display ""(.*)""")]
    public async Task ThenTheHeaderShouldDisplay(string text)
    {
        var headerText = _page.Locator($".jd-chat-header >> text={text}");
        await Expect(headerText).ToBeVisibleAsync();
    }

    [Then(@"I should see the message input field")]
    public async Task ThenIShouldSeeTheMessageInputField()
    {
        var input = _page.Locator(".jd-chat-input input");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"the message input should have placeholder text")]
    public async Task ThenTheMessageInputShouldHavePlaceholderText()
    {
        var input = _page.Locator(".jd-chat-input input");
        var placeholder = await input.GetAttributeAsync("placeholder");
        Assert.NotNull(placeholder);
        Assert.NotEmpty(placeholder);
    }

    [Then(@"I should see the agent selector or no-agents warning")]
    public async Task ThenIShouldSeeTheAgentSelectorOrNoAgentsWarning()
    {
        // Use the data-testid on the agent-selector MudSelect, or fall back
        // to the "No agents running" warning chip.
        var selector = _page.Locator("[data-testid=agent-selector]");
        var warning = _page.Locator(".jd-chat-header >> text=No agents running");

        var selectorVisible = await selector.IsVisibleAsync();
        var warningVisible = await warning.IsVisibleAsync();

        Assert.True(selectorVisible || warningVisible,
            "Expected either the agent selector or the no-agents warning to be visible");
    }

    [Then(@"I should see the chat empty state")]
    public async Task ThenIShouldSeeTheChatEmptyState()
    {
        var emptyState = _page.Locator(".jd-chat-empty");
        await Expect(emptyState).ToBeVisibleAsync();
    }

    [Given(@"an agent is selected")]
    public async Task GivenAnAgentIsSelected()
    {
        // Verify an agent is selected in the dropdown, or skip if no agents
        var selector = _page.Locator(".jd-chat-header .mud-select");
        var isVisible = await selector.IsVisibleAsync();
        if (!isVisible)
        {
            // No agents available; scenario will proceed but send will likely not work
            return;
        }
        await _page.WaitForTimeoutAsync(300);
    }

    [When(@"I type ""(.*)"" in the message input")]
    public async Task WhenITypeInTheMessageInput(string text)
    {
        var input = _page.Locator(".jd-chat-input input");
        await input.FillAsync(text);
    }

    [When(@"I send the message")]
    public async Task WhenISendTheMessage()
    {
        // Press Enter to send
        var input = _page.Locator(".jd-chat-input input");
        await input.PressAsync("Enter");
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"a user message bubble should appear")]
    public async Task ThenAUserMessageBubbleShouldAppear()
    {
        var userBubble = _page.Locator(".jd-chat-user");
        await Expect(userBubble.First).ToBeVisibleAsync();
    }

    [Then(@"the message bubble should contain ""(.*)""")]
    public async Task ThenTheMessageBubbleShouldContain(string text)
    {
        var bubble = _page.Locator($".jd-chat-user >> text={text}");
        await Expect(bubble).ToBeVisibleAsync();
    }

    [Then(@"I should see the clear chat button")]
    public async Task ThenIShouldSeeTheClearChatButton()
    {
        // The clear/DeleteSweep button is in the chat header
        var clearButton = _chatPage.ClearChatButton;
        await Expect(clearButton).ToBeVisibleAsync();
    }

    // ── Chat header icon ──────────────────────────────────────

    [Then(@"the header should have a chat icon")]
    public async Task ThenTheHeaderShouldHaveAChatIcon()
    {
        var icon = _chatPage.ChatHeaderIcon;
        await Expect(icon.First).ToBeVisibleAsync();
    }

    [Then(@"I should see the chat header")]
    public async Task ThenIShouldSeeTheChatHeader()
    {
        await Expect(_chatPage.ChatHeader).ToBeVisibleAsync();
    }

    // ── Placeholder text (parameterized) ──────────────────────

    [Then(@"the message input should have placeholder ""(.*)""")]
    public async Task ThenTheMessageInputShouldHavePlaceholder(string expectedPlaceholder)
    {
        // Try data-testid locator first, fall back to CSS class locator
        var input = _chatPage.MessageInput;
        if (await input.CountAsync() == 0)
            input = _page.Locator(".jd-chat-input input");

        var placeholder = await input.GetAttributeAsync("placeholder");
        Assert.NotNull(placeholder);
        Assert.Contains(expectedPlaceholder, placeholder, StringComparison.Ordinal);
    }

    // ── Send icon ─────────────────────────────────────────────

    [Then(@"the message input should have a send icon")]
    public async Task ThenTheMessageInputShouldHaveASendIcon()
    {
        // Try data-testid locator first, fall back to CSS class locator
        var sendIcon = _chatPage.SendIcon;
        if (await sendIcon.CountAsync() == 0)
            sendIcon = _page.Locator(".jd-chat-input .mud-input-adornment-end .mud-icon-root");

        await Expect(sendIcon).ToBeVisibleAsync();
    }

    // ── Agent selector ────────────────────────────────────────

    [Then(@"I should see the agent selector dropdown")]
    public async Task ThenIShouldSeeTheAgentSelectorDropdown()
    {
        await Expect(_chatPage.AgentSelector).ToBeVisibleAsync();
    }

    [Then(@"the dropdown should contain at least one agent option")]
    public async Task ThenTheDropdownShouldContainAtLeastOneAgentOption()
    {
        // Click the select to open the dropdown and verify options
        await _chatPage.AgentSelector.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
        var options = _page.Locator(".mud-popover-open .mud-list-item");
        var count = await options.CountAsync();
        Assert.True(count > 0, "Expected at least one agent option in the dropdown");
        // Close the dropdown by pressing Escape
        await _page.Keyboard.PressAsync("Escape");
    }

    // ── No agents warning ─────────────────────────────────────

    [Given(@"there are no running agents")]
    public async Task GivenThereAreNoRunningAgents()
    {
        // The page is loaded; if no agents are returned from the API,
        // the warning chip is shown instead of the agent selector
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see ""(.*)"" warning chip")]
    public async Task ThenIShouldSeeWarningChip(string text)
    {
        var chip = _chatPage.NoAgentsWarning;
        await Expect(chip).ToBeVisibleAsync();
        await Expect(chip).ToContainTextAsync(text);
    }

    // ── Input disabled ────────────────────────────────────────

    [Then(@"the message input should be disabled")]
    public async Task ThenTheMessageInputShouldBeDisabled()
    {
        var input = _chatPage.MessageInput;
        await Expect(input).ToBeDisabledAsync();
    }

    // ── Press Enter ───────────────────────────────────────────

    [When(@"I press Enter in the message input")]
    public async Task WhenIPressEnterInTheMessageInput()
    {
        var input = _page.Locator(".jd-chat-input input");
        await input.PressAsync("Enter");
        await _page.WaitForTimeoutAsync(300);
    }

    // ── No message bubble ─────────────────────────────────────

    [Then(@"no message bubble should appear")]
    public async Task ThenNoMessageBubbleShouldAppear()
    {
        var bubbles = _page.Locator(".jd-chat-bubble");
        var count = await bubbles.CountAsync();
        Assert.Equal(0, count);
    }

    // ── User message bubble on the right ──────────────────────

    [Then(@"a user message bubble should appear on the right")]
    public async Task ThenAUserMessageBubbleShouldAppearOnTheRight()
    {
        var userBubble = _chatPage.UserBubbles.First;
        await Expect(userBubble).ToBeVisibleAsync();
        // Verify alignment: the user bubble has jd-chat-user class which applies align-self: flex-end
        var classes = await userBubble.GetAttributeAsync("class");
        Assert.NotNull(classes);
        Assert.Contains("jd-chat-user", classes, StringComparison.Ordinal);
    }

    // ── "You" label and timestamp ─────────────────────────────

    [Then(@"the user bubble should show ""(.*)"" label")]
    public async Task ThenTheUserBubbleShouldShowLabel(string label)
    {
        var labelLocator = _page.Locator($"[data-testid='chat-bubble-user'] .jd-chat-bubble-header >> text={label}");
        await Expect(labelLocator).ToBeVisibleAsync();
    }

    [Then(@"the user bubble should show a timestamp")]
    public async Task ThenTheUserBubbleShouldShowATimestamp()
    {
        var timestamp = _chatPage.UserBubbleTimestamp;
        await Expect(timestamp.First).ToBeVisibleAsync();
        var text = await timestamp.First.TextContentAsync();
        Assert.NotNull(text);
        // Timestamp format is HH:mm:ss
        Assert.Matches(@"\d{1,2}:\d{2}:\d{2}", text);
    }

    // ── Streaming indicator ───────────────────────────────────

    [Then(@"a streaming indicator should appear")]
    public async Task ThenAStreamingIndicatorShouldAppear()
    {
        // The streaming cursor blink indicator appears during streaming
        var cursor = _chatPage.StreamingCursor;
        // Use a timeout since streaming may start after a brief delay
        await Expect(cursor).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // ── Agent response bubble ─────────────────────────────────

    [Then(@"an agent response bubble should eventually appear on the left")]
    public async Task ThenAnAgentResponseBubbleShouldEventuallyAppearOnTheLeft()
    {
        var agentBubble = _chatPage.AgentBubbles.First;
        // Agent response may take time to stream fully
        await Expect(agentBubble).ToBeVisibleAsync(new() { Timeout = 30000 });
        var classes = await agentBubble.GetAttributeAsync("class");
        Assert.NotNull(classes);
        Assert.Contains("jd-chat-agent", classes, StringComparison.Ordinal);
    }

    // ── Clear chat disabled ───────────────────────────────────

    [Then(@"the clear chat button should be disabled")]
    public async Task ThenTheClearChatButtonShouldBeDisabled()
    {
        await Expect(_chatPage.ClearChatButton).ToBeDisabledAsync();
    }

    // ── Send and clear chat ───────────────────────────────────

    [Given(@"I have sent a message ""(.*)""")]
    public async Task GivenIHaveSentAMessage(string message)
    {
        var input = _page.Locator(".jd-chat-input input");
        await input.FillAsync(message);
        await input.PressAsync("Enter");
        await _page.WaitForTimeoutAsync(500);
    }

    [When(@"I click the clear chat button")]
    public async Task WhenIClickTheClearChatButton()
    {
        await _chatPage.ClearChatButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"no message bubbles should be visible")]
    public async Task ThenNoMessageBubblesShouldBeVisible()
    {
        var bubbles = _page.Locator(".jd-chat-bubble");
        await Expect(bubbles).ToHaveCountAsync(0);
    }

    [Then(@"the empty state should be displayed")]
    public async Task ThenTheEmptyStateShouldBeDisplayed()
    {
        await Expect(_chatPage.EmptyState).ToBeVisibleAsync();
    }

    // ── Error handling ────────────────────────────────────────

    [When(@"a chat error occurs")]
    public async Task WhenAChatErrorOccurs()
    {
        // Simulate an error by sending a message to a non-existent or broken agent
        // The error handling in the component will trigger a snackbar
        // For testing, we attempt to send a message that will cause an error response
        var input = _page.Locator(".jd-chat-input input");
        var isDisabled = await input.IsDisabledAsync();
        if (!isDisabled)
        {
            await input.FillAsync("trigger-error-test");
            await input.PressAsync("Enter");
            await _page.WaitForTimeoutAsync(1000);
        }
    }
}
