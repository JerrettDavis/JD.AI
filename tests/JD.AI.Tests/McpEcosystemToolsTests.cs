using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public class McpEcosystemToolsTests : IDisposable
{
    private readonly string _tempDir;

    private static readonly string[] s_ghArgs = ["mcp"];

    public McpEcosystemToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-eco-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    // ── mcp_import_scan ──────────────────────────────────────

    [Fact]
    public void ImportScan_NoConfigs_ReportsEmpty()
    {
        var result = McpEcosystemTools.ImportScan(_tempDir);

        Assert.Contains("No MCP server definitions found", result);
    }

    [Fact]
    public void ImportScan_ClaudeJson_DiscoversServers()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                github = new { transport = "stdio", command = "gh", args = s_ghArgs },
                docs = new { transport = "sse", url = "https://docs.example.com/mcp" }
            }
        });

        var result = McpEcosystemTools.ImportScan(_tempDir);

        Assert.Contains("## MCP Import Scan", result);
        Assert.Contains("`github`", result);
        Assert.Contains("`docs`", result);
        Assert.Contains("Claude", result);
        Assert.Contains("**2** server(s)", result);
    }

    [Fact]
    public void ImportScan_JdaiConfig_DiscoversServers()
    {
        WriteJdaiConfig(new
        {
            servers = new
            {
                local = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.ImportScan(_tempDir);

        Assert.Contains("`local`", result);
        Assert.Contains("JD.AI", result);
    }

    [Fact]
    public void ImportScan_MultipleSources_DetectsDuplicates()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                github = new { transport = "stdio", command = "gh" }
            }
        });

        WriteJdaiConfig(new
        {
            servers = new
            {
                github = new { transport = "stdio", command = "gh" }
            }
        });

        var result = McpEcosystemTools.ImportScan(_tempDir);

        Assert.Contains("Duplicate Definitions", result);
        Assert.Contains("`github`", result);
    }

    // ── mcp_sync ─────────────────────────────────────────────

    [Fact]
    public void Sync_DryRun_NoChangesWritten()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                newserver = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.Sync("claude", dryRun: true, homeDir: _tempDir);

        Assert.Contains("Dry Run", result);
        Assert.Contains("➕ New Servers", result);
        Assert.Contains("`newserver`", result);

        // Verify nothing was written
        var jdaiConfig = Path.Combine(_tempDir, ".jdai", "mcp.json");
        Assert.False(File.Exists(jdaiConfig));
    }

    [Fact]
    public void Sync_Apply_WritesConfig()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                testsvr = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.Sync("claude", dryRun: false, homeDir: _tempDir);

        Assert.Contains("Applied", result);

        // Verify config was written
        var jdaiConfig = Path.Combine(_tempDir, ".jdai", "mcp.json");
        Assert.True(File.Exists(jdaiConfig));

        var json = File.ReadAllText(jdaiConfig);
        Assert.Contains("testsvr", json);
    }

    [Fact]
    public void Sync_AlreadySynced_ReportsNoChanges()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                existing = new { transport = "stdio", command = "echo" }
            }
        });

        WriteJdaiConfig(new
        {
            servers = new
            {
                existing = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.Sync("claude", dryRun: true, homeDir: _tempDir);

        Assert.Contains("No changes needed", result);
    }

    [Fact]
    public void Sync_TransportDifference_DetectsUpdate()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                remote = new { transport = "sse", url = "https://example.com" }
            }
        });

        WriteJdaiConfig(new
        {
            servers = new
            {
                remote = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.Sync("claude", dryRun: true, homeDir: _tempDir);

        Assert.Contains("Updated Servers", result);
    }

    [Fact]
    public void Sync_StdioWarning_ShowsSecurityNote()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                risky = new { transport = "stdio", command = "/usr/bin/something" }
            }
        });

        var result = McpEcosystemTools.Sync("claude", dryRun: true, homeDir: _tempDir);

        Assert.Contains("verify command is trusted", result);
    }

    // ── mcp_drift ────────────────────────────────────────────

    [Fact]
    public void Drift_NoConfigs_ReportsEmpty()
    {
        var result = McpEcosystemTools.DetectDrift(_tempDir);

        Assert.Contains("No MCP server definitions found", result);
    }

    [Fact]
    public void Drift_InSync_ReportsConsistent()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                github = new { transport = "stdio", command = "gh" }
            }
        });

        WriteJdaiConfig(new
        {
            servers = new
            {
                github = new { transport = "stdio", command = "gh" }
            }
        });

        var result = McpEcosystemTools.DetectDrift(_tempDir);

        Assert.Contains("## MCP Drift Report", result);
        Assert.Contains("In sync: **1**", result);
    }

    [Fact]
    public void Drift_TransportMismatch_DetectsDrift()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                api = new { transport = "sse", url = "https://example.com" }
            }
        });

        WriteJdaiConfig(new
        {
            servers = new
            {
                api = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpEcosystemTools.DetectDrift(_tempDir);

        Assert.Contains("Mismatches: **1**", result);
        Assert.Contains("transport-mismatch", result);
    }

    [Fact]
    public void Drift_SingleSource_DetectsMissing()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                onlyClaude = new { transport = "stdio", command = "gh" }
            }
        });

        var result = McpEcosystemTools.DetectDrift(_tempDir);

        Assert.Contains("Single source: **1**", result);
        Assert.Contains("`onlyClaude`", result);
    }

    // ── mcp_quarantine ───────────────────────────────────────

    [Fact]
    public void Quarantine_NoFile_ReportsEmpty()
    {
        var result = McpEcosystemTools.ListQuarantine(_tempDir);

        Assert.Contains("No servers in quarantine", result);
    }

    [Fact]
    public void Quarantine_WithEntries_ShowsTable()
    {
        var quarantineDir = Path.Combine(_tempDir, ".jdai");
        Directory.CreateDirectory(quarantineDir);
        File.WriteAllText(
            Path.Combine(quarantineDir, "mcp-quarantine.json"),
            """
            {
              "quarantined": [
                {
                  "name": "suspicious",
                  "reason": "Unknown executable",
                  "source": "Claude",
                  "importedAt": "2025-01-15T10:00:00Z"
                }
              ]
            }
            """);

        var result = McpEcosystemTools.ListQuarantine(_tempDir);

        Assert.Contains("## MCP Quarantine", result);
        Assert.Contains("`suspicious`", result);
        Assert.Contains("Unknown executable", result);
        Assert.Contains("**1** server(s)", result);
    }

    [Fact]
    public void Quarantine_EmptyArray_ReportsClean()
    {
        var quarantineDir = Path.Combine(_tempDir, ".jdai");
        Directory.CreateDirectory(quarantineDir);
        File.WriteAllText(
            Path.Combine(quarantineDir, "mcp-quarantine.json"),
            """{"quarantined": []}""");

        var result = McpEcosystemTools.ListQuarantine(_tempDir);

        Assert.Contains("No servers currently quarantined", result);
    }

    // ── mcp_ecosystem_export ─────────────────────────────────

    [Fact]
    public void ExportEcosystem_ReturnsValidJson()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                github = new { transport = "stdio", command = "gh" }
            }
        });

        var json = McpEcosystemTools.ExportEcosystem(_tempDir);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"servers\"", json);
        Assert.Contains("\"summary\"", json);
    }

    [Fact]
    public void ExportEcosystem_IncludesSummary()
    {
        WriteClaudeConfig(new
        {
            mcpServers = new
            {
                s1 = new { transport = "stdio", command = "echo" },
                s2 = new { transport = "sse", url = "https://example.com" }
            }
        });

        var json = McpEcosystemTools.ExportEcosystem(_tempDir);

        Assert.Contains("\"totalServers\": 2", json);
        Assert.Contains("\"Claude\"", json);
    }

    [Fact]
    public void ExportEcosystem_Empty_ReturnsEmptyArrays()
    {
        var json = McpEcosystemTools.ExportEcosystem(_tempDir);

        Assert.Contains("\"totalServers\": 0", json);
        Assert.Contains("\"servers\": []", json);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private void WriteClaudeConfig(object config)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(config, s_jsonOptions);
        File.WriteAllText(Path.Combine(_tempDir, ".claude.json"), json);
    }

    private void WriteJdaiConfig(object config)
    {
        var dir = Path.Combine(_tempDir, ".jdai");
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(config, s_jsonOptions);
        File.WriteAllText(Path.Combine(dir, "mcp.json"), json);
    }
}
