using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class OpenClawCompatibilityToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TaskTools _tasks = new();
    private readonly WebSearchTools _webSearch = new("test-key");
    private readonly OpenClawCompatibilityTools _tools;

    public OpenClawCompatibilityToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-compat-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tools = new OpenClawCompatibilityTools(_tasks, _webSearch);
    }

    public void Dispose()
    {
        _webSearch.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Read_NoContextTrue_HidesOutput()
    {
        var path = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(path, "super-secret-content");

        var result = _tools.Read(path, noContext: true);

        Assert.DoesNotContain("super-secret-content", result, StringComparison.Ordinal);
        Assert.Contains("noContext=true", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_SummaryTrue_ReturnsCompactOutput()
    {
        var path = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(path, string.Join(Environment.NewLine, Enumerable.Range(1, 20).Select(i => $"line-{i}")));

        var result = _tools.Read(path, summary: true);
        var lineCount = result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(lineCount <= 8, $"Expected <= 8 lines, got {lineCount}");
        Assert.Contains("line-1", result, StringComparison.Ordinal);
        Assert.DoesNotContain("line-20", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MaxResultChars_TruncatesOutput()
    {
        var path = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(path, new string('x', 500));

        var result = _tools.Read(path, maxResultChars: 80);

        Assert.Contains("[truncated", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Length < 200, $"Expected compact output, got {result.Length} chars.");
    }

    [Fact]
    public void TodoWrite_AndTodoRead_ShareTaskState()
    {
        var create = _tools.TodoWrite("create", title: "implement parity", description: "ship issue 65");
        var list = _tools.TodoRead();

        Assert.Contains("Created task task-1", create, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task-1", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("implement parity", list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bash_UsesRunCommandAlias()
    {
        var command = "echo hello";

        var result = await _tools.BashAsync(command);

        Assert.Contains("Exit code:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hello", result, StringComparison.OrdinalIgnoreCase);
    }
}
