using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Chat page.
/// Route: /chat
/// </summary>
public sealed class ChatPage : BasePage
{
    public ChatPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/chat";

    // ── Navigation helpers ──
    public async Task NavigateToChat() => await NavigateAsync();

    // ── Chat header ──
    public ILocator ChatHeader => Page.Locator("[data-testid='chat-header']");
    public ILocator WebChatTitle => Page.Locator("[data-testid='chat-header'] >> text=Web Chat");

    // ── Agent selector ──
    public ILocator AgentSelector => Page.Locator("[data-testid='agent-selector']");
    public ILocator NoAgentsWarning => Page.Locator("[data-testid='no-agents-warning']");

    // ── Message input ──
    public ILocator MessageInput => Page.Locator("[data-testid='message-input'] input, [data-testid='message-input'] textarea");
    public ILocator SendButton => Page.Locator("[data-testid='send-button']");
    public ILocator ClearChatButton => Page.Locator("[data-testid='clear-chat-button']");

    // ── Messages area ──
    public ILocator MessagesContainer => Page.Locator("[data-testid='messages-container']");
    public ILocator ChatBubbles => Page.Locator("[data-testid='chat-bubble']");
    public ILocator UserBubbles => Page.Locator("[data-testid='chat-bubble-user']");
    public ILocator AgentBubbles => Page.Locator("[data-testid='chat-bubble-agent']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='chat-empty']");
    public ILocator EmptyStateTitle => Page.Locator(".jd-chat-empty >> text=Start a conversation");

    // ── Streaming indicator ──
    public ILocator StreamingCursor => Page.Locator(".jd-cursor");

    /// <summary>Type and send a message.</summary>
    public async Task SendMessageAsync(string text)
    {
        await MessageInput.FillAsync(text);
        await SendButton.ClickAsync();
    }
}
