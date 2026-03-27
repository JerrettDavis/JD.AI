using System.ComponentModel;
using JD.AI.Core.Channels;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Semantic Kernel tool plugin that lets agents add emoji reactions to messages.
/// The agent chooses the emoji — useful for expressing acknowledgment, status, or emotion.
/// </summary>
public sealed class ChannelReactionTools(IChannelRegistry channelRegistry)
{
    /// <summary>
    /// The channel ID of the conversation the agent is currently responding to.
    /// Set by the message handler before each turn.
    /// </summary>
    public string? ActiveChannelType { get; set; }

    /// <summary>
    /// The conversation/channel ID of the current message context.
    /// </summary>
    public string? ActiveConversationId { get; set; }

    /// <summary>
    /// The message ID the agent is currently responding to.
    /// </summary>
    public string? ActiveMessageId { get; set; }

    [KernelFunction("react")]
    [Description("Add an emoji reaction to the current message. Use this to express acknowledgment, status, mood, or any contextual response. Choose an emoji that fits the situation — you have full creative freedom.")]
    public async Task<string> ReactAsync(
        [Description("The emoji to react with (e.g., 👍, 🎉, 🔥, 💡, ❤️, 🚀, etc.)")] string emoji,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveChannelType) ||
            string.IsNullOrWhiteSpace(ActiveConversationId) ||
            string.IsNullOrWhiteSpace(ActiveMessageId))
        {
            return "No active message context — cannot add reaction.";
        }

        var channel = channelRegistry.GetChannel(ActiveChannelType);
        if (channel is null)
            return $"Channel '{ActiveChannelType}' not found.";

        await channel.ReactAsync(ActiveConversationId, ActiveMessageId, emoji, ct);
        return $"Reacted with {emoji}";
    }

    [KernelFunction("react_to_message")]
    [Description("Add an emoji reaction to a specific message by ID. Use when you want to react to a message other than the one you're currently responding to.")]
    public async Task<string> ReactToMessageAsync(
        [Description("The conversation/channel ID")] string conversationId,
        [Description("The message ID to react to")] string messageId,
        [Description("The emoji to react with")] string emoji,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveChannelType))
            return "No active channel context.";

        var channel = channelRegistry.GetChannel(ActiveChannelType);
        if (channel is null)
            return $"Channel '{ActiveChannelType}' not found.";

        await channel.ReactAsync(conversationId, messageId, emoji, ct);
        return $"Reacted with {emoji} on message {messageId}";
    }
}
