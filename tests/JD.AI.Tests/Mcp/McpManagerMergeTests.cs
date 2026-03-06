using JD.AI.Core.Mcp;
using JD.SemanticKernel.Extensions.Mcp;
using JD.SemanticKernel.Extensions.Mcp.Registry;
using NSubstitute;

namespace JD.AI.Tests.Mcp;

public sealed class McpManagerMergeTests
{
    [Fact]
    public void GetStatus_UnknownServerReturnsDefault()
    {
        var manager = new McpManager(new McpRegistry([]), null);
        var status = manager.GetStatus("nonexistent");
        Assert.Equal(McpConnectionState.Unknown, status.State);
    }

    [Fact]
    public void SetStatus_RoundTrips()
    {
        var manager = new McpManager(new McpRegistry([]), null);
        var status = new McpServerStatus
        {
            State = McpConnectionState.Connected,
            LastErrorSummary = null,
        };

        manager.SetStatus("myserver", status);
        var retrieved = manager.GetStatus("myserver");

        Assert.Equal(McpConnectionState.Connected, retrieved.State);
    }

    // ── GetImportCandidatesAsync ──────────────────────────────────────────────

    private static McpServerDefinition MakeServer(
        string name,
        string sourceProvider = "external",
        McpTransportType transport = McpTransportType.Stdio)
        => new(
            name: name,
            displayName: name,
            transport: transport,
            scope: McpScope.User,
            sourceProvider: sourceProvider,
            sourcePath: null,
            url: null,
            command: "cmd",
            args: null,
            env: null,
            isEnabled: true);

    private static IMcpRegistry MockRegistry(params McpServerDefinition[] servers)
    {
        var registry = Substitute.For<IMcpRegistry>();
        registry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<McpServerDefinition>>(servers));
        return registry;
    }

    [Fact]
    public async Task GetImportCandidates_WhenNoJdAiProvider_ReturnsAllExternalServers()
    {
        var externalRegistry = MockRegistry(MakeServer("github"), MakeServer("gitlab"));
        var manager = new McpManager(new McpRegistry([]), jdAiProvider: null);

        var candidates = await manager.GetImportCandidatesAsync(externalRegistry);

        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public async Task GetImportCandidates_ExcludesServersAlreadyInJdAi()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"jdai-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(tempPath);
            var existing = MakeServer("github", "jd-ai");
            await jdAiProvider.AddOrUpdateAsync(existing);

            var externalRegistry = MockRegistry(
                MakeServer("github"),  // already in JD.AI
                MakeServer("gitlab")); // not in JD.AI

            var manager = new McpManager(new McpRegistry([jdAiProvider]), jdAiProvider);
            var candidates = await manager.GetImportCandidatesAsync(externalRegistry);

            Assert.Single(candidates);
            Assert.Equal("gitlab", candidates[0].Name);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task GetImportCandidates_CaseInsensitiveNameComparison()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"jdai-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(tempPath);
            var existing = MakeServer("GitHub", "jd-ai");
            await jdAiProvider.AddOrUpdateAsync(existing);

            var externalRegistry = MockRegistry(
                MakeServer("github"),  // same name, different case
                MakeServer("gitlab"));

            var manager = new McpManager(new McpRegistry([jdAiProvider]), jdAiProvider);
            var candidates = await manager.GetImportCandidatesAsync(externalRegistry);

            Assert.Single(candidates);
            Assert.Equal("gitlab", candidates[0].Name);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task GetImportCandidates_WhenExternalRegistryEmpty_ReturnsEmpty()
    {
        var externalRegistry = MockRegistry();
        var manager = new McpManager(new McpRegistry([]), jdAiProvider: null);

        var candidates = await manager.GetImportCandidatesAsync(externalRegistry);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task GetImportCandidates_WhenAllAlreadyImported_ReturnsEmpty()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"jdai-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var jdAiProvider = new JdAiMcpDiscoveryProvider(tempPath);
            await jdAiProvider.AddOrUpdateAsync(MakeServer("github", "jd-ai"));
            await jdAiProvider.AddOrUpdateAsync(MakeServer("gitlab", "jd-ai"));

            var externalRegistry = MockRegistry(MakeServer("github"), MakeServer("gitlab"));
            var manager = new McpManager(new McpRegistry([jdAiProvider]), jdAiProvider);

            var candidates = await manager.GetImportCandidatesAsync(externalRegistry);

            Assert.Empty(candidates);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
