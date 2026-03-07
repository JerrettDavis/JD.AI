using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public class McpTransportToolsTests : IDisposable
{
    private readonly string _tempDir;

    private static readonly string[] s_filesystemArgs = ["-y", "@mcp/fs"];

    public McpTransportToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    // ── mcp_list_servers ─────────────────────────────────────

    [Fact]
    public void ListServers_NoConfig_ReturnsSetupGuide()
    {
        var result = McpTransportTools.ListServers(_tempDir);

        Assert.Contains("No MCP servers configured", result);
        Assert.Contains("mcp.json", result);
    }

    [Fact]
    public void ListServers_WithConfig_ListsServers()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                filesystem = new { transport = "stdio", command = "npx", args = s_filesystemArgs },
                remote = new { transport = "sse", url = "https://mcp.example.com/sse" }
            }
        });

        var result = McpTransportTools.ListServers(_tempDir);

        Assert.Contains("## Configured MCP Servers", result);
        Assert.Contains("`filesystem`", result);
        Assert.Contains("`remote`", result);
        Assert.Contains("stdio", result);
        Assert.Contains("sse", result);
    }

    [Fact]
    public void ListServers_DotMcpJson_AlsoDiscovered()
    {
        var json = """{"mcpServers":{"test-server":{"transport":"stdio","command":"echo"}}}""";
        File.WriteAllText(Path.Combine(_tempDir, ".mcp.json"), json);

        var result = McpTransportTools.ListServers(_tempDir);

        Assert.Contains("`test-server`", result);
    }

    [Fact]
    public void ListServers_ShowsTotalCount()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                s1 = new { transport = "stdio", command = "echo" },
                s2 = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpTransportTools.ListServers(_tempDir);

        Assert.Contains("2 server(s)", result);
    }

    // ── mcp_transport_matrix ─────────────────────────────────

    [Fact]
    public void TransportMatrix_ShowsAllTransports()
    {
        var result = McpTransportTools.GetTransportMatrix();

        Assert.Contains("## MCP Transport Support Matrix", result);
        Assert.Contains("stdio", result);
        Assert.Contains("SSE", result);
        Assert.Contains("StreamableHTTP", result);
        Assert.Contains("WebSocket", result);
    }

    [Fact]
    public void TransportMatrix_IncludesExamples()
    {
        var result = McpTransportTools.GetTransportMatrix();

        Assert.Contains("#### stdio", result);
        Assert.Contains("#### SSE", result);
        Assert.Contains("```json", result);
    }

    // ── mcp_diagnose ─────────────────────────────────────────

    [Fact]
    public void Diagnose_UnknownServer_ReturnsNotFound()
    {
        var result = McpTransportTools.DiagnoseServer("nonexistent", _tempDir);

        Assert.Contains("not found", result);
        Assert.Contains("mcp_list_servers", result);
    }

    [Fact]
    public void Diagnose_StdioServer_ShowsDiagnostics()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                myserver = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpTransportTools.DiagnoseServer("myserver", _tempDir);

        Assert.Contains("## MCP Diagnostics: myserver", result);
        Assert.Contains("### Configuration", result);
        Assert.Contains("stdio", result);
        Assert.Contains("### Validation Checks", result);
        Assert.Contains("### Security Assessment", result);
    }

    [Fact]
    public void Diagnose_SseServerNoAuth_WarnsAboutAuth()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                remote = new { transport = "sse", url = "https://example.com/mcp" }
            }
        });

        var result = McpTransportTools.DiagnoseServer("remote", _tempDir);

        Assert.Contains("authentication configured", result);
    }

    [Fact]
    public void Diagnose_CaseInsensitive()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                MyServer = new { transport = "stdio", command = "echo" }
            }
        });

        var result = McpTransportTools.DiagnoseServer("MYSERVER", _tempDir);

        Assert.Contains("## MCP Diagnostics: MyServer", result);
    }

    // ── mcp_credential_status ────────────────────────────────

    [Fact]
    public void CredentialStatus_NoServers_ReportsEmpty()
    {
        var result = McpTransportTools.GetCredentialStatus(_tempDir);

        Assert.Contains("No MCP servers configured", result);
    }

    [Fact]
    public void CredentialStatus_WithServers_ShowsTable()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                local = new { transport = "stdio", command = "echo" },
                remote = new
                {
                    transport = "sse",
                    url = "https://example.com",
                    headers = new { Authorization = "Bearer token" }
                }
            }
        });

        var result = McpTransportTools.GetCredentialStatus(_tempDir);

        Assert.Contains("## MCP Credential Status", result);
        Assert.Contains("`local`", result);
        Assert.Contains("`remote`", result);
    }

    [Fact]
    public void CredentialStatus_IncludesGuidelines()
    {
        WriteMcpConfig(new
        {
            servers = new { s = new { transport = "stdio", command = "echo" } }
        });

        var result = McpTransportTools.GetCredentialStatus(_tempDir);

        Assert.Contains("### Credential Security Guidelines", result);
        Assert.Contains("environment variables", result);
    }

    // ── mcp_export_config ────────────────────────────────────

    [Fact]
    public void ExportConfig_ReturnsValidJson()
    {
        WriteMcpConfig(new
        {
            servers = new { test = new { transport = "stdio", command = "echo" } }
        });

        var json = McpTransportTools.ExportConfig(_tempDir);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"servers\"", json);
        Assert.Contains("\"summary\"", json);
    }

    [Fact]
    public void ExportConfig_IncludesSummary()
    {
        WriteMcpConfig(new
        {
            servers = new
            {
                s1 = new { transport = "stdio", command = "echo" },
                s2 = new { transport = "sse", url = "https://example.com" }
            }
        });

        var json = McpTransportTools.ExportConfig(_tempDir);

        Assert.Contains("\"total\": 2", json);
    }

    [Fact]
    public void ExportConfig_EmptyDir_ReturnsEmptyArray()
    {
        var json = McpTransportTools.ExportConfig(_tempDir);

        Assert.Contains("\"servers\": []", json);
        Assert.Contains("\"total\": 0", json);
    }

    // ── Helper ───────────────────────────────────────────────

    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private void WriteMcpConfig(object config)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(config, s_jsonOptions);
        File.WriteAllText(Path.Combine(_tempDir, "mcp.json"), json);
    }
}
