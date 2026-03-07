using System.ComponentModel;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for reading from and writing to the system clipboard.
/// Uses platform-specific commands (pbcopy/pbpaste, xclip, clip.exe/PowerShell).
/// </summary>
[ToolPlugin("clipboard")]
public sealed class ClipboardTools
{
    [KernelFunction("read_clipboard")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Read the current text content from the system clipboard.")]
    public static async Task<string> ReadClipboardAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return await RunCommandAsync("powershell", "-NoProfile -Command Get-Clipboard").ConfigureAwait(false);
        }

        if (OperatingSystem.IsMacOS())
        {
            return await RunCommandAsync("pbpaste", "").ConfigureAwait(false);
        }

        // Linux: try xclip, then xsel
        var result = await RunCommandAsync("xclip", "-selection clipboard -o").ConfigureAwait(false);
        if (result.StartsWith("Failed", StringComparison.Ordinal))
        {
            result = await RunCommandAsync("xsel", "--clipboard --output").ConfigureAwait(false);
        }

        return result;
    }

    [KernelFunction("write_clipboard")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Write text to the system clipboard.")]
    public static async Task<string> WriteClipboardAsync(
        [Description("Text to write to the clipboard")] string text)
    {
        if (OperatingSystem.IsWindows())
        {
            return await RunCommandWithInputAsync("clip", "", text).ConfigureAwait(false);
        }

        if (OperatingSystem.IsMacOS())
        {
            return await RunCommandWithInputAsync("pbcopy", "", text).ConfigureAwait(false);
        }

        // Linux: try xclip
        var result = await RunCommandWithInputAsync("xclip", "-selection clipboard", text).ConfigureAwait(false);
        if (result.StartsWith("Failed", StringComparison.Ordinal))
        {
            result = await RunCommandWithInputAsync("xsel", "--clipboard --input", text).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<string> RunCommandAsync(string command, string args)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(command, args).ConfigureAwait(false);
            return result.Success
                ? (string.IsNullOrEmpty(result.StandardOutput) ? "(clipboard empty)" : result.StandardOutput)
                : $"Failed: exit code {result.ExitCode}";
        }
        catch (Exception ex)
        {
            return $"Failed to run '{command}': {ex.Message}";
        }
    }

    private static async Task<string> RunCommandWithInputAsync(string command, string args, string input)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(command, args, standardInput: input).ConfigureAwait(false);
            return result.Success
                ? $"Copied {input.Length} characters to clipboard."
                : $"Failed: exit code {result.ExitCode}";
        }
        catch (Exception ex)
        {
            return $"Failed to run '{command}': {ex.Message}";
        }
    }
}
