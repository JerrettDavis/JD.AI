using System.ComponentModel;
using JD.AI.Core.Attributes;
using JD.AI.Core.Channels;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Rich Discord interaction tools — polls, embeds, threads.
/// Agent-callable Semantic Kernel functions for Discord-specific features.
/// </summary>
[ToolPlugin("discord", RequiresInjection = true)]
public sealed class DiscordRichTools
{
    private readonly IChannelRegistry _channelRegistry;

    /// <summary>Active conversation context — set before each turn.</summary>
    public string? ActiveConversationId { get; set; }

    public DiscordRichTools(IChannelRegistry channelRegistry)
    {
        _channelRegistry = channelRegistry;
    }

    [KernelFunction("create_poll")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description(
        "Create a Discord poll in the current channel. Polls let users vote on options. " +
        "Provide a question and 2-10 answer options. Duration is in hours (1-168, default 24).")]
    public async Task<string> CreatePollAsync(
        [Description("The poll question")] string question,
        [Description("Comma-separated answer options (2-10)")] string options,
        [Description("Poll duration in hours (1-168, default 24)")] int durationHours = 24,
        [Description("Allow multiple answers (default false)")] bool multiSelect = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveConversationId))
            return "No active conversation — cannot create poll.";

        var channel = GetDiscordChannel();
        if (channel is null)
            return "Discord channel not available.";

        var optionList = options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (optionList.Length < 2 || optionList.Length > 10)
            return "Polls require 2-10 options.";

        durationHours = Math.Clamp(durationHours, 1, 168);

        var metadata = new Dictionary<string, string>
        {
            ["discord.poll.question"] = question,
            ["discord.poll.duration_hours"] = durationHours.ToString(),
            ["discord.poll.multi_select"] = multiSelect.ToString(),
        };
        for (var i = 0; i < optionList.Length; i++)
            metadata[$"discord.poll.option.{i}"] = optionList[i];

        // Use channel's SendMessageAsync with poll metadata
        // The Discord channel implementation reads these metadata fields
        await channel.SendMessageAsync(
            ActiveConversationId,
            $"__POLL__:{question}|{string.Join("|", optionList)}|{durationHours}|{multiSelect}",
            ct);

        return $"Poll created: \"{question}\" with {optionList.Length} options, {durationHours}h duration.";
    }

    [KernelFunction("send_embed")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Send a rich embed message to Discord. Embeds have a title, description, color, " +
        "and optional fields. Use for structured information display.")]
    public async Task<string> SendEmbedAsync(
        [Description("Embed title")] string title,
        [Description("Embed description/body text")] string description,
        [Description("Color as hex (e.g., '#3b82f6') or name (e.g., 'blue')")] string color = "#3b82f6",
        [Description("Optional fields as 'name:value' pairs separated by semicolons")] string? fields = null,
        [Description("Optional footer text")] string? footer = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveConversationId))
            return "No active conversation.";

        var channel = GetDiscordChannel();
        if (channel is null)
            return "Discord channel not available.";

        // Encode embed as a special message format the Discord channel parses
        var embedData = $"__EMBED__:{title}|{description}|{color}";
        if (!string.IsNullOrWhiteSpace(fields))
            embedData += $"|FIELDS:{fields}";
        if (!string.IsNullOrWhiteSpace(footer))
            embedData += $"|FOOTER:{footer}";

        await channel.SendMessageAsync(ActiveConversationId, embedData, ct);
        return $"Embed sent: \"{title}\"";
    }

    [KernelFunction("create_thread")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description(
        "Create a new Discord thread in the current channel. Threads are useful for " +
        "organizing conversations around specific topics.")]
    public async Task<string> CreateThreadAsync(
        [Description("Thread name/title")] string name,
        [Description("Initial message in the thread")] string message,
        [Description("Auto-archive duration in minutes (60, 1440, 4320, or 10080)")] int archiveAfterMinutes = 1440,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveConversationId))
            return "No active conversation.";

        var channel = GetDiscordChannel();
        if (channel is null)
            return "Discord channel not available.";

        await channel.SendMessageAsync(
            ActiveConversationId,
            $"__THREAD__:{name}|{message}|{archiveAfterMinutes}",
            ct);

        return $"Thread created: \"{name}\"";
    }

    [KernelFunction("send_file")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Send a file to the current Discord channel. Provide the file path on the local system. " +
        "Useful for sharing logs, screenshots, code files, or generated content.")]
    public async Task<string> SendFileAsync(
        [Description("Absolute path to the file to send")] string filePath,
        [Description("Optional message text to accompany the file")] string? message = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveConversationId))
            return "No active conversation.";

        var channel = GetDiscordChannel();
        if (channel is null)
            return "Discord channel not available.";

        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > 25 * 1024 * 1024) // 25MB Discord limit
            return $"File too large ({fileInfo.Length / 1024 / 1024}MB). Discord limit is 25MB.";

        var stream = File.OpenRead(filePath);
        var attachment = new OutboundAttachment(fileInfo.Name, stream);
        await channel.SendMessageWithAttachmentsAsync(
            ActiveConversationId, message, [attachment], ct);

        return $"File sent: {fileInfo.Name} ({fileInfo.Length / 1024}KB)";
    }

    private IChannel? GetDiscordChannel()
        => _channelRegistry.GetChannel("discord");
}
