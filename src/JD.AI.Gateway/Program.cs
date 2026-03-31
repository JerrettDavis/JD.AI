using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Config;
using JD.AI.Core.Events;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Memory;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Security;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Commands;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Endpoints;
using JD.AI.Gateway.Hubs;
using JD.AI.Gateway.Middleware;
using JD.AI.Gateway.Services;
using JD.AI.Telemetry;
using JD.AI.Telemetry.Extensions;
using JD.AI.Workflows;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

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

// API key lifecycle management (create, revoke, rotate, usage tracking)
var apiKeyRotation = new ApiKeyRotation();
foreach (var entry in gatewayConfig.Auth.ApiKeys)
{
    if (Enum.TryParse<GatewayRole>(entry.Role, ignoreCase: true, out var role))
    {
        apiKeyRotation.GenerateKey(entry.Name, role);
    }
}
builder.Services.AddSingleton(apiKeyRotation);

if (string.Equals(gatewayConfig.RateLimit.Provider, "Redis", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(gatewayConfig.RateLimit.RedisConnectionString))
{
    builder.Services.AddSingleton<IRateLimiter>(sp =>
        new RedisRateLimiter(
            StackExchange.Redis.ConnectionMultiplexer.Connect(gatewayConfig.RateLimit.RedisConnectionString),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisRateLimiter>>(),
            gatewayConfig.RateLimit.MaxRequestsPerMinute));
}
else
{
    builder.Services.AddSingleton<IRateLimiter>(
        new SlidingWindowRateLimiter(gatewayConfig.RateLimit.MaxRequestsPerMinute));
}

// --- Core services ---
builder.Services.AddEventBus(new EventBusOptions
{
    Provider = gatewayConfig.EventBus.Provider,
    RedisConnectionString = gatewayConfig.EventBus.RedisConnectionString,
});
builder.Services.AddSingleton<IChannelRegistry, ChannelRegistry>();
builder.Services.AddSingleton<IProviderDetector, ClaudeCodeDetector>();
builder.Services.AddSingleton<IProviderDetector, CopilotDetector>();
builder.Services.AddSingleton<IProviderDetector, OpenAICodexDetector>();
builder.Services.AddSingleton<IProviderDetector, OllamaDetector>();
builder.Services.AddSingleton<IProviderDetector, FoundryLocalDetector>();
builder.Services.AddSingleton<IProviderDetector>(sp =>
    new LocalModelDetector(logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<LocalModelDetector>>()));
builder.Services.AddSingleton<IProviderRegistry>(sp =>
    new ProviderRegistry(sp.GetServices<IProviderDetector>()));
builder.Services.AddSingleton<SessionStore>(_ =>
    new SessionStore(DataDirectories.SessionsDb));
builder.Services.AddSingleton<AgentPoolService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentPoolService>());

// --- Plugin loader ---
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<IPluginRuntime>(sp => sp.GetRequiredService<PluginLoader>());
builder.Services.AddSingleton<PluginRegistryStore>();
builder.Services.AddSingleton<IPluginInstaller>(sp =>
    new PluginInstaller(
        new HttpClient(),
        sp.GetRequiredService<ILogger<PluginInstaller>>()));
builder.Services.AddSingleton<IPluginContextFactory, ServiceProviderPluginContextFactory>();
builder.Services.AddSingleton<IPluginLifecycleManager, PluginLifecycleManager>();
builder.Services.AddHostedService<PluginLifecycleHostedService>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddSingleton<AgentRouter>();

