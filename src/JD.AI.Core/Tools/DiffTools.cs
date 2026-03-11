using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for creating and applying unified diff patches.
/// Enables structured multi-location file edits via standard patch format.
/// </summary>
[ToolPlugin("diff")]
public sealed class DiffTools
{
    [KernelFunction("create_patch")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Create a unified diff patch from a list of file edits. Each edit specifies " +
        "a file path, the old text to find, and the new text to replace it with. " +
        "Returns a unified diff string that can be reviewed or applied with apply_patch.")]
    public static string CreatePatch(
        [Description("JSON array of edits: [{\"path\":\"file.cs\",\"oldText\":\"before\",\"newText\":\"after\"}]")]
        string editsJson)
    {
        var edits = System.Text.Json.JsonSerializer.Deserialize<PatchEdit[]>(editsJson, JsonOptions);
        if (edits is null || edits.Length == 0)
        {
            return "No edits provided.";
        }

        var sb = new StringBuilder();
        foreach (var edit in edits)
        {
            if (string.IsNullOrEmpty(edit.Path))
            {
                sb.AppendLine("# Skipped edit: missing path");
                continue;
            }

            if (PathGuard.IsProtected(edit.Path))
            {
                sb.AppendLine($"# Skipped edit: access denied — '{edit.Path}' is inside a protected directory");
                continue;
            }

            if (!File.Exists(edit.Path))
            {
                sb.AppendLine($"# Skipped edit: file not found — {edit.Path}");
                continue;
            }

            var original = File.ReadAllText(edit.Path);
            var modified = original.Replace(edit.OldText ?? "", edit.NewText ?? "");

            if (string.Equals(original, modified, StringComparison.Ordinal))
            {
                sb.AppendLine($"# No changes for {edit.Path} (old text not found)");
                continue;
            }

            sb.AppendLine($"--- a/{edit.Path}");
            sb.AppendLine($"+++ b/{edit.Path}");
            AppendUnifiedDiff(sb, original, modified);
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? "No changes generated." : result;
    }

    [KernelFunction("apply_patch")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description(
        "Apply a set of text replacements to files. Each edit specifies a file path, " +
        "old text to find, and new text to replace it with. All edits are applied atomically — " +
        "if any edit fails to find its old text, no files are modified.")]
    public static string ApplyPatch(
        [Description("JSON array of edits: [{\"path\":\"file.cs\",\"oldText\":\"before\",\"newText\":\"after\"}]")]
        string editsJson)
    {
        var edits = System.Text.Json.JsonSerializer.Deserialize<PatchEdit[]>(editsJson, JsonOptions);
        if (edits is null || edits.Length == 0)
        {
            return "No edits provided.";
        }

        // Phase 1: Validate all edits before modifying anything
        var pending = new List<(string Path, string Original, string Modified)>();
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

            var content = File.ReadAllText(edit.Path);
            if (edit.OldText is not null && !content.Contains(edit.OldText, StringComparison.Ordinal))
            {
                return OutputFormatter.Error($"old text not found in {edit.Path}. Patch aborted (no files modified).");
            }

            var modified = edit.OldText is not null
                ? content.Replace(edit.OldText, edit.NewText ?? "", StringComparison.Ordinal)
                : content + (edit.NewText ?? "");

            pending.Add((edit.Path, content, modified));
        }

        // Phase 2: Apply all edits
        var applied = 0;
        foreach (var (path, _, modified) in pending)
        {
            File.WriteAllText(path, modified);
            applied++;
        }

        return $"Applied {applied} edit(s) across {pending.Select(p => p.Path).Distinct(StringComparer.Ordinal).Count()} file(s).";
    }

    private static void AppendUnifiedDiff(StringBuilder sb, string original, string modified)
    {
        var oldLines = original.Split('\n');
        var newLines = modified.Split('\n');

        // Simple context diff — show changed lines with 3 lines of context
        var oldIdx = 0;
        var newIdx = 0;
        while (oldIdx < oldLines.Length || newIdx < newLines.Length)
        {
            if (oldIdx < oldLines.Length && newIdx < newLines.Length && string.Equals(oldLines[oldIdx], newLines[newIdx], StringComparison.Ordinal))
            {
                oldIdx++;
                newIdx++;
                continue;
            }

            // Found a difference — emit hunk
            var hunkStart = Math.Max(0, oldIdx - 3);
            sb.AppendLine($"@@ -{hunkStart + 1},{Math.Min(oldLines.Length, oldIdx + 4) - hunkStart} +{hunkStart + 1},{Math.Min(newLines.Length, newIdx + 4) - hunkStart} @@");

            // Context before
            for (var i = hunkStart; i < oldIdx; i++)
            {
                sb.AppendLine($" {oldLines[i]}");
            }

            // Changed lines
            while (oldIdx < oldLines.Length && (newIdx >= newLines.Length || !string.Equals(oldLines[oldIdx], newLines[newIdx], StringComparison.Ordinal)))
            {
                sb.AppendLine($"-{oldLines[oldIdx]}");
                oldIdx++;
            }

            while (newIdx < newLines.Length && (oldIdx >= oldLines.Length || !string.Equals(oldLines[oldIdx], newLines[newIdx], StringComparison.Ordinal)))
            {
                sb.AppendLine($"+{newLines[newIdx]}");
                newIdx++;
            }

            // Context after
            var contextEnd = Math.Min(oldLines.Length, oldIdx + 3);
            for (var i = oldIdx; i < contextEnd; i++)
            {
                if (i < oldLines.Length)
                {
                    sb.AppendLine($" {oldLines[i]}");
                }
            }

            oldIdx = contextEnd;
            newIdx = Math.Min(newLines.Length, newIdx + 3);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = JD.AI.Core.Infrastructure.JsonDefaults.Options;

    private sealed class PatchEdit
    {
        public string? Path { get; set; }
        public string? OldText { get; set; }
        public string? NewText { get; set; }
    }
}
