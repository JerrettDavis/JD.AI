using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Tools;

/// <summary>
/// Serializes and deserializes <see cref="ToolLoadout"/> instances to and from YAML.
/// </summary>
/// <remarks>
/// YAML files use camelCase keys that map to the corresponding C# properties.
/// Example schema:
/// <code>
/// name: my-loadout
/// parent: developer
/// includeCategories:
///   - Git
///   - Search
/// includePlugins:
///   - myPlugin
/// excludePlugins: []
/// discoverablePatterns:
///   - docker*
/// </code>
/// </remarks>
public static class ToolLoadoutYamlSerializer
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Serializes a <see cref="ToolLoadout"/> to a YAML string.</summary>
    public static string Serialize(ToolLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);

        var dto = new LoadoutYamlDto
        {
            Name = loadout.Name,
            Parent = loadout.ParentLoadoutName,
            IncludeCategories = loadout.IncludedCategories.Count > 0
                ? [.. loadout.IncludedCategories.Select(c => c.ToString())]
                : null,
            IncludePlugins = loadout.DefaultPlugins.Count > 0
                ? [.. loadout.DefaultPlugins]
                : null,
            ExcludePlugins = loadout.DisabledPlugins.Count > 0
                ? [.. loadout.DisabledPlugins]
                : null,
            DiscoverablePatterns = loadout.DiscoverablePatterns.Count > 0
                ? [.. loadout.DiscoverablePatterns]
                : null,
        };

        return _serializer.Serialize(dto);
    }

    /// <summary>Deserializes a YAML string into a <see cref="ToolLoadout"/>.</summary>
    public static ToolLoadout Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var dto = _deserializer.Deserialize<LoadoutYamlDto>(yaml);
        return MapToLoadout(dto);
    }

    /// <summary>Deserializes a <see cref="ToolLoadout"/> from a <c>.loadout.yaml</c> file.</summary>
    public static ToolLoadout DeserializeFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var yaml = File.ReadAllText(filePath);
        return Deserialize(yaml);
    }

    private static ToolLoadout MapToLoadout(LoadoutYamlDto dto)
    {
        var categories = new HashSet<ToolCategory>();
        if (dto.IncludeCategories is not null)
        {
            foreach (var cat in dto.IncludeCategories)
            {
                if (Enum.TryParse<ToolCategory>(cat, ignoreCase: true, out var parsed))
                    categories.Add(parsed);
            }
        }

        return new ToolLoadout(dto.Name)
        {
            ParentLoadoutName = dto.Parent,
            IncludedCategories = categories,
            DefaultPlugins = new HashSet<string>(
                dto.IncludePlugins ?? [], StringComparer.OrdinalIgnoreCase),
            DisabledPlugins = new HashSet<string>(
                dto.ExcludePlugins ?? [], StringComparer.OrdinalIgnoreCase),
            DiscoverablePatterns = (dto.DiscoverablePatterns ?? []).AsReadOnly(),
        };
    }
}

/// <summary>Data-transfer object for YAML round-trip of a loadout.</summary>
internal sealed class LoadoutYamlDto
{
    public string Name { get; set; } = string.Empty;
    public string? Parent { get; set; }
    public List<string>? IncludeCategories { get; set; }
    public List<string>? IncludePlugins { get; set; }
    public List<string>? ExcludePlugins { get; set; }
    public List<string>? DiscoverablePatterns { get; set; }
}
