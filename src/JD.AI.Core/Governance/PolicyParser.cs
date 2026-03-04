using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Parses YAML policy documents into <see cref="PolicyDocument"/> instances.
/// </summary>
public static class PolicyParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Parses a YAML string into a <see cref="PolicyDocument"/>.</summary>
    /// <param name="yaml">YAML content to parse.</param>
    /// <returns>The parsed <see cref="PolicyDocument"/>.</returns>
    /// <exception cref="YamlDotNet.Core.YamlException">Thrown when the YAML is invalid.</exception>
    public static PolicyDocument Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<PolicyDocument>(yaml);
    }

    /// <summary>Reads and parses a YAML file into a <see cref="PolicyDocument"/>.</summary>
    /// <param name="path">Absolute path to the YAML file.</param>
    /// <returns>The parsed <see cref="PolicyDocument"/>.</returns>
    public static PolicyDocument ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var yaml = File.ReadAllText(path);
        return Parse(yaml);
    }

    /// <summary>
    /// Parses all <c>.yaml</c> and <c>.yml</c> files in a directory into
    /// <see cref="PolicyDocument"/> instances.  Files that fail to parse are skipped
    /// silently.
    /// </summary>
    /// <param name="dirPath">Directory to search for policy files.</param>
    /// <returns>Enumerable of successfully parsed documents.</returns>
    public static IEnumerable<PolicyDocument> ParseDirectory(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        if (!Directory.Exists(dirPath))
            yield break;

        var files = Directory.EnumerateFiles(dirPath, "*.yaml")
            .Concat(Directory.EnumerateFiles(dirPath, "*.yml"));

        foreach (var file in files)
        {
            PolicyDocument? doc = null;
            try
            {
                doc = ParseFile(file);
            }
            catch (Exception)
            {
                // Skip files that fail to parse
            }

            if (doc is not null)
                yield return doc;
        }
    }
}
