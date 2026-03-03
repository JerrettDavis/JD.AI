using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class JdAiMcpDiscoveryProviderTests
{
    private static string TempConfigPath() =>
        Path.Combine(Path.GetTempPath(), $"jdai-mcp-{Guid.NewGuid():N}.json");

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
            var server = new McpServerDefinition
            {
                Name = "test-server",
                Transport = McpTransport.Stdio,
                Command = "npx",
                Args = ["-y", "some-package"],
                IsEnabled = true,
            };

            await provider.AddOrUpdateAsync(server);
            var results = await provider.DiscoverAsync();

            Assert.Single(results);
            Assert.Equal("test-server", results[0].Name);
            Assert.Equal("npx", results[0].Command);
            Assert.Equal(["-y", "some-package"], results[0].Args.ToList());
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

            var original = new McpServerDefinition
            {
                Name = "server1", Transport = McpTransport.Stdio, Command = "old-cmd",
            };
            await provider.AddOrUpdateAsync(original);

            var updated = original with { Command = "new-cmd" };
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
            var server = new McpServerDefinition { Name = "to-remove" };
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
            var server = new McpServerDefinition { Name = "keeper" };
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
            var server = new McpServerDefinition { Name = "svc", IsEnabled = true };
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
            var server = new McpServerDefinition { Name = "svc", IsEnabled = false };
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
            await provider.AddOrUpdateAsync(new McpServerDefinition { Name = "s" });

            var results = await provider.DiscoverAsync();
            Assert.Equal("JD.AI", results[0].SourceProvider);
            Assert.Equal(McpScope.User, results[0].Scope);
            Assert.Equal(path, results[0].SourcePath);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
