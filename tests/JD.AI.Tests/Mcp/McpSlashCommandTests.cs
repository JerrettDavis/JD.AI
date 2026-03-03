using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Mcp;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Mcp;

public sealed class McpSlashCommandTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();
    private readonly AgentSession _session;

    public McpSlashCommandTests()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        _session = new AgentSession(_registry, kernel, model);
    }

    private SlashCommandRouter CreateRouter(McpManager? manager = null)
        => new(_session, _registry, mcpManager: manager);

    [Fact]
    public async Task McpList_NoServers_ShowsEmptyState()
    {
        // Use an isolated McpManager with no providers so no real files are read
        var emptyManager = new McpManager([], null);
        var router = CreateRouter(emptyManager);

        var result = await router.ExecuteAsync("/mcp list");

        Assert.NotNull(result);
        Assert.Contains("No MCP servers", result);
        Assert.Contains("/mcp add", result);
    }

    [Fact]
    public async Task McpList_WithServers_ShowsGroupedList()
    {
        var fakeProvider = new FakeMcpDiscoveryProvider(
        [
            new McpServerDefinition
            {
                Name = "test-server",
                Scope = McpScope.User,
                SourceProvider = "JD.AI",
                SourcePath = "/home/user/.jdai/mcp.json",
                Transport = McpTransport.Stdio,
                Command = "npx",
                IsEnabled = true,
            },
        ]);

        var manager = new McpManager([fakeProvider], null);
        var router = CreateRouter(manager);

        var result = await router.ExecuteAsync("/mcp list");

        Assert.NotNull(result);
        Assert.Contains("test-server", result);
        Assert.Contains("User MCPs", result);
    }

    [Fact]
    public async Task McpUnknownSubcommand_ShowsHelp()
    {
        var manager = new McpManager([], null);
        var router = CreateRouter(manager);

        var result = await router.ExecuteAsync("/mcp bogus");

        Assert.NotNull(result);
        Assert.Contains("/mcp", result);
        Assert.Contains("list", result);
    }

    [Fact]
    public async Task Help_IncludesMcpCommand()
    {
        var router = CreateRouter();
        var result = await router.ExecuteAsync("/help");

        Assert.NotNull(result);
        Assert.Contains("/mcp", result);
    }

    [Fact]
    public async Task McpAdd_MissingArgs_ReturnsUsage()
    {
        var manager = new McpManager([], null);
        var router = CreateRouter(manager);

        var result = await router.ExecuteAsync("/mcp add");

        Assert.NotNull(result);
        Assert.Contains("Usage", result);
    }

    [Fact]
    public async Task McpAdd_InvalidTransport_ReturnsError()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var manager = new McpManager([], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync("/mcp add myserver --transport ftp server.com");

            Assert.NotNull(result);
            Assert.Contains("Unknown transport", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task McpAdd_Http_AddsServer()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var manager = new McpManager([jdAiProvider], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync(
                "/mcp add notion --transport http https://mcp.notion.com/mcp");

            Assert.NotNull(result);
            Assert.Contains("notion", result);
            Assert.Contains("http", result);

            var servers = await manager.GetAllServersAsync();
            Assert.Contains(servers, s => string.Equals(s.Name, "notion", StringComparison.Ordinal) &&
                string.Equals(s.Url, "https://mcp.notion.com/mcp", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task McpAdd_Stdio_AddsServer()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var manager = new McpManager([jdAiProvider], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync(
                "/mcp add azure --transport stdio --command npx --args -y @azure/mcp");

            Assert.NotNull(result);
            Assert.Contains("azure", result);

            var servers = await manager.GetAllServersAsync();
            var azure = servers.FirstOrDefault(s => string.Equals(s.Name, "azure", StringComparison.Ordinal));
            Assert.NotNull(azure);
            Assert.Equal("npx", azure.Command);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task McpRemove_MissingName_ReturnsUsage()
    {
        var manager = new McpManager([], null);
        var router = CreateRouter(manager);

        var result = await router.ExecuteAsync("/mcp remove");

        Assert.NotNull(result);
        Assert.Contains("Usage", result);
    }

    [Fact]
    public async Task McpRemove_ExistingServer_Removes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var server = new McpServerDefinition { Name = "to-remove" };
            await jdAiProvider.AddOrUpdateAsync(server);

            var manager = new McpManager([jdAiProvider], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync("/mcp remove to-remove");
            Assert.NotNull(result);
            Assert.Contains("to-remove", result);

            var remaining = await manager.GetAllServersAsync();
            Assert.DoesNotContain(remaining, s => string.Equals(s.Name, "to-remove", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task McpDisable_DisablesServer()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var server = new McpServerDefinition { Name = "svc", IsEnabled = true };
            await jdAiProvider.AddOrUpdateAsync(server);

            var manager = new McpManager([jdAiProvider], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync("/mcp disable svc");

            Assert.NotNull(result);
            Assert.Contains("disabled", result);

            var servers = await manager.GetAllServersAsync();
            var svc = servers.FirstOrDefault(s => string.Equals(s.Name, "svc", StringComparison.Ordinal));
            Assert.NotNull(svc);
            Assert.False(svc.IsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task McpEnable_EnablesServer()
    {
        var path = Path.GetTempFileName();
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(path);
            var server = new McpServerDefinition { Name = "svc", IsEnabled = false };
            await jdAiProvider.AddOrUpdateAsync(server);

            var manager = new McpManager([jdAiProvider], jdAiProvider);
            var router = CreateRouter(manager);

            var result = await router.ExecuteAsync("/mcp enable svc");

            Assert.NotNull(result);
            Assert.Contains("enabled", result);

            var servers = await manager.GetAllServersAsync();
            Assert.True(servers.First(s => string.Equals(s.Name, "svc", StringComparison.Ordinal)).IsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Fake provider for testing ─────────────────────────────────────────────

    private sealed class FakeMcpDiscoveryProvider(IReadOnlyList<McpServerDefinition> servers)
        : IMcpDiscoveryProvider
    {
        public string SourceLabel => "Fake";
        public Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken ct = default)
            => Task.FromResult(servers);
    }
}
