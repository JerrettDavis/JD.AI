using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Shell execution tool for the AI agent.
/// </summary>
[ToolPlugin("shell")]
public sealed class ShellTools
{
    [KernelFunction("run_command")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("Execute a shell command and return its output. Use for builds, tests, git operations, etc.")]
    public static async Task<string> RunCommandAsync(
        [Description("The command to execute")] string command,
        [Description("Working directory (defaults to cwd)")] string? cwd = null,
        [Description("Timeout in seconds (default 60)")] int timeoutSeconds = 60)
    {
        // Common Unix-style command used by models; map to cmd-compatible form.
        if (OperatingSystem.IsWindows() &&
            string.Equals(command.Trim(), "pwd", StringComparison.OrdinalIgnoreCase))
        {
            command = "cd";
        }

        var workDir = cwd ?? Directory.GetCurrentDirectory();

        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "cmd.exe" : "/bin/sh";
        var arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

        var processResult = await ProcessExecutor.RunAsync(
            fileName, arguments,
            workingDirectory: workDir,
            timeout: TimeSpan.FromSeconds(timeoutSeconds));

        var result = new StringBuilder();
        result.AppendLine($"Exit code: {processResult.ExitCode}");

        if (!string.IsNullOrEmpty(processResult.StandardOutput))
        {
            result.AppendLine("--- stdout ---");
            result.AppendLine(processResult.StandardOutput);
        }

        if (!string.IsNullOrEmpty(processResult.StandardError))
        {
            result.AppendLine("--- stderr ---");
            result.AppendLine(processResult.StandardError);
        }

        // Truncate very long output
        const int maxLength = 10000;
        var output = result.ToString();
        if (output.Length > maxLength)
        {
            output = string.Concat(output.AsSpan(0, maxLength), $"\n... [truncated, {output.Length - maxLength} more chars]");
        }

        return output;
    }
}
