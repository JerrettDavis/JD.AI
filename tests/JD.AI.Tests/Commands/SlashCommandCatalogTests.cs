using JD.AI.Commands;

namespace JD.AI.Tests.Commands;

public sealed class SlashCommandCatalogTests
{
    [Fact]
    public void Definitions_HaveDispatchBindings_ForPrimaryAndAliasCommands()
    {
        foreach (var definition in SlashCommandCatalog.Definitions)
        {
            Assert.True(
                SlashCommandCatalog.TryResolveDispatch(definition.Command, out var resolvedId),
                $"Primary command is not dispatchable: {definition.Command}");
            Assert.Equal(definition.Id, resolvedId);

            foreach (var alias in definition.Aliases)
            {
                Assert.True(
                    SlashCommandCatalog.TryResolveDispatch(alias, out var aliasId),
                    $"Alias is not dispatchable: {alias}");
                Assert.Equal(definition.Id, aliasId);
            }
        }
    }

    [Fact]
    public void HelpEntries_AllResolveToDispatchableCommands()
    {
        foreach (var helpEntry in SlashCommandCatalog.HelpEntries)
        {
            var commandToken = helpEntry.Usage.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            Assert.True(
                SlashCommandCatalog.TryResolveDispatch(commandToken, out _),
                $"Help entry has no dispatch binding: {helpEntry.Usage}");
        }
    }

    [Fact]
    public void CompletionEntries_AllResolveToDispatchableRootCommands()
    {
        foreach (var completion in SlashCommandCatalog.CompletionEntries)
        {
            var commandToken = completion.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            Assert.True(
                SlashCommandCatalog.TryResolveDispatch(commandToken, out _),
                $"Completion entry has no dispatch binding: {completion.Command}");
        }
    }
}
