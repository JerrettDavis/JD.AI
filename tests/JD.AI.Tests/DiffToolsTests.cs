using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class DiffToolsTests : IDisposable
{
    private readonly string _tempDir;

    public DiffToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static string EscapePath(string path) => path.Replace("\\", "\\\\");

    [Fact]
    public void CreatePatch_WithValidEdits_ReturnsDiff()
    {
        var file = Path.Combine(_tempDir, "test.cs");
        File.WriteAllText(file, "int x = 1;\nint y = 2;\n");

        var json = "[{\"path\":\"" + EscapePath(file) + "\",\"oldText\":\"int x = 1;\",\"newText\":\"int x = 42;\"}]";

        var result = DiffTools.CreatePatch(json);

        Assert.Contains("---", result);
        Assert.Contains("+++", result);
        Assert.Contains("-int x = 1;", result);
        Assert.Contains("+int x = 42;", result);
    }

    [Fact]
    public void CreatePatch_MissingFile_ReportsSkipped()
    {
        var json = "[{\"path\":\"nonexistent.cs\",\"oldText\":\"x\",\"newText\":\"y\"}]";

        var result = DiffTools.CreatePatch(json);

        Assert.Contains("file not found", result);
    }

    [Fact]
    public void CreatePatch_NoEdits_ReturnsMessage()
    {
        var result = DiffTools.CreatePatch("[]");

        Assert.Equal("No edits provided.", result);
    }

    [Fact]
    public void ApplyPatch_WithValidEdits_ModifiesFiles()
    {
        var file = Path.Combine(_tempDir, "apply.cs");
        File.WriteAllText(file, "var name = \"old\";\n");

        var json = "[{\"path\":\"" + EscapePath(file) + "\",\"oldText\":\"\\\"old\\\"\",\"newText\":\"\\\"new\\\"\"}]";

        var result = DiffTools.ApplyPatch(json);

        Assert.Contains("Applied 1 edit(s)", result);
        Assert.Contains("\"new\"", File.ReadAllText(file));
    }

    [Fact]
    public void ApplyPatch_OldTextNotFound_AbortsAll()
    {
        var file = Path.Combine(_tempDir, "abort.cs");
        File.WriteAllText(file, "original content\n");

        var json = "[{\"path\":\"" + EscapePath(file) + "\",\"oldText\":\"missing text\",\"newText\":\"replacement\"}]";

        var result = DiffTools.ApplyPatch(json);

        Assert.Contains("Error", result);
        Assert.Contains("not found", result);
        Assert.Equal("original content\n", File.ReadAllText(file));
    }

    [Fact]
    public void ApplyPatch_MultipleEdits_AllApplied()
    {
        var file1 = Path.Combine(_tempDir, "multi1.cs");
        var file2 = Path.Combine(_tempDir, "multi2.cs");
        File.WriteAllText(file1, "aaa");
        File.WriteAllText(file2, "bbb");

        var json = "[{\"path\":\"" + EscapePath(file1) + "\",\"oldText\":\"aaa\",\"newText\":\"AAA\"},"
                 + "{\"path\":\"" + EscapePath(file2) + "\",\"oldText\":\"bbb\",\"newText\":\"BBB\"}]";

        var result = DiffTools.ApplyPatch(json);

        Assert.Contains("2 edit(s)", result);
        Assert.Equal("AAA", File.ReadAllText(file1));
        Assert.Equal("BBB", File.ReadAllText(file2));
    }
}
