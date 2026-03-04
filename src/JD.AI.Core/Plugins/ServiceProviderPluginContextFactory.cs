using JD.AI.Plugins.SDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Plugins;

/// <summary>
/// DI-backed plugin context factory for hosted environments (gateway/daemon).
/// </summary>
public sealed class ServiceProviderPluginContextFactory : IPluginContextFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderPluginContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPluginContext CreateContext() => new ServiceProviderPluginContext(_serviceProvider);

    private sealed class ServiceProviderPluginContext : IPluginContext
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderPluginContext(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Kernel Kernel =>
            _serviceProvider.GetService<Kernel>() ?? new Kernel();

        public IReadOnlyDictionary<string, string> Configuration { get; } =
            new Dictionary<string, string>();

        public void OnEvent(string eventType, Func<object?, Task> handler) { }

        public T? GetService<T>() where T : class => _serviceProvider.GetService<T>();

        public void Log(PluginLogLevel level, string message) =>
            _serviceProvider
                .GetService<ILogger<ServiceProviderPluginContext>>()
                ?.LogInformation("[Plugin:{Level}] {Message}", level, message);
    }
}
