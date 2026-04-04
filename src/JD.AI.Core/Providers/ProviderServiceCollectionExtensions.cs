using JD.AI.Core.LocalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Providers;

/// <summary>
/// Service registration helpers for the default gateway/daemon provider detector set.
/// </summary>
public static class ProviderServiceCollectionExtensions
{
    public static IServiceCollection AddDefaultProviderRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProviderDetector, ClaudeCodeDetector>();
        services.AddSingleton<IProviderDetector, CopilotDetector>();
        services.AddSingleton<IProviderDetector, OpenAICodexDetector>();
        services.AddSingleton<IProviderDetector, OllamaDetector>();
        services.AddSingleton<IProviderDetector, FoundryLocalDetector>();
        services.AddSingleton<LocalModelDetector>();
        services.AddSingleton<IProviderDetector>(sp =>
            sp.GetRequiredService<LocalModelDetector>());
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

        return services;
    }
}
