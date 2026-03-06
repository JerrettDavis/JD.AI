using FluentAssertions;
using JD.AI.Commands;

namespace JD.AI.Tests.Commands;

public sealed class SlashCommandModelsTests
{
    // ── SlashCommandDescriptor ────────────────────────────────────────────

    [Fact]
    public void Descriptor_Construction()
    {
        var desc = new SlashCommandDescriptor("/help", "Show help");
        desc.Command.Should().Be("/help");
        desc.Description.Should().Be("Show help");
    }

    [Fact]
    public void Descriptor_RecordEquality()
    {
        var a = new SlashCommandDescriptor("/x", "X");
        var b = new SlashCommandDescriptor("/x", "X");
        a.Should().Be(b);
    }

    [Fact]
    public void Descriptor_RecordInequality()
    {
        var a = new SlashCommandDescriptor("/x", "X");
        var b = new SlashCommandDescriptor("/y", "Y");
        a.Should().NotBe(b);
    }

    // ── SlashCommandHelpEntry ─────────────────────────────────────────────

    [Fact]
    public void HelpEntry_Construction()
    {
        var entry = new SlashCommandHelpEntry("/help", "Show this help");
        entry.Usage.Should().Be("/help");
        entry.Description.Should().Be("Show this help");
    }

    [Fact]
    public void HelpEntry_RecordEquality()
    {
        var a = new SlashCommandHelpEntry("/a", "A");
        var b = new SlashCommandHelpEntry("/a", "A");
        a.Should().Be(b);
    }

    // ── SlashCommandDefinition ────────────────────────────────────────────

    [Fact]
    public void Definition_RequiredProperties()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Show help");

        def.Id.Should().Be(SlashCommandId.Help);
        def.Command.Should().Be("/help");
        def.HelpSignature.Should().Be("/help");
        def.HelpDescription.Should().Be("Show help");
    }

    [Fact]
    public void Definition_CompletionDescription_DefaultsToHelpDescription()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Clear, "/clear", "/clear", "Clear history");

        def.CompletionDescription.Should().Be("Clear history");
    }

    [Fact]
    public void Definition_CompletionDescription_CanBeCustom()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Clear, "/clear", "/clear", "Clear history",
            CompletionDescription: "Custom desc");

        def.CompletionDescription.Should().Be("Custom desc");
    }

    [Fact]
    public void Definition_IncludeInCompletion_DefaultsTrue()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Help");

        def.IncludeInCompletion.Should().BeTrue();
    }

    [Fact]
    public void Definition_IncludeInCompletion_CanBeFalse()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Help",
            IncludeInCompletion: false);

        def.IncludeInCompletion.Should().BeFalse();
    }

    [Fact]
    public void Definition_Aliases_DefaultEmpty()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Help");

        def.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Definition_Aliases_CanBeSet()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Help",
            Aliases: ["/h", "/??"]);

        def.Aliases.Should().HaveCount(2);
        def.Aliases[0].Should().Be("/h");
    }

    [Fact]
    public void Definition_AdditionalCompletions_DefaultEmpty()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Help, "/help", "/help", "Help");

        def.AdditionalCompletions.Should().BeEmpty();
    }

    [Fact]
    public void Definition_AdditionalCompletions_CanBeSet()
    {
        var def = new SlashCommandDefinition(
            SlashCommandId.Provider, "/provider", "/provider", "Provider",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/provider add", "Add provider"),
                new SlashCommandDescriptor("/provider remove", "Remove provider"),
            ]);

        def.AdditionalCompletions.Should().HaveCount(2);
    }

    // ── SlashCommandId enum ───────────────────────────────────────────────

    [Fact]
    public void SlashCommandId_HasExpectedValues()
    {
        Enum.GetValues<SlashCommandId>().Should().Contain(SlashCommandId.Help);
        Enum.GetValues<SlashCommandId>().Should().Contain(SlashCommandId.Models);
        Enum.GetValues<SlashCommandId>().Should().Contain(SlashCommandId.Clear);
        Enum.GetValues<SlashCommandId>().Should().Contain(SlashCommandId.Quit);
    }

    [Fact]
    public void SlashCommandId_HelpIsFirst()
    {
        ((int)SlashCommandId.Help).Should().Be(0);
    }

    // ── Catalog integration ───────────────────────────────────────────────

    [Fact]
    public void Catalog_DefinitionsNotEmpty()
    {
        SlashCommandCatalog.Definitions.Should().NotBeEmpty();
    }

    [Fact]
    public void Catalog_HelpEntriesNotEmpty()
    {
        SlashCommandCatalog.HelpEntries.Should().NotBeEmpty();
    }

    [Fact]
    public void Catalog_CompletionEntriesNotEmpty()
    {
        SlashCommandCatalog.CompletionEntries.Should().NotBeEmpty();
    }

    [Fact]
    public void Catalog_TryResolveDispatch_UnknownCommand_ReturnsFalse()
    {
        SlashCommandCatalog.TryResolveDispatch("/nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void Catalog_TryResolveDispatch_Help_ReturnsTrue()
    {
        SlashCommandCatalog.TryResolveDispatch("/help", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Help);
    }

    [Fact]
    public void Catalog_TryResolveDispatch_Clear_ReturnsTrue()
    {
        SlashCommandCatalog.TryResolveDispatch("/clear", out var id).Should().BeTrue();
        id.Should().Be(SlashCommandId.Clear);
    }

    [Fact]
    public void Catalog_AllDefinitions_HaveValidId()
    {
        foreach (var def in SlashCommandCatalog.Definitions)
        {
            Enum.IsDefined(def.Id).Should().BeTrue();
            def.Command.Should().StartWith("/");
        }
    }
}
