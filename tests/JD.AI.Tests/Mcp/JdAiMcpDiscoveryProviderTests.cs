using JD.AI.Core.Mcp;
using JD.SemanticKernel.Extensions.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class JdAiMcpDiscoveryProviderTests
{
    private static string TempConfigPath() =>
        Path.Combine(Path.GetTempPath(), $"jdai-mcp-{Guid.NewGuid():N}.json");

    private static McpServerDefinition MakeServer(
        string name,
        McpTransportType transport = McpTransportType.Stdio,
        string? command = null,
        IReadOnlyList<string>? args = null,
        Uri? url = null,
        bool isEnabled = true)
        => new(
            name: name,
            displayName: name,
            transport: transport,
            scope: McpScope.User,
            sourceProvider: "jd-ai",
            sourcePath: null,
            url: url,
            command: command,
            args: args,
            env: null,
            isEnabled: isEnabled);

    [Fact]
    public async Task DiscoverAsync_ReturnsEmptyWhenFileAbsent()
    {
        var provider = new JdAiMcpDiscoveryProvider(TempConfigPath());
        var results = await provider.DiscoverAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task AddOrUpdate_PersistsNewServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var server = MakeServer("test-server", McpTransportType.Stdio, "npx", ["-y", "some-package"]);

            await provider.AddOrUpdateAsync(server);
            var results = await provider.DiscoverAsync();

            Assert.Single(results);
            Assert.Equal("test-server", results[0].Name);
            Assert.Equal("npx", results[0].Command);
            Assert.Equal(["-y", "some-package"], results[0].Args!.ToList());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AddOrUpdate_OverwritesExistingServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);

            var original = MakeServer("server1", McpTransportType.Stdio, "old-cmd");
            await provider.AddOrUpdateAsync(original);

            var updated = MakeServer("server1", McpTransportType.Stdio, "new-cmd");
            await provider.AddOrUpdateAsync(updated);

            var results = await provider.DiscoverAsync();
            Assert.Single(results);
            Assert.Equal("new-cmd", results[0].Command);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Remove_DeletesExistingServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var server = MakeServer("to-remove");
            await provider.AddOrUpdateAsync(server);

            await provider.RemoveAsync("to-remove");

            var results = await provider.DiscoverAsync();
            Assert.Empty(results);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Remove_IsNoOpForNonExistentServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var server = MakeServer("keeper");
            await provider.AddOrUpdateAsync(server);

            // Should not throw or modify anything
            await provider.RemoveAsync("nonexistent");

            var results = await provider.DiscoverAsync();
            Assert.Single(results);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetEnabled_DisablesServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var server = MakeServer("svc", isEnabled: true);
            await provider.AddOrUpdateAsync(server);

            await provider.SetEnabledAsync("svc", false);
            var results = await provider.DiscoverAsync();

            Assert.False(results[0].IsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetEnabled_EnablesDisabledServer()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var server = MakeServer("svc", isEnabled: false);
            await provider.AddOrUpdateAsync(server);

            await provider.SetEnabledAsync("svc", true);
            var results = await provider.DiscoverAsync();

            Assert.True(results[0].IsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Discover_SetsCorrectSourceMetadata()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            await provider.AddOrUpdateAsync(MakeServer("s"));

            var results = await provider.DiscoverAsync();
            Assert.Equal("jd-ai", results[0].SourceProvider);
            Assert.Equal(McpScope.User, results[0].Scope);
            Assert.Equal(path, results[0].SourcePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetEnabledAsync_NoOp_WhenNameNotFound()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            await provider.AddOrUpdateAsync(MakeServer("server", isEnabled: true));

            // Should not throw and should not change the file
            await provider.SetEnabledAsync("nonexistent", false);

            var results = await provider.DiscoverAsync();
            Assert.True(results[0].IsEnabled); // unchanged
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetEnabledAsync_NoOp_WhenAlreadyInDesiredState()
    {
        var path = TempConfigPath();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            await provider.AddOrUpdateAsync(MakeServer("server", isEnabled: true));

            var modifiedBefore = File.GetLastWriteTimeUtc(path);
            await Task.Delay(10); // ensure time advances

            // Calling with the already-current value should be a true no-op (no file write)
            await provider.SetEnabledAsync("server", true);

            var modifiedAfter = File.GetLastWriteTimeUtc(path);
            Assert.Equal(modifiedBefore, modifiedAfter);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
