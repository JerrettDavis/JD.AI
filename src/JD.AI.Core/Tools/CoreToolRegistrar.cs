using JD.AI.Core.Infrastructure;
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
    ProcessSessionManager ProcessSessionManager);

/// <summary>
/// Registers essential kernel tool plugins that work without session infrastructure.
/// This is the shared registration path used by both the CLI and the Daemon/Gateway.
/// </summary>
public static class CoreToolRegistrar
{
    /// <summary>
    /// Registers core tools on a Semantic Kernel instance.
    /// Includes: file ops, exec/shell, web search, memory, tasks, capabilities, scheduler.
    /// Does NOT require <c>AgentSession</c> or <c>ProviderModelInfo</c>.
    /// </summary>
    public static CoreToolRegistration Register(Kernel kernel)
    {
        // Stateless tools — auto-discovered via [ToolPlugin(RequiresInjection = false)]
        // This picks up FileTools and other [ToolPlugin]-attributed classes
        ToolAssemblyScanner.RegisterStaticPlugins(kernel, typeof(FileTools).Assembly);

        // Memory
        kernel.Plugins.AddFromObject(new MemoryTools(), "memory");

        // Tasks
        var taskTools = new TaskTools();
        kernel.Plugins.AddFromObject(taskTools, "tasks");

        // Capabilities (introspection — lets the agent discover what tools it has)
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

        // Gateway ops (optional — works with or without a configured gateway URL)
        kernel.Plugins.AddFromObject(
            new GatewayOpsTools(Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL")), "gateway");

        return new CoreToolRegistration(taskTools, webSearchTools, processSessionManager);
    }
}
