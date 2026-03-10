using System.Globalization;
using System.Net;
using System.Text;

namespace JD.AI.SpecSite;

public static class DocFxCatalogWriter
{
    public static void Write(SpecSiteOptions options, IReadOnlyList<SpecificationCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalogs);

        if (Directory.Exists(options.DocFxOutputRoot))
            Directory.Delete(options.DocFxOutputRoot, recursive: true);
        Directory.CreateDirectory(options.DocFxOutputRoot);

        var rootIndex = BuildRootIndex(catalogs);
        File.WriteAllText(Path.Combine(options.DocFxOutputRoot, "index.md"), rootIndex);

        foreach (var catalog in catalogs)
        {
            var typeIndexRelative = SpecSitePathHelper.GetTypeDocFxIndexRelativePath(catalog.TypeName);
            var typeIndexAbsolute = Path.Combine(
                options.DocFxOutputRoot,
                typeIndexRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(typeIndexAbsolute)!);
            File.WriteAllText(typeIndexAbsolute, BuildTypeIndex(catalog));

            foreach (var document in catalog.Documents)
            {
                var docRelative = SpecSitePathHelper.GetDocumentDocFxRelativePath(catalog.TypeName, document.Id);
                var docAbsolute = Path.Combine(
                    options.DocFxOutputRoot,
                    docRelative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(docAbsolute)!);
                File.WriteAllText(docAbsolute, BuildDocument(catalog, document));
            }
        }

        File.WriteAllText(
            Path.Combine(options.DocFxOutputRoot, "toc.yml"),
            BuildToc(catalogs));
    }

    private static string BuildRootIndex(IReadOnlyList<SpecificationCatalog> catalogs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# UPSS Specifications");
        builder.AppendLine();
        builder.AppendLine("Generated from repository specification indexes.");
        builder.AppendLine();
        builder.AppendLine("- [UPSS Overview](../upss/index.md)");
        builder.AppendLine("- [UPSS Specification Catalog](../upss/catalog.md)");
        builder.AppendLine();
        builder.AppendLine("Regenerate with:");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine("dotnet run --project src/JD.AI.SpecSite -- --emit-docfx");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("| Spec Type | Count |");
        builder.AppendLine("|---|---:|");

        foreach (var catalog in catalogs)
        {
            var href = SpecSitePathHelper.GetTypeDocFxIndexRelativePath(catalog.TypeName);
            var name = ToDisplayName(catalog.TypeName);
            builder.AppendLine($"| [{name}]({href}) | {catalog.Documents.Count.ToString(CultureInfo.InvariantCulture)} |");
        }

        return builder.ToString();
    }

    private static string BuildTypeIndex(SpecificationCatalog catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {ToDisplayName(catalog.TypeName)} Specifications");
        builder.AppendLine();

        foreach (var document in catalog.Documents)
        {
            var href = $"{MakeSafeFileName(document.Id)}.md";
            builder.AppendLine($"- [{EscapeMarkdown(document.Title)}]({href})");
        }

        return builder.ToString();
    }

    private static string BuildDocument(
        SpecificationCatalog catalog,
        SpecificationDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {EscapeMarkdown(document.Title)}");
        builder.AppendLine();
        builder.AppendLine($"- **Type:** `{catalog.TypeName}`");
        builder.AppendLine($"- **Kind:** `{catalog.IndexKind}`");
        builder.AppendLine($"- **ID:** `{document.Id}`");
        builder.AppendLine($"- **Status:** `{document.Status}`");
        builder.AppendLine($"- **Source:** `{document.SourceRelativePath}`");
        builder.AppendLine();
        builder.AppendLine("## YAML");
        builder.AppendLine();
        builder.AppendLine("```yaml");
        builder.AppendLine(document.SourceYaml.TrimEnd());
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static string BuildToc(IReadOnlyList<SpecificationCatalog> catalogs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("- name: UPSS Specifications");
        builder.AppendLine("  href: index.md");
        builder.AppendLine("  items:");

        foreach (var catalog in catalogs)
        {
            var typeName = ToDisplayName(catalog.TypeName);
            var typeHref = SpecSitePathHelper.GetTypeDocFxIndexRelativePath(catalog.TypeName);
            builder.AppendLine($"  - name: {QuoteYaml(typeName)}");
            builder.AppendLine($"    href: {QuoteYaml(typeHref)}");
            builder.AppendLine("    items:");

            foreach (var document in catalog.Documents)
            {
                var docHref = SpecSitePathHelper.GetDocumentDocFxRelativePath(catalog.TypeName, document.Id);
                builder.AppendLine($"    - name: {QuoteYaml(document.Title)}");
                builder.AppendLine($"      href: {QuoteYaml(docHref)}");
            }
        }

        return builder.ToString();
    }

    private static string ToDisplayName(string value)
    {
        var normalized = value.Replace('-', ' ').Replace('_', ' ').Trim();
        return normalized.ToLowerInvariant() switch
        {
            "adrs" => "ADRs",
            "usecases" => "Use Cases",
            _ => string.Join(
                " ",
                normalized
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant())),
        };
    }

    private static string QuoteYaml(string value)
    {
        var encoded = WebUtility.HtmlDecode(value);
        return $"'{encoded.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static string EscapeMarkdown(string value) =>
        value.Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

    private static string MakeSafeFileName(string value)
    {
        var chars = value.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
                chars[i] = '-';
        }

        return new string(chars);
    }
}
