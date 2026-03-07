using FluentAssertions;
using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class CuratedMcpModelsTests
{
    // ── CuratedMcpTransport enum ──────────────────────────────────────────

    [Theory]
    [InlineData(CuratedMcpTransport.Stdio, 0)]
    [InlineData(CuratedMcpTransport.Http, 1)]
    public void CuratedMcpTransport_Values(CuratedMcpTransport transport, int expected) =>
        ((int)transport).Should().Be(expected);

    // ── McpConnectionState enum ───────────────────────────────────────────

    [Theory]
    [InlineData(McpConnectionState.Unknown, 0)]
    [InlineData(McpConnectionState.Connected, 1)]
    [InlineData(McpConnectionState.Failed, 2)]
    [InlineData(McpConnectionState.Connecting, 3)]
    [InlineData(McpConnectionState.Disabled, 4)]
    public void McpConnectionState_Values(McpConnectionState state, int expected) =>
        ((int)state).Should().Be(expected);

    // ── CuratedMcpEnvVar ──────────────────────────────────────────────────

    [Fact]
    public void CuratedMcpEnvVar_DefaultIsSecret()
    {
        var envVar = new CuratedMcpEnvVar("GITHUB_TOKEN", "Enter your GitHub token");
        envVar.Name.Should().Be("GITHUB_TOKEN");
        envVar.Prompt.Should().Be("Enter your GitHub token");
        envVar.IsSecret.Should().BeTrue();
    }

    [Fact]
    public void CuratedMcpEnvVar_ExplicitNotSecret()
    {
        var envVar = new CuratedMcpEnvVar("REGION", "Select region", IsSecret: false);
        envVar.IsSecret.Should().BeFalse();
    }

    [Fact]
    public void CuratedMcpEnvVar_RecordEquality()
    {
        var a = new CuratedMcpEnvVar("TOKEN", "Prompt");
        var b = new CuratedMcpEnvVar("TOKEN", "Prompt");
        a.Should().Be(b);
    }

    // ── CuratedMcpArgPrompt ───────────────────────────────────────────────

    [Fact]
    public void CuratedMcpArgPrompt_RequiredProperties()
    {
        var prompt = new CuratedMcpArgPrompt("workspace", "Enter workspace path");
        prompt.Placeholder.Should().Be("workspace");
        prompt.Prompt.Should().Be("Enter workspace path");
        prompt.Example.Should().BeNull();
    }

    [Fact]
    public void CuratedMcpArgPrompt_WithExample()
    {
        var prompt = new CuratedMcpArgPrompt("repo", "Repo URL", Example: "https://github.com/user/repo");
        prompt.Example.Should().Be("https://github.com/user/repo");
    }

    [Fact]
    public void CuratedMcpArgPrompt_RecordEquality()
    {
        var a = new CuratedMcpArgPrompt("ph", "prompt");
        var b = new CuratedMcpArgPrompt("ph", "prompt");
        a.Should().Be(b);
    }

    // ── CuratedMcpEntry ───────────────────────────────────────────────────

    [Fact]
    public void CuratedMcpEntry_RequiredProperties()
    {
        var entry = new CuratedMcpEntry(
            "github", "GitHub", "Source Control", "GitHub integration",
            CuratedMcpTransport.Stdio);
        entry.Id.Should().Be("github");
        entry.DisplayName.Should().Be("GitHub");
        entry.Category.Should().Be("Source Control");
        entry.Description.Should().Be("GitHub integration");
        entry.Transport.Should().Be(CuratedMcpTransport.Stdio);
    }

    [Fact]
    public void CuratedMcpEntry_OptionalDefaults()
    {
        var entry = new CuratedMcpEntry(
            "test", "Test", "Category", "Desc",
            CuratedMcpTransport.Http);
        entry.Command.Should().BeNull();
        entry.DefaultArgs.Should().BeNull();
        entry.Url.Should().BeNull();
        entry.RequiredEnvVars.Should().BeNull();
        entry.PromptArgs.Should().BeNull();
        entry.DocsUrl.Should().BeNull();
        entry.InstallNote.Should().BeNull();
    }

    [Fact]
    public void CuratedMcpEntry_AllProperties()
    {
        var entry = new CuratedMcpEntry(
            "github", "GitHub", "Source Control", "GitHub integration",
            CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-github"],
            Url: null,
            RequiredEnvVars: [new CuratedMcpEnvVar("GITHUB_TOKEN", "Enter token")],
            PromptArgs: [new CuratedMcpArgPrompt("owner", "Repo owner")],
            DocsUrl: "https://github.com/modelcontextprotocol",
            InstallNote: "Requires Node.js 18+");

        entry.Command.Should().Be("npx");
        entry.DefaultArgs.Should().HaveCount(2);
        entry.RequiredEnvVars.Should().HaveCount(1);
        entry.PromptArgs.Should().HaveCount(1);
        entry.DocsUrl.Should().NotBeNull();
        entry.InstallNote.Should().Be("Requires Node.js 18+");
    }

    [Fact]
    public void CuratedMcpEntry_RecordEquality()
    {
        var a = new CuratedMcpEntry("id", "Name", "Cat", "Desc", CuratedMcpTransport.Http);
        var b = new CuratedMcpEntry("id", "Name", "Cat", "Desc", CuratedMcpTransport.Http);
        a.Should().Be(b);
    }
}
