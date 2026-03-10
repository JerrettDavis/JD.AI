namespace JD.AI.SpecSite;

public static class SpecSitePathHelper
{
    private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();

    public static string GetTypeIndexRelativePath(string typeName) =>
        $"{typeName}/index.html";

    public static string GetDocumentRelativePath(string typeName, string id) =>
        $"{typeName}/{MakeSafeFileName(id)}.html";

    public static string GetTypeDocFxIndexRelativePath(string typeName) =>
        $"{typeName}/index.md";

    public static string GetDocumentDocFxRelativePath(string typeName, string id) =>
        $"{typeName}/{MakeSafeFileName(id)}.md";

    public static string NormalizeWebPath(string path) => path.Replace('\\', '/');

    private static string MakeSafeFileName(string value)
    {
        var sanitized = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            sanitized[i] = InvalidFileNameCharacters.Contains(character)
                ? '-'
                : character;
        }

        return new string(sanitized);
    }
}
