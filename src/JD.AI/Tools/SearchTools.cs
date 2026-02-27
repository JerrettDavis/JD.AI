using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Code search tools — grep and glob — for the AI agent.
/// </summary>
public sealed class SearchTools
{
    [KernelFunction("grep")]
    [Description("Search file contents for a regex pattern. Returns matching lines with file paths and line numbers.")]
    public static string Grep(
        [Description("Regex pattern to search for")] string pattern,
        [Description("Directory to search in (defaults to cwd)")] string? path = null,
        [Description("Glob pattern to filter files (e.g. *.cs)")] string? glob = null,
        [Description("Lines of context around matches")] int context = 0,
        [Description("Case insensitive search")] bool ignoreCase = false,
        [Description("Maximum results to return")] int maxResults = 50)
    {
        var dir = path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            return $"Error: Directory not found: {dir}";
        }

        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        Regex regex;
        try
        {
            regex = new Regex(pattern, options, TimeSpan.FromSeconds(5));
        }
        catch (RegexParseException ex)
        {
            return $"Error: Invalid regex: {ex.Message}";
        }

        var searchPattern = glob ?? "*.*";
        var files = Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories);

        var sb = new StringBuilder();
        var count = 0;

        foreach (var file in files)
        {
            if (count >= maxResults)
            {
                break;
            }

            // Skip binary/hidden
            var name = Path.GetFileName(file);
            if (name.StartsWith('.') || IsBinaryExtension(Path.GetExtension(file)))
            {
                continue;
            }

            try
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length && count < maxResults; i++)
                {
                    if (!regex.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    var relPath = Path.GetRelativePath(dir, file);
                    var start = Math.Max(0, i - context);
                    var end = Math.Min(lines.Length, i + context + 1);

                    for (var j = start; j < end; j++)
                    {
                        var marker = j == i ? ">" : " ";
                        sb.AppendLine($"{relPath}:{j + 1}{marker} {lines[j]}");
                    }

                    if (context > 0)
                    {
                        sb.AppendLine("---");
                    }

                    count++;
                }
            }
#pragma warning disable CA1031
            catch (Exception) { /* skip unreadable files */ }
#pragma warning restore CA1031
        }

        return count == 0
            ? "No matches found."
            : $"{count} match(es):\n{sb}";
    }

    [KernelFunction("glob")]
    [Description("Find files matching a glob pattern. Returns file paths.")]
    public static string Glob(
        [Description("Glob pattern (e.g. **/*.cs, src/**/*.json)")] string pattern,
        [Description("Directory to search in (defaults to cwd)")] string? path = null)
    {
        var dir = path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            return $"Error: Directory not found: {dir}";
        }

        // Simple glob implementation — convert ** and * to search patterns
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f))
            .Where(f => MatchGlob(f, pattern))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        return files.Count == 0
            ? "No files found."
            : string.Join('\n', files);
    }

    private static bool MatchGlob(string path, string pattern)
    {
        // Convert glob to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/\\\\]*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(
            path.Replace('\\', '/'),
            regexPattern.Replace('\\', '/'),
            RegexOptions.IgnoreCase);
    }

    private static bool IsBinaryExtension(string ext) =>
        ext is ".dll" or ".exe" or ".pdb" or ".png" or ".jpg" or ".gif"
            or ".ico" or ".zip" or ".gz" or ".tar" or ".bin" or ".obj"
            or ".nupkg" or ".snk";
}
