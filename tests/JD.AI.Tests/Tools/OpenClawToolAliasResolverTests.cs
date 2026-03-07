using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class OpenClawToolAliasResolverTests
{
    [Theory]
    [InlineData("bash", "run_command")]
    [InlineData("read", "read_file")]
    [InlineData("write", "write_file")]
    [InlineData("edit", "edit_file")]
    [InlineData("ls", "list_directory")]
    [InlineData("webfetch", "web_fetch")]
    [InlineData("websearch", "web_search")]
    [InlineData("todo_read", "list_tasks")]
    [InlineData("todo_write", "update_task")]
    [InlineData("exec", "run_command")]
    [InlineData("process", "process")]
    public void Resolve_KnownAlias_ReturnsMappedName(string alias, string expected) =>
        OpenClawToolAliasResolver.Resolve(alias).Should().Be(expected);

    [Theory]
    [InlineData("BASH", "run_command")]
    [InlineData("Read", "read_file")]
    [InlineData("EDIT", "edit_file")]
    public void Resolve_CaseInsensitive(string alias, string expected) =>
        OpenClawToolAliasResolver.Resolve(alias).Should().Be(expected);

    [Theory]
    [InlineData("unknown_tool")]
    [InlineData("my_custom_tool")]
    [InlineData("run_command")]
    public void Resolve_UnknownAlias_ReturnsOriginal(string toolName) =>
        OpenClawToolAliasResolver.Resolve(toolName).Should().Be(toolName);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NullOrWhitespace_ReturnsInput(string? input) =>
        OpenClawToolAliasResolver.Resolve(input!).Should().Be(input);
}
