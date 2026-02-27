using System.Collections.ObjectModel;

namespace JD.AI.Tui.Agent;

/// <summary>
/// Discovers and merges project instruction files (JDAI.md, CLAUDE.md, AGENTS.md, etc.)
/// from the working directory up to the git root, and injects them into the system prompt.
/// </summary>
public static class InstructionsLoader
{
    /// <summary>File names to search, in priority order.</summary>
    private static readonly string[] InstructionFileNames =
    [
        "JDAI.md",
        "CLAUDE.md",
        "AGENTS.md",
        ".github/copilot-instructions.md",
        ".jdai/instructions.md",
    ];

    /// <summary>
    /// Scans from <paramref name="startDir"/> up to the git root for instruction files.
    /// Returns merged content from all found files, with JDAI.md content first.
    /// </summary>
    public static InstructionsResult Load(string? startDir = null)
    {
        startDir ??= Directory.GetCurrentDirectory();
        var result = new InstructionsResult();

        var directories = GetDirectoryChain(startDir);

        foreach (var fileName in InstructionFileNames)
        {
            foreach (var dir in directories)
            {
                var path = Path.Combine(dir, fileName);
                if (!File.Exists(path)) continue;

                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content)) continue;

                // Process include directives
                content = ProcessIncludes(content, Path.GetDirectoryName(path)!);

                result.Add(new InstructionFile(fileName, path, content));
                break; // Only use the first match per file name (closest to CWD wins)
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the directory chain from startDir up to the git root (or filesystem root).
    /// </summary>
    internal static IReadOnlyList<string> GetDirectoryChain(string startDir)
    {
        var dirs = new List<string>();
        var current = Path.GetFullPath(startDir);

        while (!string.IsNullOrEmpty(current))
        {
            dirs.Add(current);

            // Stop at git root
            if (Directory.Exists(Path.Combine(current, ".git")))
                break;

            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.Ordinal))
                break;

            current = parent!;
        }

        return dirs;
    }

    /// <summary>
    /// Processes `include: path/to/file.md` directives, replacing them with file content.
    /// </summary>
    internal static string ProcessIncludes(string content, string baseDir)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("include:", StringComparison.OrdinalIgnoreCase))
            {
                var includePath = trimmed["include:".Length..].Trim();
                var fullPath = Path.IsPathRooted(includePath)
                    ? includePath
                    : Path.Combine(baseDir, includePath);

                if (File.Exists(fullPath))
                {
                    result.Add(File.ReadAllText(fullPath));
                }
                else
                {
                    result.Add($"<!-- include not found: {includePath} -->");
                }
            }
            else
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }
}

/// <summary>A single discovered instruction file.</summary>
public sealed record InstructionFile(string Name, string FullPath, string Content);

/// <summary>The combined result of instruction file discovery.</summary>
public sealed class InstructionsResult
{
    private readonly List<InstructionFile> _files = [];

    public IReadOnlyList<InstructionFile> Files => _files;

    internal void Add(InstructionFile file) => _files.Add(file);

    /// <summary>True if any instruction files were found.</summary>
    public bool HasInstructions => Files.Count > 0;

    /// <summary>
    /// Merges all instruction files into a single system prompt section.
    /// </summary>
    public string ToSystemPrompt()
    {
        if (Files.Count == 0) return string.Empty;

        var parts = new List<string>();
        foreach (var file in Files)
        {
            parts.Add($"# Project Instructions ({file.Name})\n\n{file.Content}");
        }

        return string.Join("\n\n---\n\n", parts);
    }

    /// <summary>Short summary for /instructions command.</summary>
    public string ToSummary()
    {
        if (Files.Count == 0) return "No project instructions found.";

        var lines = Files.Select(f =>
            $"  ✓ {f.Name} ({f.FullPath}, {f.Content.Length} chars)");
        return $"Loaded instructions:\n{string.Join('\n', lines)}";
    }
}
