using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// File system tools for the AI agent.
/// </summary>
public sealed class FileTools
{
    [KernelFunction("read_file")]
    [Description("Read the contents of a file. Returns the text content with line numbers.")]
    public static string ReadFile(
        [Description("Absolute or relative file path")] string path,
        [Description("Optional start line (1-based)")] int? startLine = null,
        [Description("Optional end line (1-based, -1 for end of file)")] int? endLine = null)
    {
        if (!File.Exists(path))
        {
            return $"Error: File not found: {path}";
        }

        var lines = File.ReadAllLines(path);
        var start = Math.Max(0, (startLine ?? 1) - 1);
        var end = endLine is null or -1 ? lines.Length : Math.Min(lines.Length, endLine.Value);

        var sb = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            sb.AppendLine($"{i + 1}. {lines[i]}");
        }

        return sb.ToString();
    }

    [KernelFunction("write_file")]
    [Description("Write content to a file, creating it if it doesn't exist.")]
    public static string WriteFile(
        [Description("Absolute or relative file path")] string path,
        [Description("Content to write")] string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
        return $"Wrote {content.Length} characters to {path}";
    }

    [KernelFunction("edit_file")]
    [Description("Replace exactly one occurrence of old_str with new_str in a file.")]
    public static string EditFile(
        [Description("Absolute or relative file path")] string path,
        [Description("The exact string to find and replace")] string oldStr,
        [Description("The replacement string")] string newStr)
    {
        if (!File.Exists(path))
        {
            return $"Error: File not found: {path}";
        }

        var content = File.ReadAllText(path);
        var idx = content.IndexOf(oldStr, StringComparison.Ordinal);
        if (idx < 0)
        {
            return "Error: old_str not found in file.";
        }

        // Ensure uniqueness
        var secondIdx = content.IndexOf(oldStr, idx + oldStr.Length, StringComparison.Ordinal);
        if (secondIdx >= 0)
        {
            return "Error: old_str matches multiple locations. Provide more context to make it unique.";
        }

        var updated = string.Concat(content.AsSpan(0, idx), newStr, content.AsSpan(idx + oldStr.Length));
        File.WriteAllText(path, updated);
        return $"Replaced {oldStr.Length} characters with {newStr.Length} characters in {path}";
    }

    [KernelFunction("list_directory")]
    [Description("List files and directories. Returns a tree-like listing.")]
    public static string ListDirectory(
        [Description("Directory path (defaults to current directory)")] string? path = null,
        [Description("Maximum depth to recurse (default 2)")] int maxDepth = 2)
    {
        var dir = path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            return $"Error: Directory not found: {dir}";
        }

        var sb = new StringBuilder();
        ListDirectoryRecursive(sb, dir, "", 0, maxDepth);
        return sb.ToString();
    }

    private static void ListDirectoryRecursive(
        StringBuilder sb, string dir, string indent, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            return;
        }

        try
        {
            var entries = Directory.GetFileSystemEntries(dir)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);

                // Skip hidden/special
                if (name.StartsWith('.') ||
                    string.Equals(name, "node_modules", StringComparison.Ordinal) ||
                    string.Equals(name, "bin", StringComparison.Ordinal) ||
                    string.Equals(name, "obj", StringComparison.Ordinal))
                {
                    continue;
                }

                var isDir = Directory.Exists(entry);
                sb.AppendLine($"{indent}{(isDir ? $"{name}/" : name)}");

                if (isDir)
                {
                    ListDirectoryRecursive(sb, entry, indent + "  ", depth + 1, maxDepth);
                }
            }
        }
#pragma warning disable CA1031 // catch broad — permission errors
        catch (UnauthorizedAccessException)
        {
            sb.AppendLine($"{indent}[access denied]");
        }
#pragma warning restore CA1031
    }
}