// --- Command system ---
builder.Services.AddSingleton<ICommandRegistry>(sp =>
{
    var registry = new CommandRegistry();
    registry.Register(new HelpCommand(registry));
    registry.Register(new UsageCommand(sp.GetRequiredService<AgentPoolService>()));
    registry.Register(new StatusCommand(
        sp.GetRequiredService<AgentPoolService>(),
        sp.GetRequiredService<IChannelRegistry>()));
    registry.Register(new ModelsCommand(
        sp.GetRequiredService<AgentPoolService>(),
        sp.GetRequiredService<GatewayConfig>()));
    registry.Register(new SwitchCommand(sp.GetRequiredService<AgentPoolService>()));
    registry.Register(new ClearCommand(sp.GetRequiredService<AgentPoolService>()));
    registry.Register(new AgentsCommand(
        sp.GetRequiredService<AgentPoolService>(),
        sp.GetRequiredService<AgentRouter>()));
    registry.Register(new RouteCommand(
        sp.GetRequiredService<AgentRouter>(),
        sp.GetRequiredService<AgentPoolService>()));
    registry.Register(new RoutesCommand(
        sp.GetRequiredService<AgentRouter>(),
        sp.GetRequiredService<AgentPoolService>()));
    registry.Register(new ProvidersCommand(
        sp.GetRequiredService<IProviderRegistry>()));
    registry.Register(new ProviderCommand(
        sp.GetRequiredService<AgentRouter>(),
        sp.GetRequiredService<AgentPoolService>(),
        sp.GetRequiredService<IProviderRegistry>()));
    registry.Register(new ConfigCommand(
        sp.GetRequiredService<AgentRouter>(),
        sp.GetRequiredService<AgentPoolService>(),
        sp.GetRequiredService<IChannelRegistry>(),
        sp.GetRequiredService<IProviderRegistry>()));
    registry.Register(new DoctorCommand(
        sp.GetRequiredService<HealthCheckService>()));
    registry.Register(new DocsCommand());
    registry.Register(new AuditCommand(
        sp.GetRequiredService<IQueryableAuditSink>()));
    return registry;
});
// --- Audit sinks (in-memory queryable + durable file) ---
var auditSink = new InMemoryAuditSink();
builder.Services.AddSingleton(auditSink);
builder.Services.AddSingleton<IQueryableAuditSink>(auditSink);
builder.Services.AddSingleton<IAuditSink>(auditSink);
var fileAuditSink = new FileAuditSink(Path.Combine(DataDirectories.Root, "audit"));
builder.Services.AddSingleton<IAuditSink>(fileAuditSink);
builder.Services.AddSingleton(sp => new AuditService(sp.GetServices<IAuditSink>(),
    sp.GetService<Microsoft.Extensions.Logging.ILogger<AuditService>>()));

builder.Services.AddSingleton<IVectorStore>(_ =>
    new SqliteVectorStore(DataDirectories.VectorsDb));

// --- Workflow services ---
builder.Services.AddSingleton<IWorkflowCatalog>(_ =>
{
    try
    {
        return new FileWorkflowCatalog(Path.Combine(DataDirectories.Root, "workflows"));
    }
    catch
    {
        return new InMemoryWorkflowCatalog();
    }
});
builder.Services.AddSingleton<IWorkflowBridge, WorkflowBridge>();

// --- Intent classifier: ML.NET with hot-swap (file-watcher reload) ---
// Model path: <DataRoot>/models/intent_classifier.zip
// To upgrade the model in production: replace the .zip file; Gateway reloads automatically.
var modelPath = Path.Combine(DataDirectories.Root, "models", "intent_classifier.zip");
IPromptIntentClassifier initialClassifier = File.Exists(modelPath)
    ? new MlNetIntentClassifier(modelPath)
    : new TfIdfIntentClassifier();

var classifierManager = new IntentClassifierManager(initialClassifier);
builder.Services.AddSingleton<IIntentClassifierManager>(classifierManager);
builder.Services.AddSingleton<IPromptIntentClassifier>(
    new HotSwappingIntentClassifier(classifierManager));

if (initialClassifier is MlNetIntentClassifier)
{
    builder.Services.AddHostedService(sp =>
        new IntentClassifierFileWatcher(
            classifierManager,
            modelPath,
            sp.GetService<ILogger<IntentClassifierFileWatcher>>()));
}

