using JD.AI.Tui.Tools;

namespace JD.AI.Tui.Tests;

public sealed class SearchToolsTests : IDisposable
{
    private readonly string _tempDir;

    public SearchToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create test files
        File.WriteAllText(Path.Combine(_tempDir, "hello.cs"), "public class Hello\n{\n    public void Greet() {}\n}");
        File.WriteAllText(Path.Combine(_tempDir, "world.cs"), "public class World\n{\n    public void Run() {}\n}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "sub", "nested.txt"), "This is a nested file\nwith multiple lines");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Grep_FindsMatchingLines()
    {
        var result = SearchTools.Grep("class", _tempDir);

        Assert.Contains("match", result);
        Assert.Contains("class Hello", result);
        Assert.Contains("class World", result);
    }

    [Fact]
    public void Grep_RespectsGlobFilter()
    {
        var result = SearchTools.Grep("public", _tempDir, glob: "*.txt");

        Assert.Equal("No matches found.", result);
    }

    [Fact]
    public void Grep_SupportsCaseInsensitive()
    {
        var result = SearchTools.Grep("HELLO", _tempDir, ignoreCase: true);

        Assert.Contains("class Hello", result);
    }

    [Fact]
    public void Grep_ReportsNoMatches()
    {
        var result = SearchTools.Grep("nonexistent_pattern_xyz", _tempDir);

        Assert.Equal("No matches found.", result);
    }

    [Fact]
    public void Grep_RespectsMaxResults()
    {
        var result = SearchTools.Grep("public", _tempDir, maxResults: 1);

        Assert.Contains("1 match", result);
    }

    [Fact]
    public void Grep_IncludesContextLines()
    {
        var result = SearchTools.Grep("Greet", _tempDir, context: 1);

        // Should include lines before and after
        Assert.Contains("Greet", result);
        Assert.Contains("---", result); // Context separator
    }

    [Fact]
    public void Grep_HandlesInvalidRegex()
    {
        var result = SearchTools.Grep("[invalid(", _tempDir);

        Assert.Contains("Error", result);
    }

    [Fact]
    public void Grep_ReturnsErrorForMissingDir()
    {
        var result = SearchTools.Grep("test", Path.Combine(_tempDir, "nonexistent"));

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void Glob_FindsMatchingFiles()
    {
        var result = SearchTools.Glob("*.cs", _tempDir);

        Assert.Contains("hello.cs", result);
        Assert.Contains("world.cs", result);
        Assert.DoesNotContain("nested.txt", result);
    }

    [Fact]
    public void Glob_SupportsRecursivePattern()
    {
        var result = SearchTools.Glob("**/*.txt", _tempDir);

        Assert.Contains("nested.txt", result);
    }

    [Fact]
    public void Glob_ReportsNoMatches()
    {
        var result = SearchTools.Glob("*.xyz", _tempDir);

        Assert.Equal("No files found.", result);
    }

    [Fact]
    public void Glob_ReturnsErrorForMissingDir()
    {
        var result = SearchTools.Glob("*", Path.Combine(_tempDir, "nonexistent"));

        Assert.StartsWith("Error:", result);
    }
}
