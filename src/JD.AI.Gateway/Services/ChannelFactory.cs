using JD.AI.Channels.Discord;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.Signal;
using JD.AI.Channels.Slack;
using JD.AI.Channels.Telegram;
using JD.AI.Channels.Web;
using JD.AI.Core.Channels;
using JD.AI.Gateway.Config;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Creates <see cref="IChannel"/> instances from <see cref="ChannelConfig"/> definitions.
/// Supports all built-in channel types: discord, signal, slack, telegram, web, openclaw.
/// </summary>
public sealed class ChannelFactory
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ChannelFactory> _logger;

    public ChannelFactory(IServiceProvider sp, ILogger<ChannelFactory> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    /// <summary>Creates a channel instance from configuration. Returns null if the type is unknown.</summary>
    public IChannel? Create(ChannelConfig config)
    {
        var type = config.Type.ToLowerInvariant();
        try
        {
            return type switch
            {
                "discord" => CreateDiscord(config),
                "signal" => CreateSignal(config),
                "slack" => CreateSlack(config),
                "telegram" => CreateTelegram(config),
                "web" => new WebChannel(),
                "openclaw" => _sp.GetService<OpenClawBridgeChannel>(),
                _ => LogUnknown(type)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create channel '{Type}'", type);
            return null;
        }
    }

    private DiscordChannel CreateDiscord(ChannelConfig config)
    {
        var token = ResolveSetting(config, "BotToken")
            ?? throw new InvalidOperationException("Discord channel requires 'BotToken' setting.");
        return new DiscordChannel(token);
    }

    private SignalChannel CreateSignal(ChannelConfig config)
    {
        var account = ResolveSetting(config, "Account")
            ?? throw new InvalidOperationException("Signal channel requires 'Account' setting.");
        var cliPath = ResolveSetting(config, "SignalCliPath");
        return new SignalChannel(account, cliPath);
    }

    private SlackChannel CreateSlack(ChannelConfig config)
    {
        var botToken = ResolveSetting(config, "BotToken")
            ?? throw new InvalidOperationException("Slack channel requires 'BotToken' setting.");
        var appToken = ResolveSetting(config, "AppToken")
            ?? throw new InvalidOperationException("Slack channel requires 'AppToken' setting.");
        return new SlackChannel(botToken, appToken);
    }

    private TelegramChannel CreateTelegram(ChannelConfig config)
    {
        var token = ResolveSetting(config, "BotToken")
            ?? throw new InvalidOperationException("Telegram channel requires 'BotToken' setting.");
        return new TelegramChannel(token);
    }

    private IChannel? LogUnknown(string type)
    {
        _logger.LogWarning("Unknown channel type '{Type}' — skipping", type);
        return null;
    }

    /// <summary>
    /// Resolves a setting value, supporting environment variable references via 'env:VAR_NAME' syntax.
    /// </summary>
    private static string? ResolveSetting(ChannelConfig config, string key)
    {
        if (!config.Settings.TryGetValue(key, out var value))
            return null;

        if (value.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var envVar = value[4..];
            return Environment.GetEnvironmentVariable(envVar);
        }

        return value;
    }
}
