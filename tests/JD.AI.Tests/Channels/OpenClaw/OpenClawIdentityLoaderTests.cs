using System.Text.Json;
using JD.AI.Channels.OpenClaw;

namespace JD.AI.Tests.Channels.OpenClaw;

public sealed class OpenClawIdentityLoaderTests : IDisposable
{
    private readonly string? _prevOpenClawStateDir;
    private readonly string? _prevJdAiOpenClawStateDir;

    public OpenClawIdentityLoaderTests()
    {
        _prevOpenClawStateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
        _prevJdAiOpenClawStateDir = Environment.GetEnvironmentVariable("JD_AI_OPENCLAW_STATE_DIR");
    }

    [Fact]
    public void ResolveStateDir_PrefersConfiguredPath()
    {
        var configured = CreateTempDir();
        var envDir = CreateTempDir();
        try
        {
            Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", envDir);

            var resolved = OpenClawIdentityLoader.ResolveStateDir(configured);

            Assert.Equal(configured, resolved);
        }
        finally
        {
            TryDeleteDir(configured);
            TryDeleteDir(envDir);
        }
    }

    [Fact]
    public void LoadDeviceIdentity_LoadsDeviceAndGatewayToken()
    {
        var stateDir = CreateTempDir();
        try
        {
            var identityDir = Path.Combine(stateDir, "identity");
            Directory.CreateDirectory(identityDir);

            File.WriteAllText(Path.Combine(identityDir, "device.json"), JsonSerializer.Serialize(new
            {
                deviceId = "device-1",
                publicKeyPem = "pub",
                privateKeyPem = "priv"
            }));

            File.WriteAllText(Path.Combine(identityDir, "device-auth.json"), JsonSerializer.Serialize(new
            {
                tokens = new
                {
                    @operator = new
                    {
                        token = "operator-token"
                    }
                }
            }));

            File.WriteAllText(Path.Combine(stateDir, "openclaw.json"), JsonSerializer.Serialize(new
            {
                gateway = new
                {
                    auth = new
                    {
                        token = "gateway-token"
                    }
                }
            }));

            var config = new OpenClawConfig();
            OpenClawIdentityLoader.LoadDeviceIdentity(config, stateDir);

            Assert.Equal("device-1", config.DeviceId);
            Assert.Equal("pub", config.PublicKeyPem);
            Assert.Equal("priv", config.PrivateKeyPem);
            Assert.Equal("operator-token", config.DeviceToken);
            Assert.Equal("gateway-token", config.GatewayToken);
            Assert.Equal(stateDir, config.OpenClawStateDir);
        }
        finally
        {
            TryDeleteDir(stateDir);
        }
    }

    [Fact]
    public void HasRequiredIdentity_True_WhenGatewayTokenPresent()
    {
        var config = new OpenClawConfig
        {
            DeviceId = "device-1",
            PublicKeyPem = "pub",
            PrivateKeyPem = "priv",
            GatewayToken = "gateway-token"
        };

        Assert.True(OpenClawIdentityLoader.HasRequiredIdentity(config));
    }

    [Fact]
    public void HasRequiredIdentity_False_WhenCoreFieldsMissing()
    {
        var config = new OpenClawConfig
        {
            DeviceId = "device-1",
            PublicKeyPem = "pub",
            PrivateKeyPem = "",
            DeviceToken = "operator-token"
        };

        Assert.False(OpenClawIdentityLoader.HasRequiredIdentity(config));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", _prevOpenClawStateDir);
        Environment.SetEnvironmentVariable("JD_AI_OPENCLAW_STATE_DIR", _prevJdAiOpenClawStateDir);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jdai-openclaw-id-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for tests.
        }
    }
}

