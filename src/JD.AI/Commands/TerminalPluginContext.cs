using JD.AI.Plugins.SDK;
using Microsoft.SemanticKernel;

namespace JD.AI.Commands;

/// <summary>
/// Minimal plugin context used by the interactive terminal host.
/// </summary>
internal sealed class TerminalPluginContext : IPluginContext
{
    public TerminalPluginContext(Kernel kernel)
    {
        Kernel = kernel;
    }

    public Kernel Kernel { get; }

    public IReadOnlyDictionary<string, string> Configuration { get; } =
        new Dictionary<string, string>();

    public void OnEvent(string eventType, Func<object?, Task> handler)
    {
    }

    public T? GetService<T>() where T : class => null;

    public void Log(PluginLogLevel level, string message)
    {
    }
}
