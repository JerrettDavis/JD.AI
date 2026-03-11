using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tool for applying multiple edits to one or more files in a single atomic operation.
/// If any edit fails validation, no files are modified.
/// </summary>
[ToolPlugin("batchEdit")]
public sealed class BatchEditTools
{
    [KernelFunction("batch_edit_files")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description(
        "Apply multiple text replacements across one or more files atomically. " +
        "All edits are validated first — if any oldText is not found, no files are modified. " +
        "Use this instead of multiple edit_file calls when you need to make coordinated changes.")]
    public static string BatchEditFiles(
        [Description(
            "JSON array of edits: [{\"path\":\"file.cs\",\"oldText\":\"before\",\"newText\":\"after\"}]. " +
            "Multiple edits to the same file are applied sequentially in order.")]
        string editsJson)
    {
        var edits = System.Text.Json.JsonSerializer.Deserialize<EditEntry[]>(editsJson, JsonOptions);
        if (edits is null || edits.Length == 0)
        {
            return "No edits provided.";
        }

        // Group edits by file path (preserve order within each file)
        var byFile = new Dictionary<string, List<EditEntry>>(StringComparer.Ordinal);
        foreach (var edit in edits)
        {
            if (string.IsNullOrEmpty(edit.Path))
            {
                return OutputFormatter.Error("edit missing 'path' field.");
            }

            if (PathGuard.IsProtected(edit.Path))
            {
                return OutputFormatter.Error($"Access denied: '{edit.Path}' is inside a protected directory.");
            }

            if (!File.Exists(edit.Path))
            {
                return OutputFormatter.Error($"file not found — {edit.Path}");
            }

            if (!byFile.TryGetValue(edit.Path, out var list))
            {
                list = [];
                byFile[edit.Path] = list;
            }

            list.Add(edit);
        }

        // Phase 1: Validate — apply edits in memory and check all oldTexts are found
        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, fileEdits) in byFile)
        {
            var content = File.ReadAllText(path);
            foreach (var edit in fileEdits)
            {
                if (edit.OldText is null)
                {
                    return OutputFormatter.Error($"edit for {path} missing 'oldText'.");
                }

                if (!content.Contains(edit.OldText, StringComparison.Ordinal))
                {
                    // Provide context about what was expected
                    var preview = edit.OldText.Length > 60
                        ? string.Concat(edit.OldText.AsSpan(0, 57), "...")
                        : edit.OldText;
                    return OutputFormatter.Error($"old text not found in {path}: \"{preview}\". No files modified.");
                }

                content = content.Replace(edit.OldText, edit.NewText ?? "", StringComparison.Ordinal);
            }

            results[path] = content;
        }

        // Phase 2: Write all files
        foreach (var (path, content) in results)
        {
            File.WriteAllText(path, content);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Applied {edits.Length} edit(s) across {results.Count} file(s):");
        foreach (var (path, fileEdits) in byFile)
        {
            sb.AppendLine($"  {path}: {fileEdits.Count} edit(s)");
        }

        return sb.ToString();
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = JD.AI.Core.Infrastructure.JsonDefaults.Options;

    private sealed class EditEntry
    {
        public string? Path { get; set; }
        public string? OldText { get; set; }
        public string? NewText { get; set; }
    }
}
