using JD.AI.Tui.Tools;

namespace JD.AI.Tui.Tests;

public sealed class GitToolsTests : IDisposable
{
    private readonly string _tempDir;

    public GitToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        InitGitRepo();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            // Git objects can be read-only
            foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void InitGitRepo()
    {
        RunGit("init");
        RunGit("config user.email test@test.com");
        RunGit("config user.name Test");
        File.WriteAllText(Path.Combine(_tempDir, "initial.txt"), "initial content");
        RunGit("add -A");
        RunGit("commit -m \"initial commit\"");
    }

    private void RunGit(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = _tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
    }

    [Fact]
    public async Task GitStatus_ShowsCleanRepo()
    {
        var result = await GitTools.GitStatusAsync(_tempDir);

        Assert.Equal("(no output)", result);
    }

    [Fact]
    public async Task GitStatus_ShowsModifiedFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial.txt"), "modified");

        var result = await GitTools.GitStatusAsync(_tempDir);

        Assert.Contains("initial.txt", result);
    }

    [Fact]
    public async Task GitStatus_ShowsUntrackedFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "new.txt"), "new");

        var result = await GitTools.GitStatusAsync(_tempDir);

        Assert.Contains("new.txt", result);
    }

    [Fact]
    public async Task GitDiff_ShowsUnstagedChanges()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial.txt"), "modified content");

        var result = await GitTools.GitDiffAsync(path: _tempDir);

        Assert.Contains("modified content", result);
    }

    [Fact]
    public async Task GitDiff_ShowsStagedChanges()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial.txt"), "staged content");
        RunGit("add -A");

        var result = await GitTools.GitDiffAsync(target: "--staged", path: _tempDir);

        Assert.Contains("staged content", result);
    }

    [Fact]
    public async Task GitLog_ReturnsCommitHistory()
    {
        var result = await GitTools.GitLogAsync(count: 5, path: _tempDir);

        Assert.Contains("initial commit", result);
    }

    [Fact]
    public async Task GitCommit_StagesAndCommits()
    {
        File.WriteAllText(Path.Combine(_tempDir, "new.txt"), "new file");

        var result = await GitTools.GitCommitAsync("test commit", _tempDir);

        Assert.Contains("test commit", result);

        var log = await GitTools.GitLogAsync(count: 1, path: _tempDir);
        Assert.Contains("test commit", log);
    }
}
