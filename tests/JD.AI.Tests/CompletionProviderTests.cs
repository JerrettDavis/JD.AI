using JD.AI.Rendering;

namespace JD.AI.Tests;

public sealed class CompletionProviderTests
{
    private static CompletionProvider BuildProvider()
    {
        var provider = new CompletionProvider();
        provider.Register("/help", "Show available commands");
        provider.Register("/models", "List available models");
        provider.Register("/model", "Switch to a model");
        provider.Register("/providers", "List detected providers");
        provider.Register("/provider", "Show current provider");
        provider.Register("/clear", "Clear chat history");
        provider.Register("/compact", "Force context compaction");
        provider.Register("/cost", "Show token usage");
        provider.Register("/autorun", "Toggle auto-approve");
        provider.Register("/permissions", "Toggle permission checks");
        provider.Register("/sessions", "List recent sessions");
        provider.Register("/resume", "Resume a previous session");
        provider.Register("/name", "Name the current session");
        provider.Register("/history", "Show session turn history");
        provider.Register("/export", "Export current session to JSON");
        provider.Register("/update", "Check for and apply updates");
        provider.Register("/instructions", "Show loaded project instructions");
        provider.Register("/checkpoint", "List, restore, or clear checkpoints");
        provider.Register("/sandbox", "Show or change sandbox mode");
        provider.Register("/review", "Review current code changes");
        provider.Register("/security-review", "Run a security-focused code review");
        provider.Register("/theme", "Set terminal theme");
        provider.Register("/vim", "Toggle vim editing mode");
        provider.Register("/stats", "Show session and historical stats");
        provider.Register("/config", "Manage settings");
        provider.Register("/agents", "Manage agent profiles");
        provider.Register("/hooks", "Manage hook profiles");
        provider.Register("/memory", "View or edit JDAI.md");
        provider.Register("/output-style", "Set output rendering style");
        provider.Register("/quit", "Exit jdai");
        provider.Register("/exit", "Exit jdai");
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
        Assert.Equal(31, results.Count);
    }

    [Fact]
    public void GetCompletions_SlashP_ReturnsPermissionsProviderAndProviders()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/p");
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => string.Equals(r.Text, "/permissions", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/provider", StringComparison.Ordinal));
        Assert.Contains(results, r => string.Equals(r.Text, "/providers", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_SlashMo_ReturnsModelAndModels()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/mo");
        Assert.Equal(2, results.Count);
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
        // /compact, /config, /cost
        Assert.Equal("/compact", results[0].Text);
        Assert.Equal("/config", results[1].Text);
        Assert.Equal("/cost", results[2].Text);
    }

    [Fact]
    public void GetCompletions_SlashC_Returns5Items()
    {
        var provider = BuildProvider();
        var results = provider.GetCompletions("/c");
        // /checkpoint, /clear, /compact, /config, /cost
        Assert.Equal(5, results.Count);
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
