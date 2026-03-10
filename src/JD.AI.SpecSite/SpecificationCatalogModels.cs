namespace JD.AI.SpecSite;

public sealed record SpecificationCatalog(
    string TypeName,
    string IndexKind,
    IReadOnlyList<SpecificationDocument> Documents);

public sealed record SpecificationDocument(
    string Id,
    string Title,
    string Status,
    string SourceRelativePath,
    string SourceAbsolutePath,
    string SourceYaml,
    YamlMapNode RootNode);

public abstract record YamlNode;

public sealed record YamlMapNode(IReadOnlyList<YamlMapEntry> Properties) : YamlNode;

public sealed record YamlMapEntry(string Key, YamlNode Value);

public sealed record YamlSequenceNode(IReadOnlyList<YamlNode> Items) : YamlNode;

public sealed record YamlScalarNode(object? Value) : YamlNode;
