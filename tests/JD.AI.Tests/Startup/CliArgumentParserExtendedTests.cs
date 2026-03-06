using FluentAssertions;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class CliArgumentParserExtendedTests
{
    // ── Boolean flags ──────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_NoArgs_DefaultFlags()
    {
        var opts = await CliArgumentParser.ParseAsync([]);

        opts.SkipPermissions.Should().BeFalse();
        opts.PrintMode.Should().BeFalse();
        opts.GatewayMode.Should().BeFalse();
        opts.VerboseMode.Should().BeFalse();
        opts.DebugMode.Should().BeFalse();
        opts.ContinueSession.Should().BeFalse();
        opts.IsNewSession.Should().BeFalse();
        opts.UseWorktree.Should().BeFalse();
        opts.ForkSession.Should().BeFalse();
        opts.NoSessionPersistence.Should().BeFalse();
        opts.ForceUpdateCheck.Should().BeFalse();
        opts.Subcommand.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_SkipPermissions()
    {
        var opts = await CliArgumentParser.ParseAsync(["--dangerously-skip-permissions"]);
        opts.SkipPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ForceUpdateCheck()
    {
        var opts = await CliArgumentParser.ParseAsync(["--force-update-check"]);
        opts.ForceUpdateCheck.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_VerboseMode()
    {
        var opts = await CliArgumentParser.ParseAsync(["--verbose"]);
        opts.VerboseMode.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_GatewayMode()
    {
        var opts = await CliArgumentParser.ParseAsync(["--gateway"]);
        opts.GatewayMode.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_NewSession()
    {
        var opts = await CliArgumentParser.ParseAsync(["--new"]);
        opts.IsNewSession.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ForkSession()
    {
        var opts = await CliArgumentParser.ParseAsync(["--fork-session"]);
        opts.ForkSession.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_NoSessionPersistence()
    {
        var opts = await CliArgumentParser.ParseAsync(["--no-session-persistence"]);
        opts.NoSessionPersistence.Should().BeTrue();
    }

    [Theory]
    [InlineData("-w")]
    [InlineData("--worktree")]
    public async Task ParseAsync_UseWorktree(string flag)
    {
        var opts = await CliArgumentParser.ParseAsync([flag]);
        opts.UseWorktree.Should().BeTrue();
    }

    [Theory]
    [InlineData("-c")]
    [InlineData("--continue")]
    public async Task ParseAsync_ContinueSession(string flag)
    {
        var opts = await CliArgumentParser.ParseAsync([flag]);
        opts.ContinueSession.Should().BeTrue();
    }

    [Theory]
    [InlineData("-p")]
    [InlineData("--print")]
    public async Task ParseAsync_PrintMode(string flag)
    {
        var opts = await CliArgumentParser.ParseAsync([flag]);
        opts.PrintMode.Should().BeTrue();
    }

    // ── Value flags ────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ResumeId()
    {
        var opts = await CliArgumentParser.ParseAsync(["--resume", "abc-123"]);
        opts.ResumeId.Should().Be("abc-123");
    }

    [Fact]
    public async Task ParseAsync_GatewayPort()
    {
        var opts = await CliArgumentParser.ParseAsync(["--gateway", "--gateway-port", "9090"]);
        opts.GatewayPort.Should().Be("9090");
    }

    [Fact]
    public async Task ParseAsync_MaxTurns_ValidNumber()
    {
        var opts = await CliArgumentParser.ParseAsync(["--max-turns", "5"]);
        opts.MaxTurns.Should().Be(5);
    }

    [Fact]
    public async Task ParseAsync_MaxTurns_InvalidNumber_ReturnsNull()
    {
        var opts = await CliArgumentParser.ParseAsync(["--max-turns", "not-a-number"]);
        opts.MaxTurns.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_MaxBudgetUsd()
    {
        var opts = await CliArgumentParser.ParseAsync(["--max-budget-usd", "10.50"]);
        opts.MaxBudgetUsd.Should().Be(10.50m);
    }

    [Fact]
    public async Task ParseAsync_MaxBudgetUsd_Invalid_ReturnsNull()
    {
        var opts = await CliArgumentParser.ParseAsync(["--max-budget-usd", "xyz"]);
        opts.MaxBudgetUsd.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_OutputFormat()
    {
        var opts = await CliArgumentParser.ParseAsync(["--output-format", "json"]);
        opts.OutputFormat.Should().Be("json");
    }

    [Fact]
    public async Task ParseAsync_OutputFormat_DefaultsToText()
    {
        var opts = await CliArgumentParser.ParseAsync([]);
        opts.OutputFormat.Should().Be("text");
    }

    [Fact]
    public async Task ParseAsync_InputFormat()
    {
        var opts = await CliArgumentParser.ParseAsync(["--input-format", "stream-json"]);
        opts.InputFormat.Should().Be("stream-json");
    }

    [Fact]
    public async Task ParseAsync_InputFormat_DefaultsToText()
    {
        var opts = await CliArgumentParser.ParseAsync([]);
        opts.InputFormat.Should().Be("text");
    }

    [Fact]
    public async Task ParseAsync_SessionId()
    {
        var opts = await CliArgumentParser.ParseAsync(["--session-id", "my-session"]);
        opts.CliSessionId.Should().Be("my-session");
    }

    [Fact]
    public async Task ParseAsync_PermissionMode()
    {
        var opts = await CliArgumentParser.ParseAsync(["--permission-mode", "plan"]);
        opts.PermissionModeStr.Should().Be("plan");
    }

    [Fact]
    public async Task ParseAsync_JsonSchema()
    {
        var opts = await CliArgumentParser.ParseAsync(["--json-schema", "{\"type\":\"object\"}"]);
        opts.JsonSchemaArg.Should().Be("{\"type\":\"object\"}");
    }

    // ── Collection flags ───────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_AllowedTools()
    {
        var opts = await CliArgumentParser.ParseAsync(["--allowedTools", "read_file,write_file"]);
        opts.AllowedTools.Should().BeEquivalentTo(["read_file", "write_file"]);
    }

    [Fact]
    public async Task ParseAsync_DisallowedTools()
    {
        var opts = await CliArgumentParser.ParseAsync(["--disallowedTools", "exec_cmd"]);
        opts.DisallowedTools.Should().BeEquivalentTo(["exec_cmd"]);
    }

    [Fact]
    public async Task ParseAsync_FallbackModels()
    {
        var opts = await CliArgumentParser.ParseAsync(["--fallback-model", "gpt-4.1,claude-4-sonnet"]);
        opts.FallbackModels.Should().BeEquivalentTo(["gpt-4.1", "claude-4-sonnet"]);
    }

    [Fact]
    public async Task ParseAsync_AddDir_SingleDir()
    {
        var opts = await CliArgumentParser.ParseAsync(["--add-dir", "/tmp/project"]);
        opts.AdditionalDirs.Should().ContainSingle().Which.Should().Be("/tmp/project");
    }

    [Fact]
    public async Task ParseAsync_AddDir_MultipleDirs()
    {
        var opts = await CliArgumentParser.ParseAsync(
            ["--add-dir", "/a", "--add-dir", "/b"]);
        opts.AdditionalDirs.Should().HaveCount(2);
        opts.AdditionalDirs.Should().Contain("/a");
        opts.AdditionalDirs.Should().Contain("/b");
    }

    // ── System prompt flags ────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_SystemPromptOverride()
    {
        var opts = await CliArgumentParser.ParseAsync(["--system-prompt", "You are a pirate"]);
        opts.SystemPromptOverride.Should().Be("You are a pirate");
    }

    [Fact]
    public async Task ParseAsync_AppendSystemPrompt()
    {
        var opts = await CliArgumentParser.ParseAsync(["--append-system-prompt", "Always be concise"]);
        opts.AppendSystemPrompt.Should().Be("Always be concise");
    }

    [Fact]
    public async Task ParseAsync_SystemPromptFile()
    {
        var opts = await CliArgumentParser.ParseAsync(["--system-prompt-file", "/tmp/prompt.txt"]);
        opts.SystemPromptFile.Should().Be("/tmp/prompt.txt");
    }

    [Fact]
    public async Task ParseAsync_AppendSystemPromptFile()
    {
        var opts = await CliArgumentParser.ParseAsync(["--append-system-prompt-file", "/tmp/extra.txt"]);
        opts.AppendSystemPromptFile.Should().Be("/tmp/extra.txt");
    }

    // ── Subcommand detection ───────────────────────────────────────────

    [Theory]
    [InlineData("mcp")]
    [InlineData("plugin")]
    [InlineData("onboard")]
    [InlineData("wizard")]
    [InlineData("update")]
    [InlineData("install")]
    public async Task ParseAsync_AllSubcommands_AreDetected(string cmd)
    {
        var opts = await CliArgumentParser.ParseAsync([cmd]);
        opts.Subcommand.Should().Be(cmd);
    }

    [Fact]
    public async Task ParseAsync_Subcommand_CaseInsensitive()
    {
        var opts = await CliArgumentParser.ParseAsync(["MCP"]);
        opts.Subcommand.Should().Be("mcp");
    }

    [Fact]
    public async Task ParseAsync_SubcommandArgs_Forwarded()
    {
        var opts = await CliArgumentParser.ParseAsync(["mcp", "list", "--json"]);
        opts.Subcommand.Should().Be("mcp");
        opts.SubcommandArgs.Should().BeEquivalentTo(["list", "--json"]);
    }

    [Fact]
    public async Task ParseAsync_UnknownPositional_NotSubcommand()
    {
        var opts = await CliArgumentParser.ParseAsync(["not-a-subcommand"]);
        opts.Subcommand.Should().BeNull();
    }

    // ── Debug flags ────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_DebugMode()
    {
        var opts = await CliArgumentParser.ParseAsync(["--debug"]);
        opts.DebugMode.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_DebugWithCategories()
    {
        var opts = await CliArgumentParser.ParseAsync(["--debug", "routing,tools"]);
        opts.DebugMode.Should().BeTrue();
        opts.DebugCategories.Should().Be("routing,tools");
    }

    [Fact]
    public async Task ParseAsync_DebugCategories_DashPrefix_IgnoredAsFlag()
    {
        var opts = await CliArgumentParser.ParseAsync(["--debug", "--verbose"]);
        opts.DebugMode.Should().BeTrue();
        opts.DebugCategories.Should().BeNull();
    }

    // ── Print mode with query ──────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_PrintWithQuery()
    {
        var opts = await CliArgumentParser.ParseAsync(["-p", "What is 2+2?"]);
        opts.PrintMode.Should().BeTrue();
        opts.PrintQuery.Should().Be("What is 2+2?");
    }

    // ── Default collection values ──────────────────────────────────────

    [Fact]
    public async Task ParseAsync_Defaults_CollectionsAreEmpty()
    {
        var opts = await CliArgumentParser.ParseAsync([]);

        opts.AdditionalDirs.Should().BeEmpty();
        opts.AllowedTools.Should().BeNull();
        opts.DisallowedTools.Should().BeNull();
        opts.FallbackModels.Should().BeEmpty();
        opts.RoutingStrategy.Should().Be("local-first");
        opts.SubcommandArgs.Should().BeEmpty();
    }

    // ── Combined flags ─────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_MultipleFlagsCombined()
    {
        var opts = await CliArgumentParser.ParseAsync(
            ["--verbose", "--model", "gpt-4.1", "--provider", "openai", "--max-turns", "10"]);

        opts.VerboseMode.Should().BeTrue();
        opts.CliModel.Should().Be("gpt-4.1");
        opts.CliProvider.Should().Be("openai");
        opts.MaxTurns.Should().Be(10);
    }
}
