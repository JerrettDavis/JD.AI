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

// --- SignalR hubs ---
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<EventHub>("/hubs/events");

app.Run();

/// <summary>Marker class for WebApplicationFactory test host.</summary>
public partial class Program { }
