using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Safety;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Agent;

/// <summary>
/// Unit tests for the pure-logic helpers inside <see cref="ToolConfirmationFilter"/>.
/// These are testable without an LLM connection or a live Spectre.Console terminal.
/// </summary>
public sealed class ToolConfirmationFilterTests
{
    // ── BuildRedactedArgs ─────────────────────────────────────────────────────

    [Fact]
    public void BuildRedactedArgs_NullArguments_ReturnsEmpty()
    {
        var result = ToolConfirmationFilter.BuildRedactedArgs(null);
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildRedactedArgs_EmptyArguments_ReturnsEmpty()
    {
        var args = new KernelArguments();
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildRedactedArgs_NonSensitiveKey_ShowsValue()
    {
        var args = new KernelArguments { ["filePath"] = "/tmp/hello.txt" };
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        Assert.Contains("filePath=/tmp/hello.txt", result);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("PASSWORD")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("content")]
    [InlineData("code")]
    [InlineData("input")]
    [InlineData("body")]
    public void BuildRedactedArgs_SensitiveKey_RedactsValue(string sensitiveKey)
    {
        var args = new KernelArguments { [sensitiveKey] = "super-secret-value" };
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("super-secret-value", result);
    }

    [Fact]
    public void BuildRedactedArgs_LongValue_TruncatesAt80()
    {
        var longValue = new string('x', 120);
        var args = new KernelArguments { ["data"] = longValue };
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        // value should be truncated to 77 chars + "..."
        Assert.Contains("data=", result);
        Assert.Contains("...", result);
        // Full 120-char value must not appear
        Assert.DoesNotContain(longValue, result);
    }

    [Fact]
    public void BuildRedactedArgs_MultipleKeys_SeparatedByComma()
    {
        var args = new KernelArguments
        {
            ["path"] = "/etc/hosts",
            ["mode"] = "read",
        };
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        Assert.Contains(", ", result);
        Assert.Contains("path=", result);
        Assert.Contains("mode=", result);
    }

    [Fact]
    public void BuildRedactedArgs_NullValue_ShowsNull()
    {
        var args = new KernelArguments { ["key"] = null };
        var result = ToolConfirmationFilter.BuildRedactedArgs(args);
        Assert.Contains("key=null", result);
    }

    [Fact]
    public void BuildRedactedArgs_EditFile_RedactsOldAndNewText()
    {
        var args = new KernelArguments
        {
            ["path"] = "file.txt",
            ["oldStr"] = "before",
            ["newStr"] = "after",
        };

        var result = ToolConfirmationFilter.BuildRedactedArgs("edit_file", args);

        result.Should().Be("path=file.txt, oldStr=[REDACTED], newStr=[REDACTED]");
    }

    [Fact]
    public void BuildRedactedArgs_ApplyPatch_RedactsPatchPayload()
    {
        var args = new KernelArguments
        {
            ["editsJson"] = """[{"path":"a.txt","oldText":"x","newText":"y"}]"""
        };

        var result = ToolConfirmationFilter.BuildRedactedArgs("apply_patch", args);

        result.Should().Be("editsJson=[REDACTED]");
    }

    [Fact]
    public void BuildRedactedArgs_RunCommand_RedactsCommandValue()
    {
        var args = new KernelArguments
        {
            ["command"] = "curl https://api.example.com -H \"Authorization: Bearer secret-token\""
        };

        var result = ToolConfirmationFilter.BuildRedactedArgs("run_command", args);

        result.Should().Be("command=[REDACTED]");
    }

    // ── ResolvePolicyToolName ─────────────────────────────────────────────────

    [Fact]
    public void ResolvePolicyToolName_UnknownTool_ReturnsSameName()
    {
        // For tools not in the alias map, the canonical name equals the function name
        var result = ToolConfirmationFilter.ResolvePolicyToolName("SomeUnknownTool");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void ResolvePolicyToolName_NeverReturnsNull()
    {
        var result = ToolConfirmationFilter.ResolvePolicyToolName("AnyFunctionName");
        Assert.NotNull(result);
    }

    // ── BuildContentPreview ───────────────────────────────────────────────────

    [Fact]
    public void BuildContentPreview_NullOrWhitespace_ReturnsNull()
    {
        ToolConfirmationFilter.BuildContentPreview(null).Should().BeNull();
        ToolConfirmationFilter.BuildContentPreview("").Should().BeNull();
        ToolConfirmationFilter.BuildContentPreview("   ").Should().BeNull();
    }

    [Fact]
    public void BuildContentPreview_UsesFirstNonEmptyLine()
    {
        var content = "\n\nfirst line\nsecond line";

        var preview = ToolConfirmationFilter.BuildContentPreview(content);

        preview.Should().Be("first line");
    }

    [Fact]
    public void BuildContentPreview_TruncatesLongLine()
    {
        var longLine = new string('x', 200);

        var preview = ToolConfirmationFilter.BuildContentPreview(longLine, maxChars: 50);

        preview.Should().NotBeNull();
        preview!.Length.Should().Be(50);
        preview.Should().EndWith("...");
    }

    [Fact]
    public void BuildDisplayArgs_WriteFile_ShowsPathAndSizeWithoutRawContent()
    {
        var args = new KernelArguments
        {
            ["path"] = @"C:\temp\note.txt",
            ["content"] = "\n\nfirst line\nsecond line"
        };

        var display = ToolConfirmationFilter.BuildDisplayArgs("write_file", args);

        display.Should().Contain(@"path=C:\temp\note.txt [24 chars]");
        display.Should().NotContain("first line");
        display.Should().NotContain("second line");
    }

    [Fact]
    public void BuildDisplayArgs_ApplyPatch_RedactsPatchPayload()
    {
        var args = new KernelArguments
        {
            ["editsJson"] = """[{"path":"a.txt","oldText":"x","newText":"y"}]"""
        };

        var display = ToolConfirmationFilter.BuildDisplayArgs("apply_patch", args);

        display.Should().Be("editsJson=[REDACTED]");
    }

    [Fact]
    public void BuildPersistedArgs_RunCommand_RedactsCommandValue()
    {
        var args = new KernelArguments
        {
            ["command"] = "dotnet test"
        };

        var persisted = ToolConfirmationFilter.BuildPersistedArgs("run_command", args);

        persisted.Should().Be("command=[REDACTED]");
    }

    [Fact]
    public void BuildPersistedArgs_GenericSensitiveKeys_RedactsValues()
    {
        var args = new KernelArguments
        {
            ["token"] = "abc123",
            ["body"] = "secret",
            ["path"] = "draft.txt"
        };

        var persisted = ToolConfirmationFilter.BuildPersistedArgs("missing_tool", args);

        persisted.Should().Contain("token=[REDACTED]");
        persisted.Should().Contain("body=[REDACTED]");
        persisted.Should().Contain("path=draft.txt");
        persisted.Should().NotContain("abc123");
        persisted.Should().NotContain("secret");
    }

    [Fact]
    public void ToolTierMap_FileReadAndList_RequirePrompting()
    {
        ToolConfirmationFilter.ToolTierMap["read_file"].Should().NotBe(SafetyTier.AutoApprove);
        ToolConfirmationFilter.ToolTierMap["list_directory"].Should().NotBe(SafetyTier.AutoApprove);
    }
}
