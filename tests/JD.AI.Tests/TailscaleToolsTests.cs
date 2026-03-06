using System.Text.Json;
using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class TailscaleToolsTests : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private static readonly string[] s_tagDev = ["tag:dev"];
    private static readonly string[] s_tagServer = ["tag:server"];
    private static readonly string[] s_runnerCapabilities = ["shell", "git", "file"];

    private readonly string _tempDir;

    public TailscaleToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-ts-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Status ──────────────────────────────────────────────

    [Fact]
    public void GetStatus_ReturnsHeaderAndSections()
    {
        var result = TailscaleTools.GetStatus(_tempDir);

        Assert.Contains("## Tailscale Status", result);
        Assert.Contains("CLI installed", result);
        Assert.Contains("API configured", result);
    }

    [Fact]
    public void GetStatus_WithoutApiConfig_ShowsApiNotConfigured()
    {
        var result = TailscaleTools.GetStatus(_tempDir);

        // API should always be not configured in temp dir
        Assert.Contains("**API configured**: ❌ No", result);
    }

    [Fact]
    public void GetStatus_WithApiConfig_ShowsTailnet()
    {
        WriteTailscaleConfig(_tempDir, "my-tailnet.ts.net", "api-key", "tskey-api-xxx");

        var result = TailscaleTools.GetStatus(_tempDir);

        Assert.Contains("✅ Yes", result);
        Assert.Contains("my-tailnet.ts.net", result);
    }

    [Fact]
    public void GetStatus_WithoutCliOrApi_ShowsGettingStarted()
    {
        // This test is only meaningful when Tailscale CLI is not installed
        if (TailscaleTools.IsTailscaleCliAvailable())
            return; // Skip — CLI is available on this machine

        var result = TailscaleTools.GetStatus(_tempDir);

        Assert.Contains("Getting Started", result);
        Assert.Contains("Install Tailscale", result);
    }

    // ── Machine Discovery ───────────────────────────────────

    [Fact]
    public void ListMachines_EmptyCache_ShowsTroubleshooting()
    {
        var result = TailscaleTools.ListMachinesInternal(null, null, _tempDir, useCli: false);

        Assert.Contains("No machines discovered", result);
        Assert.Contains("Troubleshooting", result);
    }

    [Fact]
    public void ListMachines_WithCachedMachines_ShowsTable()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("workstation", "linux", true, "100.64.0.1", s_tagDev, false),
            ("homelab", "linux", true, "100.64.0.2", s_tagServer, true),
            ("laptop", "windows", false, "100.64.0.3", Array.Empty<string>(), false),
        });

        var result = TailscaleTools.ListMachinesInternal(null, null, _tempDir, useCli: false);

        Assert.Contains("## Tailnet Machines", result);
        Assert.Contains("workstation", result);
        Assert.Contains("homelab", result);
        Assert.Contains("laptop", result);
        Assert.Contains("🟢 Online", result);
        Assert.Contains("🔴 Offline", result);
        Assert.Contains("3 machine(s)", result);
        Assert.Contains("2 online", result);
    }

    [Fact]
    public void ListMachines_OnlineFilter_FiltersCorrectly()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("workstation", "linux", true, "100.64.0.1", Array.Empty<string>(), false),
            ("laptop", "windows", false, "100.64.0.3", Array.Empty<string>(), false),
        });

        var result = TailscaleTools.ListMachinesInternal("online", null, _tempDir, useCli: false);

        Assert.Contains("workstation", result);
        Assert.Contains("1 machine(s)", result);
    }

    [Fact]
    public void ListMachines_OfflineFilter_FiltersCorrectly()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("workstation", "linux", true, "100.64.0.1", Array.Empty<string>(), false),
            ("laptop", "windows", false, "100.64.0.3", Array.Empty<string>(), false),
        });

        var result = TailscaleTools.ListMachinesInternal("offline", null, _tempDir, useCli: false);

        Assert.Contains("laptop", result);
        Assert.Contains("1 machine(s)", result);
    }

    [Fact]
    public void ListMachines_TagFilter_FiltersCorrectly()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("workstation", "linux", true, "100.64.0.1", s_tagDev, false),
            ("homelab", "linux", true, "100.64.0.2", s_tagServer, true),
        });

        var result = TailscaleTools.ListMachinesInternal(null, "tag:server", _tempDir, useCli: false);

        Assert.Contains("homelab", result);
        Assert.Contains("1 machine(s)", result);
    }

    [Fact]
    public void ListMachines_WithRunner_ShowsRunnerIndicator()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("homelab", "linux", true, "100.64.0.2", Array.Empty<string>(), true),
        });

        var result = TailscaleTools.ListMachinesInternal(null, null, _tempDir, useCli: false);

        Assert.Contains("✅", result);
        Assert.Contains("jdai-runner", result);
    }

    // ── Configure ───────────────────────────────────────────

    [Fact]
    public void Configure_ApiKey_SavesConfig()
    {
        var result = TailscaleTools.Configure(
            tailnet: "example.ts.net",
            authMethod: "api-key",
            credential: "tskey-api-test123",
            configDir: _tempDir);

        Assert.Contains("✅ Tailscale API credentials saved", result);
        Assert.Contains("example.ts.net", result);
        Assert.Contains("api-key", result);

        // Verify file was written
        var configPath = Path.Combine(_tempDir, "tailscale.json");
        Assert.True(File.Exists(configPath));

        var saved = File.ReadAllText(configPath);
        Assert.Contains("example.ts.net", saved);
    }

    [Fact]
    public void Configure_OAuth_SavesConfig()
    {
        var result = TailscaleTools.Configure(
            tailnet: "org.ts.net",
            authMethod: "oauth",
            credential: "client-id-123",
            clientSecret: "client-secret-456",
            configDir: _tempDir);

        Assert.Contains("✅ Tailscale API credentials saved", result);
        Assert.Contains("org.ts.net", result);
    }

    [Fact]
    public void Configure_OAuth_WithoutSecret_ReturnsError()
    {
        var result = TailscaleTools.Configure(
            tailnet: "org.ts.net",
            authMethod: "oauth",
            credential: "client-id-123",
            configDir: _tempDir);

        Assert.Contains("Error: OAuth auth method requires", result);
    }

    [Fact]
    public void Configure_InvalidAuthMethod_ReturnsError()
    {
        var result = TailscaleTools.Configure(
            tailnet: "example.ts.net",
            authMethod: "invalid",
            credential: "key",
            configDir: _tempDir);

        Assert.Contains("Error: Invalid auth method", result);
    }

    [Fact]
    public void Configure_ShowsSecurityWarning()
    {
        var result = TailscaleTools.Configure(
            tailnet: "example.ts.net",
            authMethod: "api-key",
            credential: "tskey-api-test123",
            configDir: _tempDir);

        Assert.Contains("Security", result);
        Assert.Contains("TAILSCALE_API_KEY", result);
    }

    // ── Runner Probe ────────────────────────────────────────

    [Fact]
    public void ProbeRunner_ReturnsTargetInfo()
    {
        var result = TailscaleTools.ProbeRunner("homelab", configDir: _tempDir);

        Assert.Contains("## Runner Probe: homelab", result);
        Assert.Contains("homelab", result);
        Assert.Contains("18789", result);
    }

    [Fact]
    public void ProbeRunner_CustomPort_UsesPort()
    {
        var result = TailscaleTools.ProbeRunner("homelab", port: 9999, configDir: _tempDir);

        Assert.Contains("9999", result);
    }

    [Fact]
    public void ProbeRunner_WithCachedRunnerInfo_ShowsDetails()
    {
        WriteRunnerCache(_tempDir, "homelab", "1.0.0", s_runnerCapabilities);

        var result = TailscaleTools.ProbeRunner("homelab", configDir: _tempDir);

        Assert.Contains("Cached Runner Info", result);
        Assert.Contains("1.0.0", result);
    }

    [Fact]
    public void ProbeRunner_ShowsBootstrapInstructions()
    {
        var result = TailscaleTools.ProbeRunner("newhost", configDir: _tempDir);

        Assert.Contains("Bootstrap Runner", result);
        Assert.Contains("start-runner", result);
    }

    // ── Export ───────────────────────────────────────────────

    [Fact]
    public void Export_ReturnsValidJson()
    {
        var result = TailscaleTools.Export(_tempDir);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
        Assert.True(doc.RootElement.TryGetProperty("cli", out _));
        Assert.True(doc.RootElement.TryGetProperty("api", out _));
        Assert.True(doc.RootElement.TryGetProperty("machines", out _));
        Assert.True(doc.RootElement.TryGetProperty("summary", out _));
    }

    [Fact]
    public void Export_WithApiConfig_IncludesTailnet()
    {
        WriteTailscaleConfig(_tempDir, "test.ts.net", "api-key", "tskey-test");

        var result = TailscaleTools.Export(_tempDir);
        var doc = JsonDocument.Parse(result);

        var api = doc.RootElement.GetProperty("api");
        Assert.True(api.GetProperty("configured").GetBoolean());
        Assert.Equal("test.ts.net", api.GetProperty("tailnet").GetString());
    }

    [Fact]
    public void Export_WithCachedMachines_IncludesSummary()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("host1", "linux", true, "100.64.0.1", Array.Empty<string>(), false),
            ("host2", "windows", false, "100.64.0.2", Array.Empty<string>(), true),
        });

        var result = TailscaleTools.ExportInternal(_tempDir, useCli: false);
        var doc = JsonDocument.Parse(result);

        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("totalMachines").GetInt32());
        Assert.Equal(1, summary.GetProperty("online").GetInt32());
        Assert.Equal(1, summary.GetProperty("offline").GetInt32());
        Assert.Equal(1, summary.GetProperty("withRunner").GetInt32());
    }

    // ── LoadApiConfig ───────────────────────────────────────

    [Fact]
    public void LoadApiConfig_NoConfig_ReturnsNull()
    {
        var config = TailscaleTools.LoadApiConfig(_tempDir);
        Assert.Null(config);
    }

    [Fact]
    public void LoadApiConfig_FromFile_ReturnsConfig()
    {
        WriteTailscaleConfig(_tempDir, "file.ts.net", "api-key", "tskey-file");

        var config = TailscaleTools.LoadApiConfig(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("file.ts.net", config.Tailnet);
        Assert.Equal("api-key", config.AuthMethod);
    }

    [Fact]
    public void LoadApiConfig_MalformedJson_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tailscale.json"), "{ invalid json }}}");

        var config = TailscaleTools.LoadApiConfig(_tempDir);

        Assert.Null(config);
    }

    // ── DiscoverMachines ────────────────────────────────────

    [Fact]
    public void DiscoverMachines_EmptyDir_ReturnsEmpty()
    {
        var machines = TailscaleTools.DiscoverMachines(_tempDir, useCli: false);
        Assert.Empty(machines);
    }

    [Fact]
    public void DiscoverMachines_WithCache_ReturnsMachines()
    {
        WriteMachineCache(_tempDir, new[]
        {
            ("host1", "linux", true, "100.64.0.1", Array.Empty<string>(), false),
        });

        var machines = TailscaleTools.DiscoverMachines(_tempDir, useCli: false);

        Assert.Single(machines);
        Assert.Equal("host1", machines[0].Hostname);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static void WriteTailscaleConfig(string dir, string tailnet, string authMethod, string credential)
    {
        var config = new
        {
            Tailnet = tailnet,
            AuthMethod = authMethod,
            ApiKey = string.Equals(authMethod, "api-key", StringComparison.Ordinal) ? credential : null,
            OAuthClientId = string.Equals(authMethod, "oauth", StringComparison.Ordinal) ? credential : null,
            OAuthClientSecret = (string?)null,
        };
        File.WriteAllText(
            Path.Combine(dir, "tailscale.json"),
            JsonSerializer.Serialize(config, s_jsonOptions));
    }

    private static void WriteMachineCache(
        string dir,
        (string hostname, string os, bool online, string ip, string[] tags, bool hasRunner)[] machines)
    {
        var cache = new
        {
            machines = machines.Select(m => new
            {
                hostname = m.hostname,
                os = m.os,
                online = m.online,
                tailscaleIp = m.ip,
                tags = m.tags,
                hasRunner = m.hasRunner,
            }).ToArray(),
        };
        File.WriteAllText(
            Path.Combine(dir, "tailscale-machines.json"),
            JsonSerializer.Serialize(cache, s_jsonOptions));
    }

    private static void WriteRunnerCache(string dir, string hostname, string version, string[] capabilities)
    {
        var cache = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runners"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [hostname] = new
                {
                    version,
                    lastSeen = DateTime.UtcNow.ToString("o"),
                    capabilities,
                },
            },
        };
        File.WriteAllText(
            Path.Combine(dir, "tailscale-runners.json"),
            JsonSerializer.Serialize(cache, s_jsonOptions));
    }
}
