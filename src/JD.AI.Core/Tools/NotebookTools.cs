using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using JD.AI.Core.Attributes;
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
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var sb = new StringBuilder();

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return $"Failed to start '{command}': {ex.Message}. Is the runtime installed?";
        }

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                sb.AppendLine(stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                sb.AppendLine($"[stderr] {stderr}");
            }

            sb.AppendLine($"[exit code: {process.ExitCode}]");
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort kill
            }

            return $"Execution timed out after {timeout.TotalSeconds}s. Process killed.";
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? "(no output)" : result;
    }
}
