using FluentAssertions;
using JD.AI.Commands;

namespace JD.AI.Tests.Commands;

public sealed class SlashCommandCatalogExtendedTests
{
    // ── BuildHelpText ─────────────────────────────────────────────────────

    [Fact]
    public void BuildHelpText_ContainsAllHelpEntries()
    {
        var help = SlashCommandCatalog.BuildHelpText();

        foreach (var entry in SlashCommandCatalog.HelpEntries)
        {
            help.Should().Contain(entry.Usage);
            help.Should().Contain(entry.Description);
        }
    }

    [Fact]
    public void BuildHelpText_ContainsHeader()
    {
        var help = SlashCommandCatalog.BuildHelpText();
        help.Should().Contain("Available commands");
    }

    [Fact]
    public void BuildHelpText_ContainsJdaiPrefixNote()
    {
        var help = SlashCommandCatalog.BuildHelpText();
        help.Should().Contain("/jdai-");
    }

    // ── TryResolveDispatch ────────────────────────────────────────────────

    [Fact]
    public void TryResolveDispatch_WithSlash_Resolves()
    {
        SlashCommandCatalog.TryResolveDispatch("/help", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void TryResolveDispatch_WithoutSlash_Resolves()
    {
        SlashCommandCatalog.TryResolveDispatch("help", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void TryResolveDispatch_CaseInsensitive()
    {
        SlashCommandCatalog.TryResolveDispatch("/HELP", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void TryResolveDispatch_JdaiPrefix_Resolves()
    {
        SlashCommandCatalog.TryResolveDispatch("/jdai-help", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void TryResolveDispatch_JdaiPrefix_CaseInsensitive()
    {
        SlashCommandCatalog.TryResolveDispatch("/JDAI-help", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void TryResolveDispatch_UnknownCommand_ReturnsFalse()
    {
        SlashCommandCatalog.TryResolveDispatch("/nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolveDispatch_EmptyString_ReturnsFalse()
    {
        SlashCommandCatalog.TryResolveDispatch("", out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolveDispatch_Whitespace_ReturnsFalse()
    {
        SlashCommandCatalog.TryResolveDispatch("   ", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("/models")]
    [InlineData("/model")]
    [InlineData("/clear")]
    [InlineData("/compact")]
    [InlineData("/cost")]
    [InlineData("/history")]
    public void TryResolveDispatch_CommonCommands(string command)
    {
        SlashCommandCatalog.TryResolveDispatch(command, out _).Should().BeTrue();
    }

    // ── Definitions structural ────────────────────────────────────────────

    [Fact]
    public void Definitions_AllHaveNonEmptyCommand()
    {
        foreach (var def in SlashCommandCatalog.Definitions)
        {
            def.Command.Should().NotBeNullOrWhiteSpace(
                $"Definition for {def.Id} has empty command");
        }
    }

    [Fact]
    public void Definitions_AllCommandsStartWithSlash()
    {
        foreach (var def in SlashCommandCatalog.Definitions)
        {
            def.Command.Should().StartWith("/",
                $"Command for {def.Id} should start with /");
        }
    }

    [Fact]
    public void Definitions_UniqueIds()
    {
        var ids = SlashCommandCatalog.Definitions.Select(d => d.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    // ── HelpEntries ───────────────────────────────────────────────────────

    [Fact]
    public void HelpEntries_NotEmpty()
    {
        SlashCommandCatalog.HelpEntries.Should().NotBeEmpty();
    }

    [Fact]
    public void HelpEntries_AllHaveUsageAndDescription()
    {
        foreach (var entry in SlashCommandCatalog.HelpEntries)
        {
            entry.Usage.Should().NotBeNullOrWhiteSpace();
            entry.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    // ── CompletionEntries ─────────────────────────────────────────────────

    [Fact]
    public void CompletionEntries_NotEmpty()
    {
        SlashCommandCatalog.CompletionEntries.Should().NotBeEmpty();
    }

    [Fact]
    public void CompletionEntries_AllStartWithSlash()
    {
        foreach (var entry in SlashCommandCatalog.CompletionEntries)
        {
            entry.Command.Should().StartWith("/",
                $"Completion command should start with /: {entry.Command}");
        }
    }

    // ── SlashCommandDefinition ────────────────────────────────────────────

    [Fact]
    public void SlashCommandDefinition_DefaultValues()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help,
            "/test",
            "/test",
            "Test help");

        def.IncludeInCompletion.Should().BeTrue();
        def.Aliases.Should().BeEmpty();
        def.AdditionalCompletions.Should().BeEmpty();
        def.CompletionDescription.Should().Be("Test help");
    }

    [Fact]
    public void SlashCommandDefinition_CustomCompletionDescription()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help,
            "/test",
            "/test",
            "Full help text",
            CompletionDescription: "Short desc");

        def.CompletionDescription.Should().Be("Short desc");
    }

    // ── SlashCommandDescriptor ────────────────────────────────────────────

    [Fact]
    public void SlashCommandDescriptor_RecordEquality()
    {
        var a = new SlashCommandDescriptor("/test", "desc");
        var b = new SlashCommandDescriptor("/test", "desc");
        a.Should().Be(b);
    }

    // ── SlashCommandHelpEntry ─────────────────────────────────────────────

    [Fact]
    public void SlashCommandHelpEntry_RecordEquality()
    {
        var a = new SlashCommandHelpEntry("/test", "desc");
        var b = new SlashCommandHelpEntry("/test", "desc");
        a.Should().Be(b);
    }
}
