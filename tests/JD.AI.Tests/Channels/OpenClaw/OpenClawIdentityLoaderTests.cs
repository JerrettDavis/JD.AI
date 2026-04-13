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

    [Fact]
    public void LoadDeviceIdentity_WhenDeviceJsonMissing_DoesNotPopulateDeviceId()
    {
        var stateDir = CreateTempDir();
        try
        {
            var identityDir = Path.Combine(stateDir, "identity");
            Directory.CreateDirectory(identityDir);

            // Create auth but not device.json
            File.WriteAllText(Path.Combine(identityDir, "device-auth.json"), JsonSerializer.Serialize(new
            {
                tokens = new { @operator = new { token = "operator-token" } }
            }));

            var config = new OpenClawConfig();
            OpenClawIdentityLoader.LoadDeviceIdentity(config, stateDir);

            // Neither file exists, so nothing should be loaded
            Assert.Empty(config.DeviceId ?? "");
            Assert.Empty(config.PublicKeyPem ?? "");
            Assert.Empty(config.PrivateKeyPem ?? "");
        }
        finally
        {
            TryDeleteDir(stateDir);
        }
    }

    [Fact]
    public void LoadDeviceIdentity_WhenAuthJsonMissing_DoesNotPopulateDeviceToken()
    {
        var stateDir = CreateTempDir();
        try
        {
            var identityDir = Path.Combine(stateDir, "identity");
            Directory.CreateDirectory(identityDir);

            // Create device but not auth
            File.WriteAllText(Path.Combine(identityDir, "device.json"), JsonSerializer.Serialize(new
            {
                deviceId = "device-1",
                publicKeyPem = "pub",
                privateKeyPem = "priv"
            }));

            var config = new OpenClawConfig();
            OpenClawIdentityLoader.LoadDeviceIdentity(config, stateDir);

            // Only device.json exists, auth was missing
            Assert.Empty(config.DeviceToken ?? "");
        }
        finally
        {
            TryDeleteDir(stateDir);
        }
    }

    [Fact]
    public void LoadDeviceIdentity_WhenGatewayTokenAlreadySet_DoesNotOverwrite()
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
                tokens = new { @operator = new { token = "operator-token" } }
            }));

            File.WriteAllText(Path.Combine(stateDir, "openclaw.json"), JsonSerializer.Serialize(new
            {
                gateway = new { auth = new { token = "new-gateway-token" } }
            }));

            var config = new OpenClawConfig { GatewayToken = "existing-gateway-token" };
            OpenClawIdentityLoader.LoadDeviceIdentity(config, stateDir);

            // Should not overwrite existing GatewayToken
            Assert.Equal("existing-gateway-token", config.GatewayToken);
        }
        finally
        {
            TryDeleteDir(stateDir);
        }
    }

    [Fact]
    public void HasRequiredIdentity_WithDeviceTokenOnly_ReturnsTrue()
    {
        var config = new OpenClawConfig
        {
            DeviceId = "device-1",
            PublicKeyPem = "pub",
            PrivateKeyPem = "priv",
            DeviceToken = "operator-token"
            // GatewayToken is null or empty
        };

        Assert.True(OpenClawIdentityLoader.HasRequiredIdentity(config));
    }

    [Fact]
    public void HasRequiredIdentity_DeviceIdMissing_ReturnsFalse()
    {
        var config = new OpenClawConfig
        {
            PublicKeyPem = "pub",
            PrivateKeyPem = "priv",
            GatewayToken = "gateway-token"
            // DeviceId is missing
        };

        Assert.False(OpenClawIdentityLoader.HasRequiredIdentity(config));
    }

    [Fact]
    public void HasRequiredIdentity_WithNeitherToken_ReturnsFalse()
    {
        var config = new OpenClawConfig
        {
            DeviceId = "device-1",
            PublicKeyPem = "pub",
            PrivateKeyPem = "priv"
            // Neither DeviceToken nor GatewayToken
        };

        Assert.False(OpenClawIdentityLoader.HasRequiredIdentity(config));
    }

    [Fact]
    public void ResolveStateDir_WhenNoPathExists_ReturnsFallback()
    {
        // Clear environment variables so we test fallback
        Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", null);
        Environment.SetEnvironmentVariable("JD_AI_OPENCLAW_STATE_DIR", null);

        // ResolveStateDir should return a fallback path (in user's home directory)
        var resolved = OpenClawIdentityLoader.ResolveStateDir();

        Assert.NotNull(resolved);
        Assert.NotEmpty(resolved);
        // Should end with .openclaw as the fallback
        Assert.True(resolved.EndsWith(".openclaw", StringComparison.OrdinalIgnoreCase)
                    || resolved.Contains(".openclaw"), "Fallback should include .openclaw directory");
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

