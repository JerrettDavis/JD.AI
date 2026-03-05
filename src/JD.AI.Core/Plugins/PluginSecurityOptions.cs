namespace JD.AI.Core.Plugins;

/// <summary>
/// Security policy options for plugin installation and runtime loading.
/// </summary>
public sealed class PluginSecurityOptions
{
    public const string TrustedPublishersEnvironmentVariable = "JDAI_PLUGIN_TRUSTED_PUBLISHERS";

    public IReadOnlySet<string> TrustedPublishers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool EnforceTrustedPublishers => TrustedPublishers.Count > 0;

    public static PluginSecurityOptions FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(TrustedPublishersEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PluginSecurityOptions();
        }

        var publishers = raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new PluginSecurityOptions
        {
            TrustedPublishers = publishers,
        };
    }
}
