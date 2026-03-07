using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

/// <summary>
/// Tests for the curated MCP catalog data model and static registry.
/// </summary>
public sealed class CuratedMcpCatalogTests
{
    // ── Catalog structure ─────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAtLeastSeventeenEntries()
    {
        Assert.True(CuratedMcpCatalog.All.Count >= 17,
            $"Expected ≥17 entries, got {CuratedMcpCatalog.All.Count}");
    }

    [Fact]
    public void All_AllEntriesHaveNonEmptyId()
    {
        foreach (var entry in CuratedMcpCatalog.All)
            Assert.False(string.IsNullOrWhiteSpace(entry.Id),
                $"Entry with DisplayName='{entry.DisplayName}' has empty Id");
    }

    [Fact]
    public void All_AllEntriesHaveNonEmptyDisplayName()
    {
        foreach (var entry in CuratedMcpCatalog.All)
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName),
                $"Entry with Id='{entry.Id}' has empty DisplayName");
    }

    [Fact]
    public void All_AllEntriesHaveNonEmptyCategory()
    {
        foreach (var entry in CuratedMcpCatalog.All)
            Assert.False(string.IsNullOrWhiteSpace(entry.Category),
                $"Entry '{entry.Id}' has empty Category");
    }

    [Fact]
    public void All_AllEntriesHaveNonEmptyDescription()
    {
        foreach (var entry in CuratedMcpCatalog.All)
            Assert.False(string.IsNullOrWhiteSpace(entry.Description),
                $"Entry '{entry.Id}' has empty Description");
    }

    [Fact]
    public void All_AllIdsAreUnique()
    {
        var ids = CuratedMcpCatalog.All.Select(e => e.Id).ToList();
        var duplicates = ids.GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void All_StdioEntriesHaveCommand()
    {
        foreach (var entry in CuratedMcpCatalog.All.Where(e => e.Transport == CuratedMcpTransport.Stdio))
            Assert.False(string.IsNullOrWhiteSpace(entry.Command),
                $"Stdio entry '{entry.Id}' has no Command");
    }

    [Fact]
    public void All_HttpEntriesHaveUrl()
    {
        foreach (var entry in CuratedMcpCatalog.All.Where(e => e.Transport == CuratedMcpTransport.Http))
            Assert.False(string.IsNullOrWhiteSpace(entry.Url),
                $"HTTP entry '{entry.Id}' has no Url");
    }

    [Fact]
    public void All_HttpEntriesHaveAbsoluteUrl()
    {
        foreach (var entry in CuratedMcpCatalog.All.Where(e => e.Transport == CuratedMcpTransport.Http))
        {
            Assert.True(Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri),
                $"HTTP entry '{entry.Id}' has invalid Url: '{entry.Url}'");
            Assert.True(uri!.Scheme is "http" or "https",
                $"HTTP entry '{entry.Id}' URL scheme must be http/https, got: '{uri.Scheme}'");
        }
    }

    // ── Required categories ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Source Control")]
    [InlineData("Developer Tools")]
    [InlineData("Windows Desktop")]
    [InlineData("Productivity")]
    [InlineData("Search & AI")]
    [InlineData("Databases")]
    [InlineData("Cloud & Infra")]
    public void All_ContainsCategory(string category)
    {
        Assert.Contains(CuratedMcpCatalog.All, e =>
            string.Equals(e.Category, category, StringComparison.Ordinal));
    }

    // ── Specific well-known entries ───────────────────────────────────────────

    [Theory]
    [InlineData("github")]
    [InlineData("git")]
    [InlineData("azure-devops")]
    [InlineData("windows-mcp")]
    [InlineData("filesystem")]
    [InlineData("fetch")]
    [InlineData("memory")]
    [InlineData("brave-search")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void All_ContainsExpectedEntry(string id)
    {
        Assert.Contains(CuratedMcpCatalog.All, e =>
            string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    // ── Env var metadata ──────────────────────────────────────────────────────

    [Fact]
    public void AzureDevOps_HasRequiredEnvVarAndArgPrompt()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "azure-devops", StringComparison.Ordinal));

        Assert.NotNull(entry.RequiredEnvVars);
        Assert.Contains(entry.RequiredEnvVars, v =>
            string.Equals(v.Name, "AZURE_DEVOPS_PAT", StringComparison.Ordinal));

        Assert.NotNull(entry.PromptArgs);
        Assert.Contains(entry.PromptArgs, a =>
            string.Equals(a.Placeholder, "organization", StringComparison.Ordinal));
    }

    [Fact]
    public void AzureDevOps_DefaultArgsContainOrgPlaceholder()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "azure-devops", StringComparison.Ordinal));

        Assert.NotNull(entry.DefaultArgs);
        Assert.Contains("{organization}", entry.DefaultArgs);
    }

    [Fact]
    public void BraveSearch_HasApiKeyEnvVar()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "brave-search", StringComparison.Ordinal));

        Assert.NotNull(entry.RequiredEnvVars);
        Assert.Contains(entry.RequiredEnvVars, v =>
            string.Equals(v.Name, "BRAVE_API_KEY", StringComparison.Ordinal));
        Assert.True(entry.RequiredEnvVars.First(v => string.Equals(v.Name, "BRAVE_API_KEY", StringComparison.Ordinal)).IsSecret);
    }

    [Fact]
    public void Notion_IsHttpTransportWithUrl()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "notion", StringComparison.Ordinal));

        Assert.Equal(CuratedMcpTransport.Http, entry.Transport);
        Assert.Equal("https://mcp.notion.com/mcp", entry.Url);
    }

    [Fact]
    public void GitHub_IsHttpTransport()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "github", StringComparison.Ordinal));

        Assert.Equal(CuratedMcpTransport.Http, entry.Transport);
        Assert.False(string.IsNullOrEmpty(entry.Url));
    }

    [Fact]
    public void WindowsMcp_IsStdioWithUvx()
    {
        var entry = CuratedMcpCatalog.All.First(e => string.Equals(e.Id, "windows-mcp", StringComparison.Ordinal));

        Assert.Equal(CuratedMcpTransport.Stdio, entry.Transport);
        Assert.Equal("uvx", entry.Command);
        Assert.Contains("windows-mcp", entry.DefaultArgs!);
    }

    // ── CuratedMcpEntry record equality ──────────────────────────────────────

    [Fact]
    public void CuratedMcpEntry_RecordEquality_ById()
    {
        var a = new CuratedMcpEntry("github", "GitHub", "Source Control", "desc",
            CuratedMcpTransport.Http, Url: "https://example.com");
        var b = new CuratedMcpEntry("github", "GitHub", "Source Control", "desc",
            CuratedMcpTransport.Http, Url: "https://example.com");

        Assert.Equal(a, b);
    }

    // ── CuratedMcpEnvVar ──────────────────────────────────────────────────────

    [Fact]
    public void CuratedMcpEnvVar_DefaultIsSecret()
    {
        var v = new CuratedMcpEnvVar("MY_KEY", "Enter key");
        Assert.True(v.IsSecret);
    }

    [Fact]
    public void CuratedMcpEnvVar_CanBeNonSecret()
    {
        var v = new CuratedMcpEnvVar("TEAM_ID", "Enter team ID", IsSecret: false);
        Assert.False(v.IsSecret);
    }

    // ── CuratedMcpArgPrompt ───────────────────────────────────────────────────

    [Fact]
    public void CuratedMcpArgPrompt_ExampleIsOptional()
    {
        var p = new CuratedMcpArgPrompt("org", "Organization name");
        Assert.Null(p.Example);
    }

    [Fact]
    public void CuratedMcpArgPrompt_ExampleCanBeSet()
    {
        var p = new CuratedMcpArgPrompt("org", "Organization name", Example: "contoso");
        Assert.Equal("contoso", p.Example);
    }
}
