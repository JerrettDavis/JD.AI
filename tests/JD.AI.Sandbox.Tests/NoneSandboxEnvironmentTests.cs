using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Tests;

public sealed class NoneSandboxEnvironmentTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), $"jdai-none-sandbox-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAsync_UsesConfiguredWorkingDirectory()
    {
        Directory.CreateDirectory(_workingDirectory);
        var sandbox = new NoneSandbox(new SandboxPolicy
        {
            Name = "wd",
            WorkingDirectory = _workingDirectory
        });

        var result = await sandbox.RunAsync(GetShellExecutable(), GetPrintWorkingDirectoryArguments());

        Assert.True(result.Success);
        Assert.Contains(_workingDirectory, result.StandardOutput.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_AppliesConfiguredEnvironmentVariables()
    {
        var sandbox = new NoneSandbox(new SandboxPolicy
        {
            Name = "env",
            EnvironmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["JDAI_SANDBOX_TEST_ENV"] = "from-policy"
            }
        });

        var result = await sandbox.RunAsync(GetShellExecutable(), GetReadEnvironmentVariableArguments("JDAI_SANDBOX_TEST_ENV"));

        Assert.True(result.Success);
        Assert.Contains("from-policy", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_workingDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort temp cleanup only.
        }
    }

    private static string GetShellExecutable()
        => OperatingSystem.IsWindows() ? "cmd" : "/bin/sh";

    private static string GetPrintWorkingDirectoryArguments()
        => OperatingSystem.IsWindows() ? "/c cd" : "-c pwd";

    private static string GetReadEnvironmentVariableArguments(string variableName)
        => OperatingSystem.IsWindows()
            ? $"/c echo %{variableName}%"
            : $"-c \"printenv {variableName}\"";
}
