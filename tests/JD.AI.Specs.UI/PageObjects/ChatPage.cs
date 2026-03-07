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
    public ILocator MessageInput => Page.Locator(".jd-chat-input input, .jd-chat-input textarea");
    public ILocator SendButton => Page.Locator("[data-testid='send-button']");
    public ILocator ClearChatButton => Page.Locator("[data-testid='clear-chat-button']");

    // ── Messages area ──
    public ILocator MessagesContainer => Page.Locator(".jd-chat-messages");
    public ILocator ChatBubbles => Page.Locator(".jd-chat-bubble");
    public ILocator UserBubbles => Page.Locator("[data-testid='chat-bubble-user']");
    public ILocator AgentBubbles => Page.Locator("[data-testid='chat-bubble-agent']");

    // ── Bubble content helpers ──
    public ILocator UserBubbleLabel => Page.Locator("[data-testid='chat-bubble-user'] .jd-chat-bubble-header >> text=You");
    public ILocator UserBubbleTimestamp => Page.Locator("[data-testid='chat-bubble-user'] .jd-chat-bubble-header .mud-typography-caption.ml-auto");
    public ILocator BubbleContent(string text) => Page.Locator($".jd-chat-bubble >> text={text}");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='chat-empty']");
    public ILocator EmptyStateTitle => Page.Locator("[data-testid='chat-empty'] >> text=Start a conversation");

    // ── Streaming indicator ──
    public ILocator StreamingCursor => Page.Locator(".jd-cursor");

    // ── Disabled state detection ──
    public ILocator MessageInputDisabled => Page.Locator(".jd-chat-input input[disabled], .jd-chat-input textarea[disabled], .jd-chat-input .mud-disabled input");

    // ── Chat icon in header ──
    public ILocator ChatHeaderIcon => Page.Locator("[data-testid='chat-header'] .mud-icon-root");

    // ── Send icon (adornment) ──
    public ILocator SendIcon => Page.Locator(".jd-chat-input .mud-input-adornment-end .mud-icon-root, .jd-chat-input svg");

    // ── Snackbar ──
    public ILocator ErrorSnackbar => Page.Locator(".mud-snackbar-error, .mud-snackbar.mud-alert-filled-error");

    /// <summary>Type and send a message.</summary>
    public async Task SendMessageAsync(string text)
    {
        await MessageInput.FillAsync(text);
        await SendButton.ClickAsync();
    }
}
