// Licensed under the MIT License.

using FluentAssertions;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Tests.Infrastructure;

public sealed class OutputFormatterTests
{
    [Fact]
    public void Error_SimpleMessage_FormatsCorrectly()
    {
        OutputFormatter.Error("File not found").Should().Be("Error: File not found");
    }

    [Fact]
    public void Error_WithContext_FormatsCorrectly()
    {
        OutputFormatter.Error("read_file", "path does not exist")
            .Should().Be("Error: read_file: path does not exist");
    }

    [Fact]
    public void Error_FromException_IncludesMessage()
    {
        var ex = new InvalidOperationException("bad state");
        OutputFormatter.Error("operation", ex).Should().Be("Error: operation: bad state");
    }

    [Fact]
    public void CodeBlock_NoLanguage_FormatsPlain()
    {
        OutputFormatter.CodeBlock("hello").Should().Be("```\nhello\n```");
    }

    [Fact]
    public void CodeBlock_WithLanguage_IncludesLanguage()
    {
        OutputFormatter.CodeBlock("{\"key\":\"value\"}", "json")
            .Should().Be("```json\n{\"key\":\"value\"}\n```");
    }

    [Fact]
    public void JsonBlock_FormatsAsJson()
    {
        OutputFormatter.JsonBlock("{\"a\":1}").Should().Contain("```json");
    }

    [Fact]
    public void Success_FormatsWithEmoji()
    {
        OutputFormatter.Success("Done").Should().Be("✅ Done");
    }

    [Fact]
    public void Warning_FormatsWithEmoji()
    {
        OutputFormatter.Warning("Caution").Should().Be("⚠️ Caution");
    }

    [Fact]
    public void Truncate_ShortText_ReturnsUnchanged()
    {
        OutputFormatter.Truncate("hello", 100).Should().Be("hello");
    }

    [Fact]
    public void Truncate_LongText_TruncatesWithNotice()
    {
        var text = new string('x', 200);
        var result = OutputFormatter.Truncate(text, 100);

        result.Should().HaveLength(100 + "\n...[truncated, 100 chars remaining]".Length);
        result.Should().Contain("truncated");
        result.Should().Contain("100");
    }

    [Fact]
    public void MarkdownTable_KeyValue_FormatsCorrectly()
    {
        var rows = new[] { ("Name", "Alice"), ("Age", "30") };
        var table = OutputFormatter.MarkdownTable(rows);

        table.Should().Contain("| Key | Value |");
        table.Should().Contain("| Name | Alice |");
        table.Should().Contain("| Age | 30 |");
    }

    [Fact]
    public void MarkdownTable_CustomHeaders_FormatsCorrectly()
    {
        var headers = new[] { "Tool", "Tier" };
        var rows = new[] { new[] { "read_file", "AutoApprove" } };
        var table = OutputFormatter.MarkdownTable(headers, rows);

        table.Should().Contain("| Tool | Tier |");
        table.Should().Contain("| read_file | AutoApprove |");
    }

    [Fact]
    public void BulletList_FormatsItems()
    {
        var list = OutputFormatter.BulletList(["first", "second", "third"]);

        list.Should().Be("- first\n- second\n- third");
    }

    [Fact]
    public void FromProcessResult_Success_ReturnsStdout()
    {
        var result = new ProcessResult(0, "output data", "");
        OutputFormatter.FromProcessResult(result).Should().Be("output data");
    }

    [Fact]
    public void FromProcessResult_Failure_ReturnsErrorWithStderr()
    {
        var result = new ProcessResult(1, "", "something failed");
        var output = OutputFormatter.FromProcessResult(result, "git");

        output.Should().StartWith("Error:");
        output.Should().Contain("git");
        output.Should().Contain("something failed");
    }

    [Fact]
    public void FromProcessResult_FailureNoStderr_ReturnsExitCode()
    {
        var result = new ProcessResult(127, "", "");
        var output = OutputFormatter.FromProcessResult(result);

        output.Should().Contain("exit code 127");
    }
}
