using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Config;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Shell execution tool for the AI agent.
/// </summary>
[ToolPlugin("shell")]
public sealed class ShellTools
{
    private const string DefaultShellEnvironmentVariable = "JDAI_DEFAULT_SHELL";

    [KernelFunction("run_command")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("Execute a shell command and return its output. Use for builds, tests, git operations, etc.")]
    public static async Task<string> RunCommandAsync(
        [Description("The command to execute")] string command,
        [Description("Working directory (defaults to cwd)")] string? cwd = null,
        [Description("Timeout in seconds (default 60)")] int timeoutSeconds = 60)
    {
        var workDir = cwd ?? Directory.GetCurrentDirectory();
        var configuredShell = await ResolveConfiguredShellAsync(workDir).ConfigureAwait(false);
        var (fileName, arguments) = ResolveShellInvocation(command, configuredShell);

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

    internal static (string FileName, string Arguments) ResolveShellInvocation(
        string command,
        string? configuredShell)
    {
        var trimmedCommand = command.Trim();

        if (string.IsNullOrWhiteSpace(configuredShell))
        {
            return ResolveDefaultShellInvocation(trimmedCommand);
        }

        var shell = configuredShell.Trim();

        // Advanced mode: first token is executable, remainder is args prefix/template.
        // If args include {command}, it's replaced verbatim.
        if (TrySplitExecutableAndArgs(shell, out var executable, out var argsPrefix))
        {
            if (argsPrefix.Contains("{command}", StringComparison.OrdinalIgnoreCase))
            {
                return (executable, argsPrefix.Replace("{command}", trimmedCommand, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(argsPrefix))
            {
                if (argsPrefix.EndsWith("-c", StringComparison.OrdinalIgnoreCase) ||
                    argsPrefix.EndsWith("-lc", StringComparison.OrdinalIgnoreCase))
                {
                    return (executable, $"{argsPrefix} \"{EscapeForDoubleQuotedArg(trimmedCommand)}\"");
                }

                return (executable, $"{argsPrefix} {trimmedCommand}");
            }
        }

        var normalized = NormalizeShellAlias(shell);
        return normalized switch
        {
            "cmd" => BuildCmdInvocation(trimmedCommand),
            "powershell" => ("powershell", $"-NoProfile -Command {trimmedCommand}"),
            "pwsh" => ("pwsh", $"-NoProfile -Command {trimmedCommand}"),
            "bash" => ("bash", $"-lc \"{EscapeForDoubleQuotedArg(trimmedCommand)}\""),
            "sh" => ("sh", $"-c \"{EscapeForDoubleQuotedArg(trimmedCommand)}\""),
            "gitbash" => ("bash", $"-lc \"{EscapeForDoubleQuotedArg(trimmedCommand)}\""),
            _ => ResolveCustomExecutableInvocation(shell, trimmedCommand),
        };
    }

    private static async Task<string?> ResolveConfiguredShellAsync(string workingDirectory)
    {
        var env = Environment.GetEnvironmentVariable(DefaultShellEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        try
        {
            using var store = new AtomicConfigStore();
            return await store.GetDefaultShellAsync(workingDirectory).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static (string FileName, string Arguments) ResolveDefaultShellInvocation(string command)
    {
        if (OperatingSystem.IsWindows())
            return BuildCmdInvocation(command);

        return ("/bin/sh", $"-c \"{EscapeForDoubleQuotedArg(command)}\"");
    }

    private static (string FileName, string Arguments) BuildCmdInvocation(string command)
    {
        // Common Unix-style command used by models; map to cmd-compatible form.
        if (OperatingSystem.IsWindows() &&
            string.Equals(command, "pwd", StringComparison.OrdinalIgnoreCase))
        {
            command = "cd";
        }

        return ("cmd.exe", $"/c {command}");
    }

    private static (string FileName, string Arguments) ResolveCustomExecutableInvocation(
        string executable,
        string command)
    {
        if (OperatingSystem.IsWindows())
            return (executable, $"/c {command}");

        return (executable, $"-c \"{EscapeForDoubleQuotedArg(command)}\"");
    }

    private static string NormalizeShellAlias(string shell)
    {
        var value = shell.Trim().ToLowerInvariant();
        value = value.Replace(".exe", string.Empty, StringComparison.Ordinal);
        value = value.Replace("-", string.Empty, StringComparison.Ordinal);
        value = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        return value;
    }

    internal static bool TrySplitExecutableAndArgs(
        string shellSetting,
        out string executable,
        out string argsPrefix)
    {
        executable = string.Empty;
        argsPrefix = string.Empty;

        if (string.IsNullOrWhiteSpace(shellSetting))
            return false;

        var text = shellSetting.Trim();
        if (text.Length == 0)
            return false;

        if (text[0] == '"' || text[0] == '\'')
        {
            var quote = text[0];
            var closing = text.IndexOf(quote, 1);
            if (closing <= 0)
                return false;

            executable = text[1..closing];
            argsPrefix = text[(closing + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(executable);
        }

        var firstSpace = text.IndexOf(' ');
        if (firstSpace < 0)
        {
            executable = text;
            return true;
        }

        executable = text[..firstSpace];
        argsPrefix = text[(firstSpace + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(executable);
    }

    private static string EscapeForDoubleQuotedArg(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
