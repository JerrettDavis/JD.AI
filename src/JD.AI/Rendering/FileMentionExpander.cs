using System.Text.RegularExpressions;

namespace JD.AI.Rendering;

/// <summary>
/// Expands @file/path references in user input by injecting file contents.
/// </summary>
public static partial class FileMentionExpander
{
    [GeneratedRegex(@"@([\w./\\-]+(?:\.\w+)?)")]
    private static partial Regex FileRefPattern();

    public static string Expand(string input)
    {
        return FileRefPattern().Replace(input, match =>
        {
            var path = match.Groups[1].Value;
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                return $"[File: {path}]\n```\n{content}\n```";
            }

            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                var content = File.ReadAllText(fullPath);
                return $"[File: {path}]\n```\n{content}\n```";
            }

            return match.Value;
        });
    }
}
