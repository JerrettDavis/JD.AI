using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class ClaudeCodeUserMcpDiscoveryProviderTests
{
    private const string ValidClaudeJson = """
        {
          "mcpServers": {
            "azure-devops": {
              "type": "stdio",
              "command": "npx",
              "args": ["-y", "@azure-devops/mcp", "Quiktrip"],
              "env": { "MY_VAR": "val" }
            },
            "notion": {
              "type": "http",
              "url": "https://mcp.notion.com/mcp"
            }
          }
        }
        """;

    [Fact]
    public void ParseClaudeJson_ParsesStdioServer()
    {
        var results = ClaudeCodeUserMcpDiscoveryProvider.ParseClaudeJson(
            ValidClaudeJson, McpScope.User, "ClaudeCode", "/home/user/.claude.json");

        var stdio = results.First(r => string.Equals(r.Name, "azure-devops", StringComparison.Ordinal));
        Assert.Equal(McpTransport.Stdio, stdio.Transport);
        Assert.Equal("npx", stdio.Command);
        Assert.Equal(["-y", "@azure-devops/mcp", "Quiktrip"], stdio.Args);
        Assert.Equal("val", stdio.Env["MY_VAR"]);
        Assert.Equal(McpScope.User, stdio.Scope);
        Assert.Equal("ClaudeCode", stdio.SourceProvider);
        Assert.Equal("/home/user/.claude.json", stdio.SourcePath);
        Assert.True(stdio.IsEnabled);
    }

    [Fact]
    public void ParseClaudeJson_ParsesHttpServer()
    {
        var results = ClaudeCodeUserMcpDiscoveryProvider.ParseClaudeJson(
            ValidClaudeJson, McpScope.User, "ClaudeCode", null);

        var http = results.First(r => string.Equals(r.Name, "notion", StringComparison.Ordinal));
        Assert.Equal(McpTransport.Http, http.Transport);
        Assert.Equal("https://mcp.notion.com/mcp", http.Url);
        Assert.Null(http.Command);
    }

    [Fact]
    public void ParseClaudeJson_ReturnsEmptyWhenNoMcpServersKey()
    {
        var json = """{ "other": {} }""";
        var results = ClaudeCodeUserMcpDiscoveryProvider.ParseClaudeJson(
            json, McpScope.User, "ClaudeCode", null);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseClaudeJson_DefaultsToStdioWhenTypeAbsent()
    {
        var json = """
            {
              "mcpServers": {
                "no-type": {
                  "command": "python",
                  "args": ["server.py"]
                }
              }
            }
            """;
        var results = ClaudeCodeUserMcpDiscoveryProvider.ParseClaudeJson(
            json, McpScope.User, "ClaudeCode", null);

        Assert.Single(results);
        Assert.Equal(McpTransport.Stdio, results[0].Transport);
        Assert.Equal("python", results[0].Command);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsEmptyWhenFileAbsent()
    {
        var provider = new ClaudeCodeUserMcpDiscoveryProvider("/nonexistent/path/.claude.json");
        var results = await provider.DiscoverAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task DiscoverAsync_ParsesRealFile()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, ValidClaudeJson);
            var provider = new ClaudeCodeUserMcpDiscoveryProvider(tmp);
            var results = await provider.DiscoverAsync();
            Assert.Equal(2, results.Count);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void SourceLabel_ContainsPath()
    {
        var provider = new ClaudeCodeUserMcpDiscoveryProvider("/home/user/.claude.json");
        Assert.Contains("/home/user/.claude.json", provider.SourceLabel);
    }
}
