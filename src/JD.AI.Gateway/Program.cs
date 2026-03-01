using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Plugins;
using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Memory;
using JD.AI.Core.Providers;
using JD.AI.Core.Security;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Endpoints;
using JD.AI.Gateway.Hubs;
using JD.AI.Gateway.Middleware;
using JD.AI.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Gateway configuration ---
var gatewayConfig = builder.Configuration.GetSection("Gateway").Get<GatewayConfig>() ?? new GatewayConfig();
builder.Services.AddSingleton(gatewayConfig);

// --- Security services ---
var authProvider = new ApiKeyAuthProvider();
foreach (var entry in gatewayConfig.Auth.ApiKeys)
{
    if (Enum.TryParse<GatewayRole>(entry.Role, ignoreCase: true, out var role))
        authProvider.RegisterKey(entry.Key, entry.Name, role);
}

builder.Services.AddSingleton<IAuthProvider>(authProvider);
builder.Services.AddSingleton<IRateLimiter>(
    new SlidingWindowRateLimiter(gatewayConfig.RateLimit.MaxRequestsPerMinute));

// --- Core services ---
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddSingleton<IChannelRegistry, ChannelRegistry>();
builder.Services.AddSingleton<IProviderDetector, ClaudeCodeDetector>();
builder.Services.AddSingleton<IProviderDetector, CopilotDetector>();
builder.Services.AddSingleton<IProviderDetector, OllamaDetector>();
builder.Services.AddSingleton<IProviderRegistry>(sp =>
    new ProviderRegistry(sp.GetServices<IProviderDetector>()));
builder.Services.AddSingleton<SessionStore>(_ =>
    new SessionStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".jdai", "sessions.db")));
builder.Services.AddSingleton<AgentPoolService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentPoolService>());

// --- Plugin loader ---
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<AgentRouter>();
builder.Services.AddSingleton<IVectorStore>(_ =>
    new SqliteVectorStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".jdai", "vectors.db")));

// --- Channel factory & orchestrator ---
builder.Services.AddSingleton<ChannelFactory>();
builder.Services.AddHostedService<GatewayOrchestrator>();

// --- OpenClaw bridge (if enabled) ---
if (gatewayConfig.OpenClaw.Enabled)
{
    builder.Services.AddOpenClawBridge(config =>
    {
        config.WebSocketUrl = gatewayConfig.OpenClaw.WebSocketUrl;
    });

    builder.Services.AddOpenClawRouting(
        routing =>
        {
            if (Enum.TryParse<OpenClawRoutingMode>(gatewayConfig.OpenClaw.DefaultMode, true, out var defaultMode))
                routing.DefaultMode = defaultMode;

            foreach (var (channelName, channelConfig) in gatewayConfig.OpenClaw.Channels)
            {
                var route = new OpenClawChannelRouteConfig();

                if (Enum.TryParse<OpenClawRoutingMode>(channelConfig.Mode, true, out var mode))
                    route.Mode = mode;

                route.CommandPrefix = channelConfig.CommandPrefix;
                route.TriggerPattern = channelConfig.TriggerPattern;

                if (!string.IsNullOrEmpty(channelConfig.SystemPrompt))
                    route.SystemPrompt = channelConfig.SystemPrompt;

                if (!string.IsNullOrEmpty(channelConfig.AgentId))
                    route.AgentProfile = channelConfig.AgentId;

                routing.Channels[channelName] = route;
            }
        },
        messageProcessor: null); // Wired below after build

    // Register the real message processor after app is built
    builder.Services.AddSingleton<Func<string, string, Task<string?>>>(sp =>
    {
        var pool = sp.GetRequiredService<AgentPoolService>();
        return async (sessionKey, content) =>
        {
            var agents = pool.ListAgents();
            var agentId = agents.FirstOrDefault()?.Id;
            if (agentId is null) return null;
            return await pool.SendMessageAsync(agentId, content, CancellationToken.None);
        };
    });

    // Register JD.AI agents with OpenClaw so they appear in the dashboard
    builder.Services.AddSingleton<OpenClawAgentRegistrar>();
}

// --- SignalR ---
builder.Services.AddSignalR();

// --- OpenAPI ---
builder.Services.AddOpenApi();

// --- Health checks ---
builder.Services.AddHealthChecks()
    .AddCheck<GatewayHealthCheck>("gateway");

// --- CORS (allow TUI and web clients) ---
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// --- Middleware pipeline ---
app.UseCors();

// --- Security middleware ---
if (gatewayConfig.Auth.Enabled)
{
    app.UseMiddleware<ApiKeyAuthMiddleware>();
}

if (gatewayConfig.RateLimit.Enabled)
{
    app.UseMiddleware<RateLimitMiddleware>();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- Health ---
app.MapHealthChecks("/health");
app.MapGet("/ready", () => Results.Ok(new { Status = "Ready" }));

// --- REST API endpoints ---
app.MapSessionEndpoints();
app.MapAgentEndpoints();
app.MapProviderEndpoints();
app.MapChannelEndpoints();
app.MapPluginEndpoints();
app.MapMemoryEndpoints();
app.MapRoutingEndpoints();
app.MapGatewayConfigEndpoints();

// --- SignalR hubs ---
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<EventHub>("/hubs/events");

app.Run();

/// <summary>Marker class for WebApplicationFactory test host.</summary>
public partial class Program { }
