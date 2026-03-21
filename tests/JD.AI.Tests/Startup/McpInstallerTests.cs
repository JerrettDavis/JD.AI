using JD.AI.Core.Mcp;
using JD.AI.Startup;
using JD.SemanticKernel.Extensions.Mcp.Registry;

namespace JD.AI.Tests.Startup;

public sealed class McpInstallerTests
{
    [Fact]
    public async Task InstallAsync_InstallsStdioAndHttpEntries()
    {
        var path = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable("JDAI_TEST_MCP_TOKEN", "token-from-env");

            var provider = new JdAiMcpDiscoveryProvider(path);
            var manager = new McpManager(new McpRegistry([provider]), provider);
            var selected = new[]
            {
                new CuratedMcpEntry(
                    Id: "stdio-server",
                    DisplayName: "Stdio Server",
                    Category: "Test",
                    Description: "stdio test",
                    Transport: CuratedMcpTransport.Stdio,
                    Command: "npx",
                    DefaultArgs: ["-y", "@example/stdio"],
                    RequiredEnvVars: [new CuratedMcpEnvVar("JDAI_TEST_MCP_TOKEN", "Token")]),
                new CuratedMcpEntry(
                    Id: "http-server",
                    DisplayName: "Http Server",
                    Category: "Test",
                    Description: "http test",
                    Transport: CuratedMcpTransport.Http,
                    Url: "https://localhost:7777/mcp"),
            };

            var installed = await McpInstaller.InstallAsync(selected, manager);

            Assert.Equal(2, installed);

            var servers = await manager.GetAllServersAsync();
            Assert.Contains(servers, s =>
                string.Equals(s.Name, "stdio-server", StringComparison.Ordinal)
                && string.Equals(s.Command, "npx", StringComparison.Ordinal));
            Assert.Contains(servers, s =>
                string.Equals(s.Name, "http-server", StringComparison.Ordinal)
                && s.Url is not null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_TEST_MCP_TOKEN", null);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAsync_InvalidHttpUrl_IsSkipped()
    {
        var path = Path.GetTempFileName();
        try
        {
            var provider = new JdAiMcpDiscoveryProvider(path);
            var manager = new McpManager(new McpRegistry([provider]), provider);
            var selected = new[]
            {
                new CuratedMcpEntry(
                    Id: "bad-http",
                    DisplayName: "Bad Http",
                    Category: "Test",
                    Description: "invalid url",
                    Transport: CuratedMcpTransport.Http,
                    Url: "not-a-uri"),
            };

            var installed = await McpInstaller.InstallAsync(selected, manager);

            Assert.Equal(0, installed);
            var servers = await manager.GetAllServersAsync();
            Assert.DoesNotContain(servers, s =>
                string.Equals(s.Name, "bad-http", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAsync_WhenManagerCannotWrite_ReturnsZeroWithoutThrowing()
    {
        var manager = new McpManager(new McpRegistry([]), jdAiProvider: null);
        var selected = new[]
        {
            new CuratedMcpEntry(
                Id: "stdio-server",
                DisplayName: "Stdio Server",
                Category: "Test",
                Description: "stdio test",
                Transport: CuratedMcpTransport.Stdio,
                Command: "npx",
                DefaultArgs: ["-y", "@example/stdio"]),
        };

        var installed = await McpInstaller.InstallAsync(selected, manager);

        Assert.Equal(0, installed);
    }
}
