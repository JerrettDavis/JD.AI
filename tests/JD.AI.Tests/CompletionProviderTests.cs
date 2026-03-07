using JD.AI.Commands;
using JD.AI.Rendering;

namespace JD.AI.Tests;

public sealed class CompletionProviderTests
{
    private static CompletionProvider BuildProvider()
    {
        var provider = new CompletionProvider();
        SlashCommandCatalog.RegisterCompletions(provider);
        return provider;
    }

    [Fact]
    public void GetCompletions_EmptyPrefix_ReturnsEmpty()
    {
        var provider = BuildProvider();
        Assert.Empty(provider.GetCompletions(""));
    }

    [Fact]
    public void GetCompletions_NullPrefix_ReturnsEmpty()
    {
        var provider = BuildProvider();
        Assert.Empty(provider.GetCompletions(null!));
    }

    [Fact]
    public void GetCompletions_SlashOnly_ReturnsAllCommands()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/");
        Assert.Equal(SlashCommandCatalog.CompletionEntries.Count, results.Count);
    }

    [Fact]
    public void GetCompletions_SlashP_ReturnsPermissionsProviderAndProviders()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/p");
        Assert.True(results.Count >= 3);
        Assert.Contains(results, r => string.Equals(r.Text, "/permissions", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/provider", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/providers", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_SlashMo_ReturnsModelAndModels()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/mo");
        Assert.True(results.Count >= 2);
        Assert.Contains(results, r => string.Equals(r.Text, "/model", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/models", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_ExactMatch_ReturnsEmpty()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/help");
        Assert.Empty(results);
    }

    [Fact]
    public void GetCompletions_CaseInsensitive()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/H");
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => string.Equals(r.Text, "/help", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/history", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/hooks", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_NoMatch_ReturnsEmpty()
    {
        var provider = BuildProvider();
        Assert.Empty(provider.GetCompletions("/xyz"));
    }

    [Fact]
    public void GetCompletions_IncludesDescription()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/he");
        Assert.Single(results);
        Assert.Equal("Show available commands", results[0].Description);
    }

    [Fact]
    public void GetCompletions_SlashHi_ReturnsHistory()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/hi");
        Assert.Single(results);
        Assert.Equal("/history", results[0].Text);
    }

    [Fact]
    public void GetCompletions_ResultsAreSorted()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/co");
        // /compact, /config, /context, /copy, /cost
        Assert.Equal("/compact", results[0].Text);
        Assert.Equal("/config", results[1].Text);
        Assert.Equal("/context", results[2].Text);
        Assert.Equal("/copy", results[3].Text);
        Assert.Equal("/cost", results[4].Text);
    }

    [Fact]
    public void GetCompletions_SlashC_Returns5Items()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/c");
        Assert.Contains(results, r => string.Equals(r.Text, "/checkpoint", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/clear", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/compact", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/config", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/cost", StringComparison.Ordinal));
    }

    [Fact]
    public void Register_AddsItem()
    {
        var provider = new CompletionProvider();
        Assert.Empty(provider.GetCompletions("/"));

        provider.Register("/test", "A test command");
        var results = provider.GetCompletions("/");
        Assert.Single(results);
        Assert.Equal("/test", results[0].Text);
        Assert.Equal("A test command", results[0].Description);
    }

    [Fact]
    public void CompletionItem_RecordEquality()
    {
        var a = new CompletionItem("/help", "Help");
        var b = new CompletionItem("/help", "Help");
        Assert.Equal(a, b);
    }

    [Fact]
    public void CompletionItem_NullDescription()
    {
        var item = new CompletionItem("/test", null);
        Assert.Null(item.Description);
    }
}
