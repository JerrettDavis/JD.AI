using System.Reflection;
using System.Runtime.InteropServices;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Installation;

/// <summary>
/// Detects how JD.AI was installed on the current machine by examining the
/// executable path and querying package managers.
/// </summary>
public static class InstallationDetector
{
    /// <summary>
    /// Detects the current installation method, executable path, version, and RID.
    /// </summary>
    public static async Task<InstallationInfo> DetectAsync(CancellationToken ct = default)
    {
        var exePath = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "jdai");
        var version = GetCurrentVersion();
        var rid = GetCurrentRid();

        var kind = await DetectKindAsync(exePath, ct).ConfigureAwait(false);
        return new InstallationInfo(kind, exePath, version, rid);
    }

    /// <summary>Gets the running assembly's informational version (without git metadata).</summary>
    public static string GetCurrentVersion()
    {
        var attr = typeof(InstallationDetector).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr?.InformationalVersion is { } ver)
        {
            var plusIdx = ver.IndexOf('+', StringComparison.Ordinal);
            return plusIdx >= 0 ? ver[..plusIdx] : ver;
        }

        var asmVer = typeof(InstallationDetector).Assembly.GetName().Version;
        return asmVer?.ToString(3) ?? "0.0.0";
    }

    /// <summary>
    /// Builds a runtime identifier for the current platform (e.g. <c>win-x64</c>, <c>osx-arm64</c>).
    /// </summary>
    public static string GetCurrentRid()
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };

        return $"{os}-{arch}";
    }

    private static async Task<InstallKind> DetectKindAsync(string exePath, CancellationToken ct)
    {
        // 1. dotnet tool — binary lives under ~/.dotnet/tools/
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            var dotnetToolsDir = Path.Combine(home, ".dotnet", "tools");
            if (exePath.StartsWith(dotnetToolsDir, StringComparison.OrdinalIgnoreCase))
                return InstallKind.DotnetTool;
        }

        // 2. Scoop — path contains scoop directory
        if (exePath.Contains(Path.DirectorySeparatorChar + "scoop" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return InstallKind.Scoop;

        // 3. Chocolatey — path contains chocolatey directory
        if (exePath.Contains(Path.DirectorySeparatorChar + "chocolatey" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return InstallKind.Chocolatey;

        // 4. Homebrew — path contains homebrew or Cellar
        if (exePath.Contains("/homebrew/", StringComparison.OrdinalIgnoreCase) ||
            exePath.Contains("/Cellar/", StringComparison.Ordinal))
            return InstallKind.Brew;

        // 5. Winget — check Windows Package Manager (path-based, then probe)
        if (OperatingSystem.IsWindows() &&
            exePath.Contains("WinGet", StringComparison.OrdinalIgnoreCase))
            return InstallKind.Winget;

        // 6. APT — check dpkg on Linux
        if (OperatingSystem.IsLinux() && await IsManagedByDpkgAsync(exePath, ct).ConfigureAwait(false))
            return InstallKind.Apt;

        // 7. Check if dotnet CLI is available and the tool is registered
        if (await IsDotnetToolRegisteredAsync(ct).ConfigureAwait(false))
            return InstallKind.DotnetTool;

        // 8. If it's a self-contained single-file binary → NativeBinary
        //    Otherwise → Unknown
        return IsSelfContained() ? InstallKind.NativeBinary : InstallKind.Unknown;
    }

    private static async Task<bool> IsManagedByDpkgAsync(string exePath, CancellationToken ct)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(
                "dpkg", $"-S {exePath}",
                timeout: TimeSpan.FromSeconds(3),
                cancellationToken: ct).ConfigureAwait(false);
            return result.Success;
        }
#pragma warning disable CA1031
        catch { return false; }
#pragma warning restore CA1031
    }

    private static async Task<bool> IsDotnetToolRegisteredAsync(CancellationToken ct)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(
                "dotnet", "tool list -g",
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken: ct).ConfigureAwait(false);
            return result.Success &&
                   result.StandardOutput.Contains("jd.ai", StringComparison.OrdinalIgnoreCase);
        }
#pragma warning disable CA1031
        catch { return false; }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Heuristic: self-contained apps bundle the runtime, so the base directory
    /// contains a <c>coreclr</c> library or the executable is large (>30 MB single-file).
    /// </summary>
    private static bool IsSelfContained()
    {
        var baseDir = AppContext.BaseDirectory;

        // Single-file self-contained: the entry assembly is extracted to a temp dir,
        // but Environment.ProcessPath points to the original large binary.
        var processPath = Environment.ProcessPath;
        if (processPath is not null)
        {
            try
            {
                var size = new FileInfo(processPath).Length;
                if (size > 30 * 1024 * 1024) // > 30 MB likely self-contained single-file
                    return true;
            }
#pragma warning disable CA1031
            catch { /* ignore */ }
#pragma warning restore CA1031
        }

        // Framework-dependent apps have a host but no bundled runtime
        var coreclr = OperatingSystem.IsWindows() ? "coreclr.dll"
            : OperatingSystem.IsMacOS() ? "libcoreclr.dylib"
            : "libcoreclr.so";
        return File.Exists(Path.Combine(baseDir, coreclr));
    }
}
