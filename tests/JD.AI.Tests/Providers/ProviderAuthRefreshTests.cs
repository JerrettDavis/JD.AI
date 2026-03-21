using System.Reflection;
using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers;

[Collection("CliRefresh")]
public sealed class ProviderAuthRefreshTests : IDisposable
{
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string? _originalPath = Environment.GetEnvironmentVariable("PATH");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        _fixture.Dispose();
    }

    [Fact]
    public async Task ClaudeCodeRefresh_InvokesClaudeCli()
    {
        var logPath = _fixture.GetPath("claude.log");
        var script = OperatingSystem.IsWindows()
            ? $"""
              @echo off
              echo claude %*>>"{logPath}"
              exit /b 0
              """
            : $"""
              #!/bin/sh
              echo "claude $*" >> "{logPath}"
              exit 0
              """;
        CreateCliShim("claude", script);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(ClaudeCodeDetector));

        refreshed.Should().BeTrue();
        (await File.ReadAllTextAsync(logPath)).Should().Contain("claude --version");
    }

    [Fact]
    public async Task CopilotRefresh_InvokesGhCli()
    {
        var logPath = _fixture.GetPath("gh.log");
        var script = OperatingSystem.IsWindows()
            ? $"""
              @echo off
              echo gh %*>>"{logPath}"
              exit /b 0
              """
            : $"""
              #!/bin/sh
              echo "gh $*" >> "{logPath}"
              exit 0
              """;
        CreateCliShim("gh", script);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(CopilotDetector));

        refreshed.Should().BeTrue();
        (await File.ReadAllTextAsync(logPath)).Should().Contain("gh auth status");
    }

    [Fact]
    public async Task OpenAICodexRefresh_FallsBackToVersionProbeWhenStatusFails()
    {
        var logPath = _fixture.GetPath("codex.log");
        var script = OperatingSystem.IsWindows()
            ? $"""
              @echo off
              echo codex %*>>"{logPath}"
              if /I "%~1 %~2"=="login status" exit /b 1
              exit /b 0
              """
            : $"""
              #!/bin/sh
              echo "codex $*" >> "{logPath}"
              if [ "$1 $2" = "login status" ]; then
                exit 1
              fi
              exit 0
              """;
        CreateCliShim("codex", script);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(OpenAICodexDetector));

        refreshed.Should().BeTrue();
        var log = await File.ReadAllTextAsync(logPath);
        log.Should().Contain("codex login status");
        log.Should().Contain("codex --version");
    }

    private void CreateCliShim(string toolName, string scriptBody)
    {
        var normalized = string.Join(
            Environment.NewLine,
            scriptBody
                .Trim()
                .Split(LineSeparators, StringSplitOptions.None)
                .Select(line => line.TrimStart()));

        if (OperatingSystem.IsWindows())
        {
            var shimPath = _fixture.GetPath($"{toolName}.cmd");
            File.WriteAllText(shimPath, normalized);
            return;
        }

        var unixShim = _fixture.GetPath(toolName);
        File.WriteAllText(unixShim, normalized);
        TryMakeExecutable(unixShim);
    }

    private static void TryMakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
        catch
        {
            // Best effort for environments that do not support Unix mode APIs.
        }
    }

    private DisposableAction PushPathPrefix(string directory)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        var updated = string.IsNullOrWhiteSpace(path)
            ? directory
            : string.Join(Path.PathSeparator, directory, path);
        Environment.SetEnvironmentVariable("PATH", updated);
        return new DisposableAction(() => Environment.SetEnvironmentVariable("PATH", path));
    }

    private static async Task<bool> InvokeRefreshAsync(Type detectorType)
    {
        var method = detectorType.GetMethod(
            "TryRefreshAuthAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull($"because {detectorType.Name} should expose a private refresh helper");

        var task = (Task<bool>)method!.Invoke(null, [CancellationToken.None])!;
        return await task.ConfigureAwait(false);
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _dispose;

        public DisposableAction(Action dispose) => _dispose = dispose;

        public void Dispose() => _dispose();
    }
}
