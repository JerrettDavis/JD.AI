using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Loads user-to-role and user-to-group mappings from a YAML file.
/// </summary>
/// <remarks>
/// The YAML schema is:
/// <code>
/// users:
///   alice:
///     role: admin
///     groups:
///       - engineering
///       - security
///   bob:
///     role: developer
///     groups:
///       - engineering
/// </code>
/// </remarks>
public sealed class FileRoleResolver : IRoleResolver
{
    private readonly Dictionary<string, UserRoleEntry> _entries;

    public FileRoleResolver(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            _entries = [];
            return;
        }

        var yaml = File.ReadAllText(filePath);
        _entries = ParseYaml(yaml);
    }

    /// <inheritdoc />
    public string? ResolveRole(string? userId)
    {
        if (userId is null) return null;
        return _entries.TryGetValue(userId, out var entry) ? entry.Role : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveGroups(string? userId)
    {
        if (userId is null) return [];
        return _entries.TryGetValue(userId, out var entry) ? entry.Groups : [];
    }

    private static Dictionary<string, UserRoleEntry> ParseYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var doc = deserializer.Deserialize<RoleDocument>(yaml);
        return doc?.Users ?? [];
    }

    private sealed class RoleDocument
    {
        public Dictionary<string, UserRoleEntry> Users { get; set; } = [];
    }
}

/// <summary>
/// Represents role and group membership for a single user.
/// </summary>
public sealed class UserRoleEntry
{
    public string? Role { get; set; }
    public IReadOnlyList<string> Groups { get; set; } = [];
}
