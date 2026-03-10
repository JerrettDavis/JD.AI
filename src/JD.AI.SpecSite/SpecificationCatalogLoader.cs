using System.Collections;
using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.SpecSite;

public static class SpecificationCatalogLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();
    private static readonly IDeserializer CamelCaseDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static IReadOnlyList<SpecificationCatalog> Load(SpecSiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.SpecsRoot))
            throw new DirectoryNotFoundException($"Specs root not found: {options.SpecsRoot}");

        var catalogs = new List<SpecificationCatalog>();
        var specTypeDirectories = Directory
            .GetDirectories(options.SpecsRoot)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

        foreach (var specTypeDirectory in specTypeDirectories)
        {
            var indexPath = Path.Combine(specTypeDirectory, "index.yaml");
            if (!File.Exists(indexPath))
                continue;

            var specType = Path.GetFileName(specTypeDirectory);
            var indexDocument = ParseIndex(indexPath);
            var documents = new List<SpecificationDocument>();

            foreach (var entry in indexDocument.Entries.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(entry.Path))
                    throw new InvalidDataException(
                        $"Spec index entry '{entry.Id}' in '{indexPath}' is missing a path.");

                var sourceRelativePath = NormalizeRepoRelativePath(entry.Path);
                var sourceAbsolutePath = Path.GetFullPath(
                    Path.Combine(options.RepoRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(sourceAbsolutePath))
                    throw new FileNotFoundException(
                        $"Spec file '{sourceRelativePath}' referenced by '{indexPath}' was not found.",
                        sourceAbsolutePath);

                var yaml = File.ReadAllText(sourceAbsolutePath);
                var rootNode = ParseRootNode(yaml, sourceRelativePath);
                var title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Id : entry.Title;

                documents.Add(new SpecificationDocument(
                    entry.Id,
                    title,
                    entry.Status,
                    sourceRelativePath,
                    sourceAbsolutePath,
                    yaml,
                    rootNode));
            }

            catalogs.Add(new SpecificationCatalog(specType, indexDocument.Kind, documents));
        }

        return catalogs;
    }

    private static string NormalizeRepoRelativePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        return normalized.TrimStart('/');
    }

    private static YamlMapNode ParseRootNode(string yaml, string sourceRelativePath)
    {
        var raw = Deserializer.Deserialize<object>(yaml);
        var converted = ConvertNode(raw);
        if (converted is YamlMapNode map)
            return map;

        throw new InvalidDataException(
            $"Spec '{sourceRelativePath}' root YAML value must be a mapping.");
    }

    private static YamlNode ConvertNode(object? raw)
    {
        return raw switch
        {
            IDictionary<object, object> map => new YamlMapNode(
                map.Select(pair => new YamlMapEntry(
                        Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty,
                        ConvertNode(pair.Value)))
                    .ToList()),
            IList list => new YamlSequenceNode(list.Cast<object?>().Select(ConvertNode).ToList()),
            _ => new YamlScalarNode(raw),
        };
    }

    private static SpecificationIndexDocument ParseIndex(string indexPath)
    {
        var yaml = File.ReadAllText(indexPath);
        var index = CamelCaseDeserializer.Deserialize<SpecificationIndexDocument>(yaml);
        if (index is null)
            throw new InvalidDataException($"Unable to parse specification index '{indexPath}'.");

        return index;
    }

    private sealed class SpecificationIndexDocument
    {
        public string ApiVersion { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public List<SpecificationIndexEntry> Entries { get; set; } = [];
    }

    private sealed class SpecificationIndexEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
