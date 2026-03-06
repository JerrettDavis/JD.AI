namespace JD.AI.Connectors.Sdk;

/// <summary>
/// Marks a class as a JD.AI connector. Connectors are discovered at startup via
/// assembly scanning and registered in the <c>ConnectorRegistry</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JdAiConnectorAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the connector's unique machine name.
    /// </summary>
    /// <param name="name">Unique machine-readable name (e.g. "jira", "confluence"). Lowercase, no spaces.</param>
    /// <param name="displayName">Human-readable display name.</param>
    /// <param name="version">Connector version string (e.g. "1.0.0").</param>
    public JdAiConnectorAttribute(string name, string displayName, string version = "1.0.0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Name = name;
        DisplayName = displayName;
        Version = version;
    }

    /// <summary>Unique machine-readable connector name.</summary>
    public string Name { get; }

    /// <summary>Human-readable connector name.</summary>
    public string DisplayName { get; }

    /// <summary>Connector version string.</summary>
    public string Version { get; }

    /// <summary>Optional description shown in connector listings.</summary>
    public string? Description { get; init; }

    /// <summary>Optional URL to documentation or the connector homepage.</summary>
    public string? Homepage { get; init; }
}
