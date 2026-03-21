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
        CreateCliShim(
            "claude",
            $"""
            @echo off
            echo claude %*>>"{logPath}"
            exit /b 0
            """);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(ClaudeCodeDetector));

        refreshed.Should().BeTrue();
        (await File.ReadAllTextAsync(logPath)).Should().Contain("claude --version");
    }

    [Fact]
    public async Task CopilotRefresh_InvokesGhCli()
    {
        var logPath = _fixture.GetPath("gh.log");
        CreateCliShim(
            "gh",
            $"""
            @echo off
            echo gh %*>>"{logPath}"
            exit /b 0
            """);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(CopilotDetector));

        refreshed.Should().BeTrue();
        (await File.ReadAllTextAsync(logPath)).Should().Contain("gh auth status");
    }

    [Fact]
    public async Task OpenAICodexRefresh_FallsBackToVersionProbeWhenStatusFails()
    {
        var logPath = _fixture.GetPath("codex.log");
        CreateCliShim(
            "codex",
            $"""
            @echo off
            echo codex %*>>"{logPath}"
            if /I "%~1 %~2"=="login status" exit /b 1
            exit /b 0
            """);

        using var _ = PushPathPrefix(_fixture.DirectoryPath);

        var refreshed = await InvokeRefreshAsync(typeof(OpenAICodexDetector));

        refreshed.Should().BeTrue();
        var log = await File.ReadAllTextAsync(logPath);
        log.Should().Contain("codex login status");
        log.Should().Contain("codex --version");
    }

    private void CreateCliShim(string toolName, string scriptBody)
    {
        var shimPath = _fixture.GetPath($"{toolName}.cmd");
        var normalized = string.Join(
            Environment.NewLine,
            scriptBody
                .Trim()
                .Split(LineSeparators, StringSplitOptions.None)
                .Select(line => line.TrimStart()));
        File.WriteAllText(shimPath, normalized);
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
