using System.Diagnostics;
using System.Text;

namespace JD.AI.Core.Installation;

/// <summary>
/// Launches a self-update as a detached, out-of-process script so the running
/// binary can be replaced after the current process exits.
///
/// On Windows, executable files are locked by the OS while running, so
/// <c>dotnet tool update -g JD.AI</c> fails in-process. This launcher writes
/// a temporary script that waits for the current process to exit, then performs
/// the update. On Unix, file replacement works while a process is running, so
/// this is only used on Windows.
/// </summary>
public static class DetachedUpdater
{
    /// <summary>
    /// Launches an out-of-process updater that runs <c>dotnet tool update -g &lt;packageId&gt;</c>
    /// after a short delay (giving the caller time to exit). Returns immediately.
    /// </summary>
    /// <param name="packageId">The dotnet tool package ID to update.</param>
    /// <param name="targetVersion">Specific version to pin, or null for latest.</param>
    /// <returns>An <see cref="InstallResult"/> with <c>LaunchedDetached = true</c> on success.</returns>
    public static InstallResult Launch(string packageId, string? targetVersion = null)
    {
        try
        {
            var versionArg = targetVersion is not null ? $" --version {targetVersion}" : "";
            var updateCmd = $"dotnet tool update -g {packageId}{versionArg}";

            var scriptPath = WriteUpdateScript(updateCmd);
            StartDetached(scriptPath);

            return new InstallResult(
                Success: true,
                Output: $"Update process launched. Exit jdai and restart to use the new version.",
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

    // ── Internals ────────────────────────────────────────────────────────

    private static string WriteUpdateScript(string updateCmd)
    {
        var tempDir = Path.GetTempPath();

        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(tempDir, $"jdai-update-{Guid.NewGuid():N}.bat");
            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("echo.");
            bat.AppendLine("echo  JD.AI Updater");
            bat.AppendLine("echo  ============");
            bat.AppendLine("echo.");
            bat.AppendLine("echo  Waiting for jdai to exit...");
            bat.AppendLine("timeout /t 3 /nobreak >nul");
            bat.AppendLine($"echo  Running: {updateCmd}");
            bat.AppendLine(updateCmd);
            bat.AppendLine("if %ERRORLEVEL% == 0 (");
            bat.AppendLine("    echo.");
            bat.AppendLine("    echo  [OK] Update applied successfully. Restart jdai.");
            bat.AppendLine(") else (");
            bat.AppendLine("    echo.");
            bat.AppendLine("    echo  [ERROR] Update failed. Try running manually:");
            bat.AppendLine($"    echo    {updateCmd}");
            bat.AppendLine(")");
            // Self-delete the script after a delay
            bat.AppendLine("echo.");
            bat.AppendLine("pause");
            bat.AppendLine($"del /f /q \"{scriptPath}\" >nul 2>&1");
            File.WriteAllText(scriptPath, bat.ToString(), Encoding.ASCII);
            return scriptPath;
        }
        else
        {
            var scriptPath = Path.Combine(tempDir, $"jdai-update-{Guid.NewGuid():N}.sh");
            var sh = new StringBuilder();
            sh.AppendLine("#!/bin/sh");
            sh.AppendLine("echo ''");
            sh.AppendLine("echo 'JD.AI Updater'");
            sh.AppendLine("echo '============='");
            sh.AppendLine("echo 'Waiting for jdai to exit...'");
            sh.AppendLine("sleep 3");
            sh.AppendLine($"echo 'Running: {updateCmd}'");
            sh.AppendLine(updateCmd);
            sh.AppendLine("if [ $? -eq 0 ]; then");
            sh.AppendLine("    echo '[OK] Update applied. Restart jdai.'");
            sh.AppendLine("else");
            sh.AppendLine("    echo '[ERROR] Update failed. Run manually:'");
            sh.AppendLine($"    echo '  {updateCmd}'");
            sh.AppendLine("fi");
            sh.AppendLine($"rm -f \"{scriptPath}\"");
            File.WriteAllText(scriptPath, sh.ToString());
            // Make executable
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            return scriptPath;
        }
    }

    private static void StartDetached(string scriptPath)
    {
        ProcessStartInfo psi;

        if (OperatingSystem.IsWindows())
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"JD.AI Updater\" cmd.exe /k \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
            };
        }
        else
        {
            // Try common terminal emulators in order
            var (terminal, args) = FindUnixTerminal(scriptPath);
            psi = new ProcessStartInfo
            {
                FileName = terminal,
                Arguments = args,
                UseShellExecute = true,
            };
        }

        using var proc = Process.Start(psi);
        // Don't wait — detached
    }

    private static (string Terminal, string Args) FindUnixTerminal(string scriptPath)
    {
        var escaped = scriptPath.Replace("'", "'\\''");

        // xterm-based (widely available)
        if (ExistsOnPath("xterm"))
            return ("xterm", $"-e sh '{escaped}'");

        // GNOME terminal
        if (ExistsOnPath("gnome-terminal"))
            return ("gnome-terminal", $"-- sh '{escaped}'");

        // KDE konsole
        if (ExistsOnPath("konsole"))
            return ("konsole", $"-e sh '{escaped}'");

        // macOS Terminal
        if (OperatingSystem.IsMacOS())
            return ("open", $"-a Terminal '{escaped}'");

        // Fallback: run invisible in background (user won't see output but update will run)
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
