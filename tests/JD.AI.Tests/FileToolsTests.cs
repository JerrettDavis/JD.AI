using JD.AI.Tui.Tools;

namespace JD.AI.Tui.Tests;

public sealed class FileToolsTests : IDisposable
{
    private readonly string _tempDir;

    public FileToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadFile_ReturnsContentWithLineNumbers()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "line1\nline2\nline3");

        var result = FileTools.ReadFile(path);

        Assert.Contains("1. line1", result);
        Assert.Contains("2. line2", result);
        Assert.Contains("3. line3", result);
    }

    [Fact]
    public void ReadFile_SupportsLineRange()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "a\nb\nc\nd\ne");

        var result = FileTools.ReadFile(path, startLine: 2, endLine: 4);

        Assert.DoesNotContain("1. a", result);
        Assert.Contains("2. b", result);
        Assert.Contains("3. c", result);
        Assert.Contains("4. d", result);
        Assert.DoesNotContain("5. e", result);
    }

    [Fact]
    public void ReadFile_ReturnsErrorForMissingFile()
    {
        var result = FileTools.ReadFile(Path.Combine(_tempDir, "nonexistent.txt"));

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void WriteFile_CreatesFileAndDirectories()
    {
        var path = Path.Combine(_tempDir, "sub", "deep", "file.txt");

        var result = FileTools.WriteFile(path, "hello world");

        Assert.Contains("Wrote", result);
        Assert.True(File.Exists(path));
        Assert.Equal("hello world", File.ReadAllText(path));
    }

    [Fact]
    public void EditFile_ReplacesExactMatch()
    {
        var path = Path.Combine(_tempDir, "edit.txt");
        File.WriteAllText(path, "Hello World\nGoodbye World");

        var result = FileTools.EditFile(path, "Hello World", "Hi World");

        Assert.Contains("Replaced", result);
        Assert.Equal("Hi World\nGoodbye World", File.ReadAllText(path));
    }

    [Fact]
    public void EditFile_FailsOnAmbiguousMatch()
    {
        var path = Path.Combine(_tempDir, "edit.txt");
        File.WriteAllText(path, "foo bar\nfoo bar");

        var result = FileTools.EditFile(path, "foo bar", "baz");

        Assert.Contains("multiple locations", result);
    }

    [Fact]
    public void EditFile_FailsWhenNotFound()
    {
        var path = Path.Combine(_tempDir, "edit.txt");
        File.WriteAllText(path, "hello");

        var result = FileTools.EditFile(path, "nonexistent", "replacement");

        Assert.Contains("not found", result);
    }

    [Fact]
    public void ListDirectory_ReturnsTreeListing()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub1"));
        File.WriteAllText(Path.Combine(_tempDir, "file1.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "sub1", "file2.txt"), "");

        var result = FileTools.ListDirectory(_tempDir);

        Assert.Contains("file1.txt", result);
        Assert.Contains("sub1/", result);
        Assert.Contains("file2.txt", result);
    }

    [Fact]
    public void ListDirectory_RespectsMaxDepth()
    {
        var deep = Path.Combine(_tempDir, "a", "b", "c");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(deep, "deep.txt"), "");

        var result = FileTools.ListDirectory(_tempDir, maxDepth: 1);

        Assert.Contains("a/", result);
        Assert.Contains("b/", result);
        Assert.DoesNotContain("deep.txt", result);
    }

    [Fact]
    public void ListDirectory_SkipsHiddenAndSpecialDirs()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".hidden"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "bin"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "obj"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));

        var result = FileTools.ListDirectory(_tempDir);

        Assert.DoesNotContain(".hidden", result);
        Assert.DoesNotContain("bin/", result);
        Assert.DoesNotContain("obj/", result);
        Assert.DoesNotContain("node_modules/", result);
        Assert.Contains("src/", result);
    }

    [Fact]
    public void ListDirectory_ReturnsErrorForMissingDir()
    {
        var result = FileTools.ListDirectory(Path.Combine(_tempDir, "nonexistent"));

        Assert.StartsWith("Error:", result);
    }
}
