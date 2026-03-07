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
        // Either the MudSelect agent selector or the "No agents running" warning chip
        var selector = _page.Locator(".jd-chat-header .mud-select");
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

    [Then(@"the chat empty state should display ""(.*)""")]
    public async Task ThenTheChatEmptyStateShouldDisplay(string text)
    {
        var element = _page.Locator($".jd-chat-empty >> text={text}");
        await Expect(element).ToBeVisibleAsync();
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
        var clearButton = _page.Locator(".jd-chat-header button").Last;
        await Expect(clearButton).ToBeVisibleAsync();
    }
}
