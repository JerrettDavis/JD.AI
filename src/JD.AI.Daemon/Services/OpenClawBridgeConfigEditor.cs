using System.Text.Json;
using System.Text.Json.Nodes;
using JD.AI.Gateway.Config;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Reads and updates OpenClaw bridge settings in gateway appsettings.json.
/// </summary>
public static class OpenClawBridgeConfigEditor
{
    private const string GatewayKey = "Gateway";
    private const string OpenClawKey = "OpenClaw";
    private const string EnabledKey = "Enabled";
    private const string AutoConnectKey = "AutoConnect";
    private const string DefaultModeKey = "DefaultMode";
    private const string ChannelsKey = "Channels";
    private const string ModeKey = "Mode";
    private const string PassthroughMode = "Passthrough";

    public static OpenClawBridgeState ReadState(string appSettingsPath)
    {
        var root = ReadRoot(appSettingsPath);
        var openClawNode = GetOrCreateOpenClawNode(root);

        var enabled = openClawNode[EnabledKey]?.GetValue<bool?>() ?? false;
        var autoConnect = openClawNode[AutoConnectKey]?.GetValue<bool?>() ?? true;
        var defaultMode = openClawNode[DefaultModeKey]?.GetValue<string>() ?? PassthroughMode;
        var (overrideActive, overrideChannels) = AnalyzeOverrides(openClawNode, defaultMode);

        return new OpenClawBridgeState(
            Enabled: enabled,
            AutoConnect: autoConnect,
            DefaultMode: defaultMode,
            OverrideActive: overrideActive,
            OverrideChannels: overrideChannels);
    }

    public static OpenClawBridgeState SetEnabled(string appSettingsPath, bool enabled)
    {
        var root = ReadRoot(appSettingsPath);
        var openClawNode = GetOrCreateOpenClawNode(root);

        openClawNode[EnabledKey] = enabled;
        openClawNode[AutoConnectKey] = enabled;

        WriteRoot(appSettingsPath, root);
        return ReadState(appSettingsPath);
    }

    public static OpenClawBridgeState SetPassthrough(string appSettingsPath)
    {
        var root = ReadRoot(appSettingsPath);
        var openClawNode = GetOrCreateOpenClawNode(root);

        openClawNode[EnabledKey] = true;
        openClawNode[AutoConnectKey] = true;
        openClawNode[DefaultModeKey] = PassthroughMode;

        if (openClawNode[ChannelsKey] is JsonObject channels)
        {
            foreach (var (_, value) in channels)
            {
                if (value is JsonObject channelObj)
                    channelObj[ModeKey] = PassthroughMode;
            }
        }

        WriteRoot(appSettingsPath, root);
        return ReadState(appSettingsPath);
    }

    internal static (bool OverrideActive, string[] OverrideChannels) AnalyzeOverrides(
        JsonObject openClawNode,
        string defaultMode)
    {
        var overrideChannels = new List<string>();
        var overrideActive = !IsPassthrough(defaultMode);

        if (openClawNode[ChannelsKey] is JsonObject channels)
        {
            foreach (var (channelName, channelNode) in channels)
            {
                if (channelNode is not JsonObject channelObj)
                    continue;

                var mode = channelObj[ModeKey]?.GetValue<string>() ?? defaultMode;
                if (IsPassthrough(mode))
                    continue;

                overrideActive = true;
                overrideChannels.Add(channelName);
            }
        }

        return (overrideActive, [.. overrideChannels.Order(StringComparer.OrdinalIgnoreCase)]);
    }

    private static bool IsPassthrough(string? mode) =>
        string.Equals(mode, PassthroughMode, StringComparison.OrdinalIgnoreCase);

    private static JsonObject ReadRoot(string appSettingsPath)
    {
        if (!File.Exists(appSettingsPath))
            throw new FileNotFoundException($"appsettings.json not found: {appSettingsPath}");

        var json = File.ReadAllText(appSettingsPath);
        if (JsonNode.Parse(json) is not JsonObject root)
            throw new InvalidOperationException($"Invalid JSON in {appSettingsPath}");

        return root;
    }

    private static JsonObject GetOrCreateOpenClawNode(JsonObject root)
    {
        if (root[GatewayKey] is not JsonObject gatewayObj)
        {
            gatewayObj = new JsonObject();
            root[GatewayKey] = gatewayObj;
        }

        if (gatewayObj[OpenClawKey] is not JsonObject openClawObj)
        {
            openClawObj = new JsonObject();
            gatewayObj[OpenClawKey] = openClawObj;
        }

        return openClawObj;
    }

    private static void WriteRoot(string appSettingsPath, JsonObject root)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        File.WriteAllText(appSettingsPath, root.ToJsonString(options));
    }
}

public sealed record OpenClawBridgeState(
    bool Enabled,
    bool AutoConnect,
    string DefaultMode,
    bool OverrideActive,
    IReadOnlyList<string> OverrideChannels);
