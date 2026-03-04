using JD.AI.Agent;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Agent;

public sealed class ToolAliasResolutionTests
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
    [InlineData("run_command", "run_command")]
    public void OpenClawToolAliasResolver_ResolvesExpectedCanonicalName(
        string alias,
        string expectedCanonical)
    {
        var result = OpenClawToolAliasResolver.Resolve(alias);

        Assert.Equal(expectedCanonical, result);
    }

    [Fact]
    public void ToolConfirmationFilter_UsesAliasResolutionForPolicyName()
    {
        Assert.Equal(
            "run_command",
            ToolConfirmationFilter.ResolvePolicyToolName("bash"));
    }
}
