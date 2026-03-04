using JD.AI.Core.Mcp;
using JD.SemanticKernel.Extensions.Mcp;
using JD.SemanticKernel.Extensions.Mcp.Registry;

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
}
