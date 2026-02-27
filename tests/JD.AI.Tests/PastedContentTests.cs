using JD.AI.Tui.Rendering;

namespace JD.AI.Tui.Tests;

public sealed class PastedContentTests : IDisposable
{
    public PastedContentTests()
    {
        PastedContent.ResetCounter();
    }

    public void Dispose()
    {
        PastedContent.ResetCounter();
    }

    [Fact]
    public void TextPaste_SingleLine_ShowsCharCount()
    {
        var paste = new PastedContent("Hello, this is a test paste content");
        Assert.Contains("Pasted content #", paste.Label);
        Assert.Contains("chars", paste.Label);
    }

    [Fact]
    public void TextPaste_MultiLine_ShowsLineCount()
    {
        var paste = new PastedContent("line1\nline2\nline3\nline4");
        Assert.Contains("Pasted content #", paste.Label);
        Assert.Contains("4 lines", paste.Label);
    }

    [Fact]
    public void ImagePaste_ShowsSize()
    {
        var paste = new PastedContent(new string('x', 1500), PasteKind.Image);
        Assert.Contains("Pasted Image #", paste.Label);
        Assert.Contains("KB", paste.Label);
    }

    [Fact]
    public void FilePaste_ShowsSize()
    {
        var paste = new PastedContent(new string('x', 2560), PasteKind.File);
        Assert.Contains("Pasted File #", paste.Label);
        Assert.Contains("KB", paste.Label);
    }

    [Fact]
    public void Chip_IncludesBrackets()
    {
        var paste = new PastedContent("test content here");
        Assert.StartsWith("[", paste.Chip);
        Assert.EndsWith("]", paste.Chip);
    }

    [Fact]
    public void Id_AutoIncrements()
    {
        var a = new PastedContent("first");
        var b = new PastedContent("second");
        Assert.True(b.Id > a.Id, "Second paste should have higher Id than first");
    }

    [Fact]
    public void FormatCharCount_SmallValues()
    {
        Assert.Equal("50 chars", PastedContent.FormatCharCount(50));
        Assert.Equal("999 chars", PastedContent.FormatCharCount(999));
    }

    [Fact]
    public void FormatCharCount_LargeValues()
    {
        Assert.Equal("1.5k chars", PastedContent.FormatCharCount(1500));
        Assert.Equal("15k chars", PastedContent.FormatCharCount(15000));
    }

    [Fact]
    public void FormatSize_Bytes()
    {
        Assert.Equal("500B", PastedContent.FormatSize(500));
    }

    [Fact]
    public void FormatSize_Kilobytes()
    {
        Assert.Equal("2.5KB", PastedContent.FormatSize(2560));
    }

    [Fact]
    public void FormatSize_Megabytes()
    {
        Assert.Equal("1.5MB", PastedContent.FormatSize(1572864));
    }
}

public sealed class InputResultTests
{
    [Fact]
    public void AssemblePrompt_NoAttachments_ReturnsTypedText()
    {
        var result = new InputResult { TypedText = "hello world" };
        Assert.Equal("hello world", result.AssemblePrompt());
    }

    [Fact]
    public void AssemblePrompt_WithAttachments_AppendsContent()
    {
        PastedContent.ResetCounter();
        var paste = new PastedContent("pasted code block\nline 2");
        var result = new InputResult
        {
            TypedText = "Review this:",
            Attachments = [paste],
        };

        var assembled = result.AssemblePrompt();
        Assert.StartsWith("Review this:", assembled);
        Assert.Contains("pasted code block", assembled);
        Assert.Contains("line 2", assembled);
    }

    [Fact]
    public void AssemblePrompt_MultipleAttachments_AppendsAll()
    {
        PastedContent.ResetCounter();
        var p1 = new PastedContent("first paste");
        var p2 = new PastedContent("second paste");
        var result = new InputResult
        {
            TypedText = "Compare:",
            Attachments = [p1, p2],
        };

        var assembled = result.AssemblePrompt();
        Assert.Contains("first paste", assembled);
        Assert.Contains("second paste", assembled);
    }

    [Fact]
    public void EmptyAttachments_DefaultsToEmpty()
    {
        var result = new InputResult { TypedText = "test" };
        Assert.Empty(result.Attachments);
    }
}
