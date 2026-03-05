using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Channels;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Operational tools for messaging channels — list, status, send, and read.
/// Requires an <see cref="IChannelRegistry"/> for channel access.
/// </summary>
[ToolPlugin("channels", RequiresInjection = true)]
public sealed class ChannelOpsTools
{
    private readonly IChannelRegistry _registry;

    public ChannelOpsTools(IChannelRegistry registry)
    {
        _registry = registry;
    }

    [KernelFunction("channel_list")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all registered messaging channels and their connection status.")]
    public string ListChannels()
    {
        var channels = _registry.Channels;
        if (channels.Count == 0)
            return "No channels registered. Configure channels in gateway settings.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Channels ({channels.Count})");
        sb.AppendLine();
        sb.AppendLine("| Type | Name | Status |");
        sb.AppendLine("|------|------|--------|");

        foreach (var ch in channels)
        {
            var status = ch.IsConnected ? "✓ Connected" : "✗ Disconnected";
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {ch.ChannelType} | {ch.DisplayName} | {status} |");
        }

        return sb.ToString();
    }

    [KernelFunction("channel_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get detailed status of a specific messaging channel.")]
    public string GetChannelStatus(
        [Description("Channel type identifier (e.g. 'discord', 'slack', 'signal', 'web')")] string channelType)
    {
        var channel = _registry.GetChannel(channelType);
        if (channel is null)
            return $"Error: Channel '{channelType}' not found. Use channel_list to see available channels.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Channel: {channel.DisplayName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Type**: {channel.ChannelType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Connected**: {(channel.IsConnected ? "Yes" : "No")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Implementation**: {channel.GetType().Name}");

        return sb.ToString();
    }

    [KernelFunction("channel_send")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Send a message through a specific channel to a conversation or thread.")]
    public async Task<string> SendMessageAsync(
        [Description("Channel type (e.g. 'discord', 'slack', 'signal', 'web')")] string channelType,
        [Description("Conversation or thread ID to send to")] string conversationId,
        [Description("Message content to send")] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: Message content cannot be empty.";

        var channel = _registry.GetChannel(channelType);
        if (channel is null)
            return $"Error: Channel '{channelType}' not found.";

        if (!channel.IsConnected)
            return $"Error: Channel '{channelType}' is not connected. Connect it first.";

        try
        {
            await channel.SendMessageAsync(conversationId, message).ConfigureAwait(false);
            return $"Message sent to {channelType}:{conversationId} ({message.Length} chars).";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error sending message: {ex.Message}";
        }
    }

    [KernelFunction("channel_connect")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Connect a disconnected channel adapter.")]
    public async Task<string> ConnectChannelAsync(
        [Description("Channel type to connect (e.g. 'discord', 'slack')")] string channelType)
    {
        var channel = _registry.GetChannel(channelType);
        if (channel is null)
            return $"Error: Channel '{channelType}' not found.";

        if (channel.IsConnected)
            return $"Channel '{channelType}' is already connected.";

        try
        {
            await channel.ConnectAsync().ConfigureAwait(false);
            return $"Channel '{channelType}' connected successfully.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error connecting channel: {ex.Message}";
        }
    }

    [KernelFunction("channel_disconnect")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Disconnect a channel adapter gracefully.")]
    public async Task<string> DisconnectChannelAsync(
        [Description("Channel type to disconnect")] string channelType)
    {
        var channel = _registry.GetChannel(channelType);
        if (channel is null)
            return $"Error: Channel '{channelType}' not found.";

        if (!channel.IsConnected)
            return $"Channel '{channelType}' is already disconnected.";

        try
        {
            await channel.DisconnectAsync().ConfigureAwait(false);
            return $"Channel '{channelType}' disconnected.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error disconnecting channel: {ex.Message}";
        }
    }
}
