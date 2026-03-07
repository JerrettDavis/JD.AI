using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace JD.AI.Core.Installation;

/// <summary>
/// Launches a self-update as a detached, out-of-process script so the running
/// binary can be replaced after the current process exits.
///
/// On Windows, executable files are locked by the OS while running, so
/// <c>dotnet tool update -g JD.AI</c> fails in-process. This launcher writes
/// a temporary script that waits for the parent process to fully exit (by polling
/// the PID), then performs the update. On Unix, file replacement works while a
/// process is running, so this is only used on Windows.
/// </summary>
public static class DetachedUpdater
{
    // NuGet package IDs: alphanumeric, dots, hyphens, underscores only.
    private static readonly Regex SafePackageIdPattern =
        new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // SemVer-ish: digits, dots, hyphens, alphanumerics (pre-release suffix).
    private static readonly Regex SafeVersionPattern =
        new(@"^\d+\.\d+[\.\d]*(-[A-Za-z0-9._-]+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Launches an out-of-process updater that runs <c>dotnet tool update -g &lt;packageId&gt;</c>
    /// after the current process exits. Returns immediately.
    /// </summary>
    /// <param name="packageId">The dotnet tool package ID to update.</param>
    /// <param name="targetVersion">Specific version to pin, or null for latest.</param>
    /// <returns>An <see cref="InstallResult"/> with <c>LaunchedDetached = true</c> on success.</returns>
    public static InstallResult Launch(string packageId, string? targetVersion = null)
    {
        try
        {
            ValidatePackageId(packageId);
            if (targetVersion is not null)
                ValidateVersion(targetVersion);

            var parentPid = Environment.ProcessId;
            var scriptPath = WriteUpdateScript(packageId, targetVersion, parentPid);
            StartDetached(scriptPath);

            return new InstallResult(
                Success: true,
                Output: "Update process launched. Exit jdai and restart to use the new version.",
                RequiresRestart: true,
                LaunchedDetached: true);
        }
        catch (Exception ex)
        {
            return new InstallResult(
                Success: false,
                Output: $"Failed to launch update process: {ex.Message}");
        }
    }

    // ── Validation ────────────────────────────────────────────────────────

    private static void ValidatePackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !SafePackageIdPattern.IsMatch(packageId))
            throw new ArgumentException(
                $"Package ID '{packageId}' contains characters that are not allowed in a NuGet package ID.", nameof(packageId));
    }

    private static void ValidateVersion(string version)
    {
        if (!SafeVersionPattern.IsMatch(version))
            throw new ArgumentException(
                $"Version '{version}' contains characters that are not allowed in a version string.", nameof(version));
    }

    // ── Script writing ────────────────────────────────────────────────────

    private static string WriteUpdateScript(string packageId, string? targetVersion, int parentPid)
    {
        var tempDir = Path.GetTempPath();
        var versionArg = targetVersion is not null ? $" --version {targetVersion}" : "";
        var updateCmd = $"dotnet tool update -g {packageId}{versionArg}";

        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(tempDir, $"jdai-update-{Guid.NewGuid():N}.bat");
            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            // PID embedded as integer literal — no injection risk.
            bat.AppendLine($"set PARENT_PID={parentPid}");
            bat.AppendLine("echo.");
            bat.AppendLine("echo  JD.AI Updater");
            bat.AppendLine("echo  ============");
            bat.AppendLine("echo.");
            bat.AppendLine("echo  Waiting for jdai (PID %PARENT_PID%) to exit...");
            bat.AppendLine(":wait_loop");
            bat.AppendLine("  tasklist /FI \"PID eq %PARENT_PID%\" 2>nul | find \"%PARENT_PID%\" >nul");
            bat.AppendLine("  if not errorlevel 1 (");
            bat.AppendLine("    timeout /t 1 /nobreak >nul");
            bat.AppendLine("    goto wait_loop");
            bat.AppendLine("  )");
            bat.AppendLine("echo  jdai has exited. Applying update...");
            bat.AppendLine("echo.");
            bat.AppendLine($"echo  Running: {updateCmd}");
            bat.AppendLine(updateCmd);
            bat.AppendLine("if %ERRORLEVEL% == 0 (");
            bat.AppendLine("    echo.");
            bat.AppendLine("    echo  [OK] Update applied successfully. Restart jdai.");
            bat.AppendLine(") else (");
            bat.AppendLine("    echo.");
            bat.AppendLine("    echo  [ERROR] Update failed. Run manually:");
            bat.AppendLine($"    echo    {updateCmd}");
            bat.AppendLine(")");
            bat.AppendLine("echo.");
            bat.AppendLine("pause");
            // Self-delete — no temp file accumulation.
            bat.AppendLine("del /f /q \"%~f0\" >nul 2>&1");
            File.WriteAllText(scriptPath, bat.ToString(), Encoding.ASCII);
            return scriptPath;
        }
        else
        {
            var scriptPath = Path.Combine(tempDir, $"jdai-update-{Guid.NewGuid():N}.sh");
            var sh = new StringBuilder();
            sh.AppendLine("#!/bin/sh");
            sh.AppendLine($"PARENT_PID={parentPid}");
            sh.AppendLine("echo ''");
            sh.AppendLine("echo 'JD.AI Updater'");
            sh.AppendLine("echo '============='");
            sh.AppendLine("echo ''");
            sh.AppendLine("echo \"Waiting for jdai (PID $PARENT_PID) to exit...\"");
            sh.AppendLine("while kill -0 \"$PARENT_PID\" 2>/dev/null; do sleep 1; done");
            sh.AppendLine("echo 'jdai has exited. Applying update...'");
            sh.AppendLine("echo ''");
            sh.AppendLine($"echo 'Running: {updateCmd}'");
            sh.AppendLine(updateCmd);
            sh.AppendLine("if [ $? -eq 0 ]; then");
            sh.AppendLine("    echo '[OK] Update applied. Restart jdai.'");
            sh.AppendLine("else");
            sh.AppendLine("    echo '[ERROR] Update failed. Run manually:'");
            sh.AppendLine($"    echo '  {updateCmd}'");
            sh.AppendLine("fi");
            sh.AppendLine("SCRIPT_PATH=\"$0\"");
            sh.AppendLine("rm -f \"$SCRIPT_PATH\"");
            File.WriteAllText(scriptPath, sh.ToString());
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            return scriptPath;
        }
    }

    // ── Process launch ────────────────────────────────────────────────────

    private static void StartDetached(string scriptPath)
    {
        ProcessStartInfo psi;

        if (OperatingSystem.IsWindows())
        {
            // `start "title" "script.bat"` opens a new visible cmd.exe window.
            // UseShellExecute = false lets us run cmd.exe directly without a shell wrapper.
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"JD.AI Updater\" \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
        else
        {
            var (terminal, args) = FindUnixTerminal(scriptPath);
            psi = new ProcessStartInfo
            {
                FileName = terminal,
                Arguments = args,
                UseShellExecute = true,
            };
        }

        using var proc = Process.Start(psi);
        // Return immediately — do not wait for the updater.
    }

    private static (string Terminal, string Args) FindUnixTerminal(string scriptPath)
    {
        var escaped = scriptPath.Replace("'", "'\\''");

        if (ExistsOnPath("xterm"))
            return ("xterm", $"-e sh '{escaped}'");

        if (ExistsOnPath("gnome-terminal"))
            return ("gnome-terminal", $"-- sh '{escaped}'");

        if (ExistsOnPath("konsole"))
            return ("konsole", $"-e sh '{escaped}'");

        if (OperatingSystem.IsMacOS())
            return ("open", $"-a Terminal '{escaped}'");

        // Fallback: run silently in background.
        return ("sh", $"'{escaped}' &");
    }

    private static bool ExistsOnPath(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        return pathVar
            .Split(sep, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, name)));
    }
}
