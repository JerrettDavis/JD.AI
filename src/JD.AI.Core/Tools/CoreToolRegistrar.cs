using JD.AI.Core.Infrastructure;
using JD.AI.Core.Providers;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Holds references to the core tool instances registered by <see cref="CoreToolRegistrar.Register"/>.
/// Used by both CLI and Daemon/Gateway paths.
/// </summary>
public sealed record CoreToolRegistration(
    TaskTools TaskTools,
    WebSearchTools WebSearchTools,
    ProcessSessionManager ProcessSessionManager,
    SystemInfoTools SystemInfoTools);

/// <summary>
/// Registers essential kernel tool plugins that work without session infrastructure.
/// This is the shared registration path used by both the CLI and the Daemon/Gateway.
/// </summary>
public static class CoreToolRegistrar
{
    /// <summary>
    /// Registers core tools on a Semantic Kernel instance.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel to register tools on.</param>
    /// <param name="modelInfo">
    /// Optional model metadata. When provided, the agent can answer
    /// "what model am I?" via the <c>system.get_identity</c> tool.
    /// </param>
    public static CoreToolRegistration Register(Kernel kernel, ProviderModelInfo? modelInfo = null)
    {
        // Stateless tools — auto-discovered via [ToolPlugin(RequiresInjection = false)]
        ToolAssemblyScanner.RegisterStaticPlugins(kernel, typeof(FileTools).Assembly);

        // Memory
        kernel.Plugins.AddFromObject(new MemoryTools(), "memory");

        // Tasks
        var taskTools = new TaskTools();
        kernel.Plugins.AddFromObject(taskTools, "tasks");

        // Capabilities (introspection)
        var capabilityTools = new CapabilityTools(kernel);
        kernel.Plugins.AddFromObject(capabilityTools, "capabilities");

        // Process execution (shell commands)
        var processSessionManager = new ProcessSessionManager();
        kernel.Plugins.AddFromObject(new ExecProcessTools(processSessionManager), "runtime");

        // Web search
        var webSearchTools = new WebSearchTools();
        kernel.ImportPluginFromObject(webSearchTools, "WebSearchTools");

        // Scheduler
        kernel.Plugins.AddFromObject(new SchedulerTools(), "scheduler");

        // Gateway ops
        kernel.Plugins.AddFromObject(
            new GatewayOpsTools(Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL")), "gateway");

        // System info / self-identity
        var systemInfoTools = new SystemInfoTools();
        if (modelInfo is not null)
            systemInfoTools.SetModel(modelInfo);
        kernel.Plugins.AddFromObject(systemInfoTools, "system");

        return new CoreToolRegistration(taskTools, webSearchTools, processSessionManager, systemInfoTools);
    }
}
