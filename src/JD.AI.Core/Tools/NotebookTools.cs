using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Code execution (REPL) tools for running snippets in various languages.
/// </summary>
[ToolPlugin("notebook")]
public sealed class NotebookTools
{

    [KernelFunction("execute_code")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description(
        "Execute a code snippet in the specified language and return the output. " +
        "Supported languages: csharp (dotnet-script), python, node (JavaScript), bash/powershell.")]
    public static async Task<string> ExecuteCodeAsync(
        [Description("Language: csharp, python, node, bash, powershell")] string language,
        [Description("The code to execute")] string code,
        [Description("Timeout in seconds (default 30)")] int timeoutSeconds = 30)
    {
        var (command, args, tempFile) = ResolveRuntime(language, code);
        if (command is null)
        {
            return $"Unsupported language: '{language}'. Supported: csharp, python, node, bash, powershell.";
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300));
            return await RunCodeAsync(command, args, timeout).ConfigureAwait(false);
        }
        finally
        {
            if (tempFile is not null && File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    private static (string? Command, string Args, string? TempFile) ResolveRuntime(
        string language,
        string code)
    {
        var lang = language.Trim().ToUpperInvariant();

        switch (lang)
        {
            case "CSHARP" or "C#" or "CS":
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"jdai_{Guid.NewGuid():N}.csx");
                    File.WriteAllText(temp, code, Encoding.UTF8);
                    return ("dotnet-script", temp, temp);
                }

            case "PYTHON" or "PY":
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"jdai_{Guid.NewGuid():N}.py");
                    File.WriteAllText(temp, code, Encoding.UTF8);
                    return ("python", temp, temp);
                }

            case "NODE" or "JAVASCRIPT" or "JS":
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"jdai_{Guid.NewGuid():N}.mjs");
                    File.WriteAllText(temp, code, Encoding.UTF8);
                    return ("node", temp, temp);
                }

            case "BASH" or "SH":
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"jdai_{Guid.NewGuid():N}.sh");
                    File.WriteAllText(temp, code, Encoding.UTF8);
                    return ("bash", temp, temp);
                }

            case "POWERSHELL" or "PWSH" or "PS1":
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"jdai_{Guid.NewGuid():N}.ps1");
                    File.WriteAllText(temp, code, Encoding.UTF8);
                    return ("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File {temp}", temp);
                }

            default:
                return (null, "", null);
        }
    }

    private static async Task<string> RunCodeAsync(string command, string args, TimeSpan timeout)
    {
        ProcessResult result;
        try
        {
            result = await ProcessExecutor.RunAsync(command, args, timeout: timeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Failed to start '{command}': {ex.Message}. Is the runtime installed?";
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            sb.AppendLine(result.StandardOutput);
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            sb.AppendLine($"[stderr] {result.StandardError}");
        sb.AppendLine($"[exit code: {result.ExitCode}]");

        var output = sb.ToString();
        return string.IsNullOrWhiteSpace(output) ? "(no output)" : output;
    }
}
