namespace JD.AI.Connectors.Sdk;

/// <summary>
/// Metadata describing a registered connector.
/// </summary>
public sealed class ConnectorDescriptor
{
    /// <summary>Machine-readable connector name.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Connector version.</summary>
    public required string Version { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>The connector instance.</summary>
    public required IConnector Connector { get; init; }

    /// <summary>Whether this connector is currently enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Plugin types registered by this connector.</summary>
    public IReadOnlyList<Type> ToolPluginTypes { get; init; } = [];

    /// <summary>Named loadouts declared by this connector.</summary>
    public IReadOnlyDictionary<string, Func<string, bool>> Loadouts { get; init; } =
        new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase);
}
