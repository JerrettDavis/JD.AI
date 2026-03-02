namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Configuration for connecting to an OpenClaw gateway via WebSocket JSON-RPC.
/// Device identity fields are required for Ed25519 challenge-response authentication.
/// </summary>
public sealed class OpenClawConfig
{
    /// <summary>WebSocket URL of the OpenClaw gateway (e.g., "ws://localhost:18789").</summary>
    public string WebSocketUrl { get; set; } = "ws://localhost:18789";

    /// <summary>Friendly name for this OpenClaw instance.</summary>
    public string InstanceName { get; set; } = "local";

    /// <summary>Default session key to send messages to (e.g., "agent:main:main").</summary>
    public string SessionKey { get; set; } = "agent:main:main";

    // Device identity (loaded from ~/.openclaw/identity/)

    /// <summary>Hex-encoded device ID (SHA-256 of the raw Ed25519 public key).</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>PEM-encoded Ed25519 public key.</summary>
    public string PublicKeyPem { get; set; } = "";

    /// <summary>PEM-encoded Ed25519 private key (PKCS#8).</summary>
    public string PrivateKeyPem { get; set; } = "";

    /// <summary>Device auth token issued by OpenClaw after pairing.</summary>
    public string DeviceToken { get; set; } = "";

    /// <summary>Gateway shared authentication token (from openclaw.json → gateway.auth.token).</summary>
    public string GatewayToken { get; set; } = "";

    /// <summary>
    /// Path to the OpenClaw state directory containing identity files.
    /// When set, <see cref="DeviceId"/>, <see cref="PublicKeyPem"/>,
    /// <see cref="PrivateKeyPem"/>, and <see cref="DeviceToken"/> are
    /// loaded automatically from device.json and device-auth.json.
    /// Defaults to ~/.openclaw.
    /// </summary>
    public string? OpenClawStateDir { get; set; }
}
