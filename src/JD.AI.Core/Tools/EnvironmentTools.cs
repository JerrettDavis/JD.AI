using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for inspecting the runtime environment, OS, and system information.
/// </summary>
[ToolPlugin("environment")]
public sealed class EnvironmentTools
{
    [KernelFunction("get_environment")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Get information about the current environment: OS, architecture, .NET runtime, " +
        "working directory, git version, available disk space, and environment variables.")]
    public static async Task<string> GetEnvironmentAsync(
        [Description("Include environment variables in output")] bool includeEnvVars = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Environment Info ===");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($".NET Runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Working Directory: {Directory.GetCurrentDirectory()}");
        sb.AppendLine($"User: {Environment.UserName}");
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");

        // Disk space
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            var root = Path.GetPathRoot(cwd);
            if (root is not null)
            {
                var driveInfo = new DriveInfo(root);
                sb.AppendLine($"Disk ({root}): {driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024)} GB free / {driveInfo.TotalSize / (1024 * 1024 * 1024)} GB total");
            }
        }
        catch
        {
            // Drive info may not be available on all platforms
        }

        // Git version
        try
        {
            var gitVersion = await RunCommandAsync("git", "--version").ConfigureAwait(false);
            sb.AppendLine($"Git: {gitVersion.Trim()}");
        }
        catch
        {
            sb.AppendLine("Git: not available");
        }

        // .NET SDK version
        try
        {
            var dotnetVersion = await RunCommandAsync("dotnet", "--version").ConfigureAwait(false);
            sb.AppendLine($".NET SDK: {dotnetVersion.Trim()}");
        }
        catch
        {
            sb.AppendLine(".NET SDK: not available");
        }

        if (includeEnvVars)
        {
            sb.AppendLine();
            sb.AppendLine("=== Environment Variables ===");
            var envVars = Environment.GetEnvironmentVariables();
            foreach (var key in envVars.Keys.Cast<string>().OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                var value = envVars[key]?.ToString() ?? "";
                // Mask potential secrets
                if (key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase))
                {
                    value = "***";
                }

                sb.AppendLine($"  {key}={value}");
            }
        }

        return sb.ToString();
    }

    private static async Task<string> RunCommandAsync(string command, string args)
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

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return output;
    }
}
