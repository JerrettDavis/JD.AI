using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Services;

var builder = Host.CreateApplicationBuilder(args);

// Platform-specific service hosting
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "JD.AI Gateway";
});
builder.Services.AddSystemd();

// Core services (same as Gateway)
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

var host = builder.Build();
host.Run();
