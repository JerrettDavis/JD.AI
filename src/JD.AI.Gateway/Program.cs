using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Endpoints;
using JD.AI.Gateway.Hubs;
using JD.AI.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

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

// --- SignalR hubs ---
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<EventHub>("/hubs/events");

app.Run();

/// <summary>Marker class for WebApplicationFactory test host.</summary>
public partial class Program { }
