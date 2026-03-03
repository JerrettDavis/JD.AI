using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class BatchEditToolsTests : IDisposable
{
    private readonly string _tempDir;

    public BatchEditToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-batch-{Guid.NewGuid():N}");
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
    public void BatchEdit_SingleFile_AppliesEdit()
    {
        var file = Path.Combine(_tempDir, "single.cs");
        File.WriteAllText(file, "var x = 1;\nvar y = 2;\n");
        var escaped = EscapePath(file);

        var json = "[{\"path\":\"" + escaped + "\",\"oldText\":\"var x = 1;\",\"newText\":\"var x = 42;\"}]";

        var result = BatchEditTools.BatchEditFiles(json);

        Assert.Contains("Applied 1 edit(s)", result);
        Assert.Contains("var x = 42;", File.ReadAllText(file));
    }

    [Fact]
    public void BatchEdit_MultipleEditsInSameFile_AppliesSequentially()
    {
        var file = Path.Combine(_tempDir, "multi.cs");
        File.WriteAllText(file, "class Foo\n{\n    int a;\n    int b;\n}\n");
        var escaped = EscapePath(file);

        var json = "[{\"path\":\"" + escaped + "\",\"oldText\":\"int a;\",\"newText\":\"int alpha;\"},"
                 + "{\"path\":\"" + escaped + "\",\"oldText\":\"int b;\",\"newText\":\"int beta;\"}]";

        var result = BatchEditTools.BatchEditFiles(json);

        Assert.Contains("2 edit(s)", result);
        var content = File.ReadAllText(file);
        Assert.Contains("int alpha;", content);
        Assert.Contains("int beta;", content);
    }

    [Fact]
    public void BatchEdit_AcrossMultipleFiles_AppliesAll()
    {
        var file1 = Path.Combine(_tempDir, "f1.cs");
        var file2 = Path.Combine(_tempDir, "f2.cs");
        File.WriteAllText(file1, "aaa");
        File.WriteAllText(file2, "bbb");

        var json = "[{\"path\":\"" + EscapePath(file1) + "\",\"oldText\":\"aaa\",\"newText\":\"AAA\"},"
                 + "{\"path\":\"" + EscapePath(file2) + "\",\"oldText\":\"bbb\",\"newText\":\"BBB\"}]";

        var result = BatchEditTools.BatchEditFiles(json);

        Assert.Contains("2 file(s)", result);
        Assert.Equal("AAA", File.ReadAllText(file1));
        Assert.Equal("BBB", File.ReadAllText(file2));
    }

    [Fact]
    public void BatchEdit_OldTextNotFound_AbortsAll()
    {
        var file1 = Path.Combine(_tempDir, "ok.cs");
        var file2 = Path.Combine(_tempDir, "bad.cs");
        File.WriteAllText(file1, "good content");
        File.WriteAllText(file2, "other content");

        var json = "[{\"path\":\"" + EscapePath(file1) + "\",\"oldText\":\"good content\",\"newText\":\"modified\"},"
                 + "{\"path\":\"" + EscapePath(file2) + "\",\"oldText\":\"MISSING\",\"newText\":\"replaced\"}]";

        var result = BatchEditTools.BatchEditFiles(json);

        Assert.Contains("Error", result);
        Assert.Contains("not found", result);
        Assert.Equal("good content", File.ReadAllText(file1));
        Assert.Equal("other content", File.ReadAllText(file2));
    }

    [Fact]
    public void BatchEdit_EmptyEdits_ReturnsMessage()
    {
        var result = BatchEditTools.BatchEditFiles("[]");

        Assert.Equal("No edits provided.", result);
    }

    [Fact]
    public void BatchEdit_MissingFile_ReturnsError()
    {
        var json = "[{\"path\":\"nonexistent.cs\",\"oldText\":\"x\",\"newText\":\"y\"}]";

        var result = BatchEditTools.BatchEditFiles(json);

        Assert.Contains("file not found", result);
    }
}
