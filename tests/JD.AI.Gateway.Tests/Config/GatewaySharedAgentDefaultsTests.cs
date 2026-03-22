using JD.AI.Core.Config;
using JD.AI.Gateway.Config;

namespace JD.AI.Gateway.Tests.Config;

public sealed class GatewaySharedAgentDefaultsTests
{
    [Fact]
    public async Task ApplyAsync_WhenPreferenceExists_UpdatesConfiguredDefaultAgent()
    {
        var configPath = BuildTempConfigPath();
        try
        {
            using var store = new AtomicConfigStore(configPath);
            await store.SetGatewayDefaultAgentAsync("OpenAI Codex", "gpt-5.3-codex");

            var gatewayConfig = new GatewayConfig
            {
                Agents =
                [
                    new AgentDefinition { Id = "default", Provider = "ollama", Model = "qwen3.5:27b", AutoSpawn = true }
                ],
                Routing = new RoutingConfig
                {
                    DefaultAgentId = "default",
                }
            };

            await GatewaySharedAgentDefaults.ApplyAsync(gatewayConfig, store);

            Assert.Single(gatewayConfig.Agents);
            Assert.Equal("OpenAI Codex", gatewayConfig.Agents[0].Provider);
            Assert.Equal("gpt-5.3-codex", gatewayConfig.Agents[0].Model);
            Assert.Equal("default", gatewayConfig.Routing.DefaultAgentId);
        }
        finally
        {
            CleanupTempConfig(configPath);
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenNoAgentsConfigured_CreatesDefaultAgent()
    {
        var configPath = BuildTempConfigPath();
        try
        {
            using var store = new AtomicConfigStore(configPath);
            await store.SetGatewayDefaultAgentAsync("Mistral", "mistral-large-latest");

            var gatewayConfig = new GatewayConfig
            {
                Agents = [],
                Routing = new RoutingConfig(),
            };

            await GatewaySharedAgentDefaults.ApplyAsync(gatewayConfig, store);

            Assert.Single(gatewayConfig.Agents);
            Assert.Equal("default", gatewayConfig.Agents[0].Id);
            Assert.Equal("Mistral", gatewayConfig.Agents[0].Provider);
            Assert.Equal("mistral-large-latest", gatewayConfig.Agents[0].Model);
            Assert.Equal("default", gatewayConfig.Routing.DefaultAgentId);
        }
        finally
        {
            CleanupTempConfig(configPath);
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenPreferenceMissing_DoesNotMutateAgents()
    {
        var configPath = BuildTempConfigPath();
        try
        {
            using var store = new AtomicConfigStore(configPath);

            var gatewayConfig = new GatewayConfig
            {
                Agents =
                [
                    new AgentDefinition { Id = "default", Provider = "OpenAI Codex", Model = "gpt-5.1-codex-mini" }
                ],
                Routing = new RoutingConfig { DefaultAgentId = "default" }
            };

            await GatewaySharedAgentDefaults.ApplyAsync(gatewayConfig, store);

            Assert.Single(gatewayConfig.Agents);
            Assert.Equal("OpenAI Codex", gatewayConfig.Agents[0].Provider);
            Assert.Equal("gpt-5.1-codex-mini", gatewayConfig.Agents[0].Model);
        }
        finally
        {
            CleanupTempConfig(configPath);
        }
    }

    private static string BuildTempConfigPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"jdai-gateway-shared-defaults-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    private static void CleanupTempConfig(string configPath)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;

        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for test temp files.
        }
    }
}
