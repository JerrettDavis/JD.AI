namespace JD.AI.Gateway.Config;

public sealed class GatewayConfig
{
    public ServerConfig Server { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public List<ChannelConfig> Channels { get; set; } = [];
    public List<ProviderConfig> Providers { get; set; } = [];
}

public sealed class ServerConfig
{
    public int Port { get; set; } = 18789;
    public string Host { get; set; } = "localhost";
    public bool Verbose { get; set; }
}

public sealed class AuthConfig
{
    public bool Enabled { get; set; }
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
}

public sealed class ApiKeyEntry
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "User";
}

public sealed class RateLimitConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerMinute { get; set; } = 60;
}

public sealed class ChannelConfig
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, string> Settings { get; set; } = [];
}

public sealed class ProviderConfig
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = [];
}
