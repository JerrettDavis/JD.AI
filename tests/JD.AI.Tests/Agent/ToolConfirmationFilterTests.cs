using FluentAssertions;
using JD.AI.Agent;
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
    public void ToolTierMap_FileReadAndList_RequirePrompting()
    {
        ToolConfirmationFilter.ToolTierMap["read_file"].Should().NotBe(SafetyTier.AutoApprove);
        ToolConfirmationFilter.ToolTierMap["list_directory"].Should().NotBe(SafetyTier.AutoApprove);
    }
}
