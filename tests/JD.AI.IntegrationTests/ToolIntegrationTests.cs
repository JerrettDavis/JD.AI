using JD.AI.Tui.Tools;
using Xunit;

namespace JD.AI.Tui.IntegrationTests;

/// <summary>
/// Integration tests that validate tools work against real filesystem and shell.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ToolIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public ToolIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-tool-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [SkippableFact]
    public void FileTools_FullWorkflow_CreateReadEditList()
    {
        TuiIntegrationGuard.EnsureEnabled();

        // Write
        var writePath = Path.Combine(_tempDir, "workflow.txt");
        var writeResult = FileTools.WriteFile(writePath, "Hello World\nSecond Line");
        Assert.Contains("Wrote", writeResult);

        // Read
        var readResult = FileTools.ReadFile(writePath);
        Assert.Contains("1. Hello World", readResult);
        Assert.Contains("2. Second Line", readResult);

        // Edit
        var editResult = FileTools.EditFile(writePath, "Hello World", "Hi World");
        Assert.Contains("Replaced", editResult);

        // Verify edit
        var verifyResult = FileTools.ReadFile(writePath);
        Assert.Contains("Hi World", verifyResult);
        Assert.DoesNotContain("Hello World", verifyResult);

        // List
        var listResult = FileTools.ListDirectory(_tempDir);
        Assert.Contains("workflow.txt", listResult);
    }

    [SkippableFact]
    public void SearchTools_GrepAndGlob_RealFilesystem()
    {
        TuiIntegrationGuard.EnsureEnabled();

        // Create test files
        File.WriteAllText(Path.Combine(_tempDir, "app.cs"), "public class App { }");
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "public class TestApp { }");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "sub", "readme.md"), "# README\nThis is a test");

        // Grep
        var grepResult = SearchTools.Grep("class", _tempDir, glob: "*.cs");
        Assert.Contains("App", grepResult);
        Assert.Contains("TestApp", grepResult);

        // Glob
        var globResult = SearchTools.Glob("**/*.md", _tempDir);
        Assert.Contains("readme.md", globResult);
    }

    [SkippableFact]
    public async Task ShellTools_ExecutesRealCommands()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var cmd = OperatingSystem.IsWindows() ? "echo integration-test" : "echo integration-test";
        var result = await ShellTools.RunCommandAsync(cmd, cwd: _tempDir);

        Assert.Contains("Exit code: 0", result);
        Assert.Contains("integration-test", result);
    }

    [SkippableFact]
    public async Task GitTools_FullWorkflow_InitCommitStatusDiffLog()
    {
        TuiIntegrationGuard.EnsureEnabled();

        // Init repo
        await ShellTools.RunCommandAsync("git init", cwd: _tempDir);
        await ShellTools.RunCommandAsync("git config user.email test@test.com", cwd: _tempDir);
        await ShellTools.RunCommandAsync("git config user.name Test", cwd: _tempDir);

        // Create and commit a file
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "initial");
        await GitTools.GitCommitAsync("initial commit", _tempDir);

        // Verify log
        var log = await GitTools.GitLogAsync(count: 5, path: _tempDir);
        Assert.Contains("initial commit", log);

        // Modify and check status
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "modified");
        var status = await GitTools.GitStatusAsync(_tempDir);
        Assert.Contains("test.txt", status);

        // Check diff
        var diff = await GitTools.GitDiffAsync(path: _tempDir);
        Assert.Contains("modified", diff);
    }

    [SkippableFact]
    public async Task WebTools_FetchesRealUrl()
    {
        TuiIntegrationGuard.EnsureEnabled();

        // WebTools uses HttpClient which doesn't support file:// URIs.
        // Test with a simple HTTP request to a known endpoint (Ollama's API).
        var ollamaAvailable = await TuiIntegrationGuard.IsOllamaAvailableAsync().ConfigureAwait(false);
        Skip.IfNot(ollamaAvailable, "Need a local HTTP server to test web_fetch");

        var result = await WebTools.WebFetchAsync("http://localhost:11434").ConfigureAwait(false);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.DoesNotContain("Error", result);
    }
}
