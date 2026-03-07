using FluentAssertions;
using JD.AI.Core.Mcp;
using JD.SemanticKernel.Extensions.Mcp;
using JD.SemanticKernel.Extensions.Mcp.Registry;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class McpIntegrationSteps
{
    private readonly ScenarioContext _context;

    public McpIntegrationSteps(ScenarioContext context) => _context = context;

    [Given(@"an MCP manager with a mock registry containing servers:")]
    public void GivenAnMcpManagerWithMockRegistryContainingServers(Table table)
    {
        var servers = table.Rows.Select(r =>
            new McpServerDefinition(
                name: r["name"],
                displayName: r["name"],
                transport: McpTransportType.Stdio,
                scope: McpScope.User,
                sourceProvider: "test",
                sourcePath: "/test",
                command: r["command"],
                isEnabled: true)
        ).ToList();

        var registry = Substitute.For<IMcpRegistry>();
        registry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<McpServerDefinition>>(servers));

        var manager = new McpManager(registry);
        _context.Set(manager, "McpManager");
        _context.Set(servers, "McpServers");
    }

    [Given(@"an MCP manager with a mock registry")]
    public void GivenAnMcpManagerWithMockRegistry()
    {
        var registry = Substitute.For<IMcpRegistry>();
        registry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<McpServerDefinition>>([]));

        var manager = new McpManager(registry);
        _context.Set(manager, "McpManager");
    }

    [Given(@"an MCP manager with a writable provider")]
    public void GivenAnMcpManagerWithWritableProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jdai-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "jdai.mcp.json");
        var provider = new JdAiMcpDiscoveryProvider(configPath);

        var registry = Substitute.For<IMcpRegistry>();
        registry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<McpServerDefinition>>([]));

        var manager = new McpManager(registry, provider);
        _context.Set(manager, "McpManager");
        _context.Set(provider, "McpProvider");
        _context.Set(tempDir, "McpTempDir");
    }

    [Given(@"an MCP manager with a writable provider containing ""(.*)""")]
    public async Task GivenAnMcpManagerWithWritableProviderContaining(string serverName)
    {
        GivenAnMcpManagerWithWritableProvider();
        var manager = _context.Get<McpManager>("McpManager");
        var server = new McpServerDefinition(
            name: serverName,
            displayName: serverName,
            transport: McpTransportType.Stdio,
            scope: McpScope.User,
            sourceProvider: "jd-ai",
            sourcePath: "/test",
            command: "npx old-tool",
            isEnabled: true);
        await manager.AddOrUpdateAsync(server);
    }

    [When(@"I list all MCP servers")]
    public async Task WhenIListAllMcpServers()
    {
        var manager = _context.Get<McpManager>("McpManager");
        var servers = await manager.GetAllServersAsync();
        _context.Set(servers, "ListedServers");
    }

    [When(@"I add an MCP server ""(.*)"" with command ""(.*)""")]
    public async Task WhenIAddAnMcpServer(string name, string command)
    {
        var manager = _context.Get<McpManager>("McpManager");
        var server = new McpServerDefinition(
            name: name,
            displayName: name,
            transport: McpTransportType.Stdio,
            scope: McpScope.User,
            sourceProvider: "jd-ai",
            sourcePath: "/test",
            command: command,
            isEnabled: true);
        await manager.AddOrUpdateAsync(server);
    }

    [When(@"I remove MCP server ""(.*)""")]
    public async Task WhenIRemoveMcpServer(string name)
    {
        var manager = _context.Get<McpManager>("McpManager");
        await manager.RemoveAsync(name);
    }

    [When(@"I get the status of ""(.*)""")]
    public void WhenIGetTheStatusOf(string serverName)
    {
        var manager = _context.Get<McpManager>("McpManager");
        var status = manager.GetStatus(serverName);
        _context.Set(status, "McpStatus");
    }

    [When(@"I set the status of ""(.*)"" to connected")]
    public void WhenISetTheStatusToConnected(string serverName)
    {
        var manager = _context.Get<McpManager>("McpManager");
        manager.SetStatus(serverName, McpServerStatus.Default with { State = McpConnectionState.Connected });
    }

    [Then(@"I should see (\d+) servers")]
    public void ThenIShouldSeeServers(int count)
    {
        var servers = _context.Get<IReadOnlyList<McpServerDefinition>>("ListedServers");
        servers.Should().HaveCount(count);
    }

    [Then(@"the server ""(.*)"" should be in the configuration")]
    public async Task ThenTheServerShouldBeInTheConfiguration(string serverName)
    {
        var provider = _context.Get<JdAiMcpDiscoveryProvider>("McpProvider");
        var servers = await provider.DiscoverAsync();
        servers.Should().Contain(s => s.Name == serverName);
    }

    [Then(@"the server ""(.*)"" should not be in the configuration")]
    public async Task ThenTheServerShouldNotBeInTheConfiguration(string serverName)
    {
        var provider = _context.Get<JdAiMcpDiscoveryProvider>("McpProvider");
        var servers = await provider.DiscoverAsync();
        servers.Should().NotContain(s => s.Name == serverName);
    }

    [Then(@"the status should be the default status")]
    public void ThenTheStatusShouldBeTheDefaultStatus()
    {
        var status = _context.Get<McpServerStatus>("McpStatus");
        status.Should().Be(McpServerStatus.Default);
    }

    [Then(@"the status should be connected")]
    public void ThenTheStatusShouldBeConnected()
    {
        var status = _context.Get<McpServerStatus>("McpStatus");
        status.State.Should().Be(McpConnectionState.Connected);
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_context.TryGetValue("McpTempDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
