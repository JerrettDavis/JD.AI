using FluentAssertions;
using JD.AI.Core.Agents.Checkpointing;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class CheckpointingSteps
{
    private readonly ScenarioContext _context;

    public CheckpointingSteps(ScenarioContext context) => _context = context;

    [Given(@"a stash checkpoint strategy for a git working directory")]
    public void GivenAStashCheckpointStrategyForAGitWorkingDirectory()
    {
        // Use the actual repo root as the working directory
        var workingDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var strategy = new StashCheckpointStrategy(workingDir);
        _context.Set<ICheckpointStrategy>(strategy);
        _context.Set(workingDir, "workingDir");
    }

    [Given(@"a stash checkpoint strategy for a clean git working directory")]
    public void GivenAStashCheckpointStrategyForACleanGitWorkingDirectory()
    {
        // For a clean directory we use a temp dir initialized as a git repo
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        RunGit(tempDir, "init");
        RunGit(tempDir, "commit --allow-empty -m \"init\"");

        var strategy = new StashCheckpointStrategy(tempDir);
        _context.Set<ICheckpointStrategy>(strategy);
        _context.Set(tempDir, "workingDir");
        _context.Set(true, "cleanupTempDir");
    }

    [Given(@"the working directory has uncommitted changes")]
    public void GivenTheWorkingDirectoryHasUncommittedChanges()
    {
        var workingDir = _context.Get<string>("workingDir");
        var testFile = Path.Combine(workingDir, $"__test_checkpoint_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(testFile, "test change for checkpoint");
        _context.Set(testFile, "tempTestFile");
    }

    [Given(@"a directory checkpoint strategy for a temporary directory")]
    public void GivenADirectoryCheckpointStrategyForATemporaryDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-cp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var strategy = new DirectoryCheckpointStrategy(tempDir);
        _context.Set<ICheckpointStrategy>(strategy);
        _context.Set(tempDir, "workingDir");
        _context.Set(true, "cleanupTempDir");
    }

    [Given(@"the working directory contains source files")]
    public void GivenTheWorkingDirectoryContainsSourceFiles()
    {
        var workingDir = _context.Get<string>("workingDir");
        File.WriteAllText(Path.Combine(workingDir, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(workingDir, "Helper.cs"), "class Helper {}");
    }

    [Given(@"the working directory contains a file ""(.*)"" with content ""(.*)""")]
    public void GivenTheWorkingDirectoryContainsAFileWithContent(string filename, string content)
    {
        var workingDir = _context.Get<string>("workingDir");
        File.WriteAllText(Path.Combine(workingDir, filename), content);
    }

    [Given(@"a checkpoint exists with label ""(.*)""")]
    public async Task GivenACheckpointExistsWithLabel(string label)
    {
        // DirectoryCheckpointStrategy uses second-precision timestamps for IDs,
        // so ensure consecutive checkpoints get different timestamps.
        if (_context.TryGetValue<List<string>>("checkpointIds", out var existingIds) && existingIds.Count > 0)
        {
            await Task.Delay(1100);
        }

        var strategy = _context.Get<ICheckpointStrategy>();
        var id = await strategy.CreateAsync(label);
        if (!_context.TryGetValue<List<string>>("checkpointIds", out var ids))
        {
            ids = [];
            _context.Set(ids, "checkpointIds");
        }
        if (id != null) ids.Add(id);
    }

    [Given(@"the working directory file ""(.*)"" is modified to ""(.*)""")]
    public void GivenTheWorkingDirectoryFileIsModifiedTo(string filename, string content)
    {
        var workingDir = _context.Get<string>("workingDir");
        File.WriteAllText(Path.Combine(workingDir, filename), content);
    }

    [Given(@"a commit checkpoint strategy for a git working directory")]
    public void GivenACommitCheckpointStrategyForAGitWorkingDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-commit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        RunGit(tempDir, "init");
        RunGit(tempDir, "commit --allow-empty -m \"init\"");

        var strategy = new CommitCheckpointStrategy(tempDir);
        _context.Set<ICheckpointStrategy>(strategy);
        _context.Set(tempDir, "workingDir");
        _context.Set(true, "cleanupTempDir");
    }

    [When(@"a checkpoint is created with label ""(.*)""")]
    public async Task WhenACheckpointIsCreatedWithLabel(string label)
    {
        var strategy = _context.Get<ICheckpointStrategy>();
        var id = await strategy.CreateAsync(label);
        _context.Set(id, "checkpointId");
    }

    [When(@"the checkpoints are listed")]
    public async Task WhenTheCheckpointsAreListed()
    {
        var strategy = _context.Get<ICheckpointStrategy>();
        var list = await strategy.ListAsync();
        _context.Set(list, "checkpointList");
    }

    [When(@"the checkpoint ""(.*)"" is restored")]
    public async Task WhenTheCheckpointIsRestored(string label)
    {
        var strategy = _context.Get<ICheckpointStrategy>();
        var ids = _context.Get<List<string>>("checkpointIds");
        var id = ids.First(); // Use the first checkpoint
        var result = await strategy.RestoreAsync(id);
        _context.Set(result, "restoreResult");
    }

    [When(@"all checkpoints are cleared")]
    public async Task WhenAllCheckpointsAreCleared()
    {
        var strategy = _context.Get<ICheckpointStrategy>();
        await strategy.ClearAsync();
    }

    [Then(@"the checkpoint ID should contain ""(.*)""")]
    public void ThenTheCheckpointIdShouldContain(string expected)
    {
        var id = _context.Get<string?>("checkpointId");
        id.Should().NotBeNull();
        id.Should().Contain(expected);
    }

    [Then(@"the checkpoint ID should be null")]
    public void ThenTheCheckpointIdShouldBeNull()
    {
        var id = _context.Get<string?>("checkpointId");
        id.Should().BeNull();
    }

    [Then(@"the checkpoint ID should not be null")]
    public void ThenTheCheckpointIdShouldNotBeNull()
    {
        var id = _context.Get<string?>("checkpointId");
        id.Should().NotBeNull();
    }

    [Then(@"the checkpoint directory should contain the source files")]
    public void ThenTheCheckpointDirectoryShouldContainTheSourceFiles()
    {
        var workingDir = _context.Get<string>("workingDir");
        var checkpointRoot = Path.Combine(workingDir, ".jdai", "checkpoints");
        Directory.Exists(checkpointRoot).Should().BeTrue();
        var cpDirs = Directory.GetDirectories(checkpointRoot);
        cpDirs.Should().NotBeEmpty();
        var latestCp = cpDirs.OrderDescending().First();
        File.Exists(Path.Combine(latestCp, "Program.cs")).Should().BeTrue();
    }

    [Then(@"the checkpoint list should contain (\d+) entries")]
    public void ThenTheCheckpointListShouldContainEntries(int count)
    {
        var list = _context.Get<IReadOnlyList<CheckpointInfo>>("checkpointList");
        list.Should().HaveCount(count);
    }

    [Then(@"the file ""(.*)"" should contain ""(.*)""")]
    public void ThenTheFileShouldContain(string filename, string expected)
    {
        var workingDir = _context.Get<string>("workingDir");
        var content = File.ReadAllText(Path.Combine(workingDir, filename));
        content.Should().Contain(expected);
    }

    [Then(@"the checkpoints list should be empty")]
    public async Task ThenTheCheckpointsListShouldBeEmpty()
    {
        var strategy = _context.Get<ICheckpointStrategy>();
        var list = await strategy.ListAsync();
        list.Should().BeEmpty();
    }

    [AfterScenario("@checkpointing")]
    public void Cleanup()
    {
        // Clean up temp test file if created in main repo
        if (_context.TryGetValue<string>("tempTestFile", out var tempFile) && File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        // Clean up temp directories
        if (_context.TryGetValue<bool>("cleanupTempDir", out var cleanup) && cleanup
            && _context.TryGetValue<string>("workingDir", out var dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort cleanup */ }
        }
    }

    private static void RunGit(string workingDir, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit(10_000);
    }
}