builder.Services.AddSingleton<IWorkflowMatcher, WorkflowMatcher>();
builder.Services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();

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

    // Register the real message processor that routes based on OpenClaw session key
    builder.Services.AddSingleton<Func<string, string, Task<string?>>>(sp =>
    {
        var pool = sp.GetRequiredService<AgentPoolService>();
        var gwConfig = sp.GetRequiredService<GatewayConfig>();

        // Build a map: OpenClaw agent ID → JD.AI gateway pool agent ID
        // e.g., "jdai-default" → "default" (config ID)
        var agentMapping = gwConfig.OpenClaw.RegisterAgents
            .Where(r => !string.IsNullOrEmpty(r.GatewayAgentId))
            .ToDictionary(r => r.Id, r => r.GatewayAgentId!, StringComparer.OrdinalIgnoreCase);

        return async (sessionKey, content) =>
        {
            // OpenClaw session keys are "agent:{agentId}:{sessionSuffix}"
            var ocAgentId = ExtractAgentIdFromSessionKey(sessionKey);
            string? poolAgentId = null;

            // Try to resolve via config mapping: OpenClaw agent ID → gateway agent config ID → pool ID
            if (ocAgentId is not null && agentMapping.TryGetValue(ocAgentId, out var gatewayAgentConfigId))
            {
                // Look up the pool agent spawned from this config ID
                var agents = pool.ListAgents();
                poolAgentId = agents.Count > 0 ? agents[0].Id : null; // Spawned agents currently don't track config ID
            }

            // Fall back to first available agent
            var allAgents = pool.ListAgents();
            poolAgentId ??= allAgents.Count > 0 ? allAgents[0].Id : null;

            if (poolAgentId is null) return null;
            return await pool.SendMessageAsync(poolAgentId, content, CancellationToken.None);
        };

        static string? ExtractAgentIdFromSessionKey(string sessionKey)
        {
            // Session key format: "agent:{agentId}:{suffix}"
            if (!sessionKey.StartsWith("agent:", StringComparison.Ordinal))
                return null;
            var parts = sessionKey.Split(':', 3);
            return parts.Length >= 2 ? parts[1] : null;
        }
    });

    // Register JD.AI agents with OpenClaw so they appear in the dashboard
    builder.Services.AddSingleton<OpenClawAgentRegistrar>();
}

// --- SignalR ---
builder.Services.AddSignalR();

// --- OpenAPI ---
builder.Services.AddOpenApi();

// --- OpenTelemetry ---
var telemetryConfig = new TelemetryConfig
{
    Enabled = gatewayConfig.Telemetry.Enabled,
    ServiceName = gatewayConfig.Telemetry.ServiceName,
    Exporter = gatewayConfig.Telemetry.Exporter,
    Endpoint = gatewayConfig.Telemetry.Endpoint,
};
builder.Services.AddSingleton(telemetryConfig);
builder.Services.AddJdAiTelemetry(telemetryConfig);

// --- Health checks ---
builder.Services.AddJdAiHealthChecks()
    .AddCheck<GatewayHealthCheck>("gateway");

// --- CORS (allow TUI, web clients, and SignalR WebSockets) ---
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();

// --- Initialize stores ---
await app.Services.GetRequiredService<SessionStore>().InitializeAsync();

// --- Middleware pipeline ---
// API version rewrite must precede routing so /api/* → /api/v1/* happens first
app.UseMiddleware<ApiVersionRewriteMiddleware>();
app.UseRouting();
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

// --- OpenAPI (available in all environments, protected by auth middleware) ---
app.MapOpenApi();

// --- Health ---
app.MapHealthChecks(GatewayRuntimeDefaults.HealthPath);
app.MapHealthChecks(GatewayRuntimeDefaults.HealthReadyPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});
app.MapGet(GatewayRuntimeDefaults.HealthLivePath, () => Results.Ok(new { Status = "Live" }));
app.MapGet(GatewayRuntimeDefaults.HealthStartupPath, () => Results.Ok(new { Status = "Started" }));
app.MapGet(GatewayRuntimeDefaults.ReadyPath, () => Results.Ok(new { Status = "Ready" }));

// --- REST API endpoints ---
app.MapSessionEndpoints();
app.MapAgentEndpoints();
app.MapProviderEndpoints();
app.MapChannelEndpoints();
app.MapPluginEndpoints();
app.MapMemoryEndpoints();
app.MapRoutingEndpoints();
app.MapGatewayConfigEndpoints();
// app.MapApiKeyEndpoints(); // Enabled by PR #426
app.MapAuditEndpoints();
app.MapWorkflowEndpoints();

// --- SignalR hubs ---
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<EventHub>("/hubs/events");

app.Run();

/// <summary>Marker class for WebApplicationFactory test host.</summary>
public partial class Program { }
