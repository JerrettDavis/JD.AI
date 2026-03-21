using System.Text.Json.Nodes;

namespace JD.AI.Daemon.Tests.Services;

public sealed class OpenClawBridgeConfigEditorTests
{
    [Fact]
    public async Task ReadState_WhenBridgeSectionMissing_UsesDefaults()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{}");

            var state = OpenClawBridgeConfigEditor.ReadState(path);

            Assert.False(state.Enabled);
            Assert.True(state.AutoConnect);
            Assert.Equal("Passthrough", state.DefaultMode);
            Assert.False(state.OverrideActive);
            Assert.Empty(state.OverrideChannels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadState_WhenDisabled_IgnoresOverrides()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """
                                    {
                                      "Gateway": {
                                        "OpenClaw": {
                                          "Enabled": false,
                                          "AutoConnect": false,
                                          "DefaultMode": "Intercept",
                                          "Channels": {
                                            "discord": { "Mode": "Sidecar" }
                                          }
                                        }
                                      }
                                    }
                                    """);

            var state = OpenClawBridgeConfigEditor.ReadState(path);

            Assert.False(state.Enabled);
            Assert.False(state.AutoConnect);
            Assert.Equal("Intercept", state.DefaultMode);
            Assert.False(state.OverrideActive);
            Assert.Empty(state.OverrideChannels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetPassthrough_UpdatesEveryConfiguredChannel()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """
                                    {
                                      "Gateway": {
                                        "OpenClaw": {
                                          "Enabled": false,
                                          "AutoConnect": false,
                                          "DefaultMode": "Intercept",
                                          "Channels": {
                                            "discord": { "Mode": "Sidecar" },
                                            "signal": { "Mode": "Proxy" }
                                          }
                                        }
                                      }
                                    }
                                    """);

            var updated = OpenClawBridgeConfigEditor.SetPassthrough(path);
            var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            var channels = json["Gateway"]!["OpenClaw"]!["Channels"]!.AsObject();

            Assert.True(updated.Enabled);
            Assert.True(updated.AutoConnect);
            Assert.Equal("Passthrough", updated.DefaultMode);
            Assert.False(updated.OverrideActive);
            Assert.Empty(updated.OverrideChannels);
            Assert.Equal("Passthrough", channels["discord"]!["Mode"]!.GetValue<string>());
            Assert.Equal("Passthrough", channels["signal"]!["Mode"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
