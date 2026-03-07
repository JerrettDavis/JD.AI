using System.Diagnostics;
using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class GitToolsSteps
{
    private readonly ScenarioContext _context;

    public GitToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a temporary git repository")]
    public void GivenATemporaryGitRepository()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        RunGit("init -b main", dir);
        RunGit("config user.email \"test@test.com\"", dir);
        RunGit("config user.name \"Test\"", dir);
        // Create initial commit so we have a valid HEAD
        File.WriteAllText(Path.Combine(dir, ".gitkeep"), "");
        RunGit("add -A", dir);
        RunGit("commit -m \"Initial commit\"", dir);
        _context.Set(dir, "GitDir");
    }

    [Given(@"a tracked file ""(.*)"" with content ""(.*)""")]
    public void GivenATrackedFileWithContent(string fileName, string content)
    {
        var dir = _context.Get<string>("GitDir");
        File.WriteAllText(Path.Combine(dir, fileName), content);
        RunGit("add -A", dir);
        RunGit($"commit -m \"Add {fileName}\"", dir);
    }

    [Given(@"the file ""(.*)"" is modified to ""(.*)""")]
    public void GivenTheFileIsModifiedTo(string fileName, string content)
    {
        var dir = _context.Get<string>("GitDir");
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    [When(@"I run git status")]
    public async Task WhenIRunGitStatus()
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitStatusAsync(dir);
        _context.Set(result, "Result");
    }

    [When(@"I run git diff")]
    public async Task WhenIRunGitDiff()
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitDiffAsync(path: dir);
        _context.Set(result, "Result");
    }

    [When(@"I run git log with count (\d+)")]
    public async Task WhenIRunGitLogWithCount(int count)
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitLogAsync(count, dir);
        _context.Set(result, "Result");
    }

    [When(@"I commit with message ""(.*)""")]
    public async Task WhenICommitWithMessage(string message)
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitCommitAsync(message, dir);
        _context.Set(result, "Result");
    }

    [When(@"I list branches")]
    public async Task WhenIListBranches()
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitBranchAsync(path: dir);
        _context.Set(result, "Result");
    }

    [When(@"I create branch ""(.*)""")]
    public async Task WhenICreateBranch(string name)
    {
        var dir = _context.Get<string>("GitDir");
        var result = await GitTools.GitBranchAsync(name, path: dir);
        _context.Set(result, "Result");
    }

    private static void RunGit(string args, string workDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_context.TryGetValue("GitDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
