using JD.AI.Core.Agents;
using JD.AI.Core.Channels;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Providers;
using JD.AI.Core.Tools;
using JD.AI.Rendering;
using JD.AI.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Startup;

/// <summary>
/// Holds references to stateful tool instances that need to be accessed
/// after registration (e.g. for usage tracking, plugin interop).
/// </summary>
internal sealed record ToolRegistration(
    UsageTools UsageTools,
    TaskTools TaskTools,
    WebSearchTools WebSearchTools,
    ProcessSessionManager ProcessSessionManager);

/// <summary>
/// Registers all built-in kernel tool plugins.
/// Extracted from Program.cs lines 103-148.
/// </summary>
internal static class ToolRegistrar
{
    public static ToolRegistration RegisterAll(
        Kernel kernel,
        AgentSession session,
        ProviderModelInfo selectedModel)
    {
        // Stateless tools — auto-discovered via [ToolPlugin(RequiresInjection = false)]
        ToolAssemblyScanner.RegisterStaticPlugins(kernel, typeof(FileTools).Assembly);

        // Stateful tools
        kernel.Plugins.AddFromObject(new MemoryTools(), "memory");

        var taskTools = new TaskTools();
        kernel.Plugins.AddFromObject(taskTools, "tasks");

        var usageTools = new UsageTools();
        usageTools.SetModel(selectedModel);
        kernel.Plugins.AddFromObject(usageTools, "usage");

        var capabilityTools = new CapabilityTools(kernel);
        kernel.Plugins.AddFromObject(capabilityTools, "capabilities");
        kernel.Plugins.AddFromObject(new BenchmarkTools(kernel), "benchmark");
        kernel.Plugins.AddFromObject(
            new QuestionTools(req => QuestionnaireSession.Run(req)), "questions");

        var processSessionManager = new ProcessSessionManager();
        kernel.Plugins.AddFromObject(new ExecProcessTools(processSessionManager), "runtime");

        var webSearchTools = new WebSearchTools();
        kernel.ImportPluginFromObject(webSearchTools, "WebSearchTools");
        kernel.ImportPluginFromObject(
            new OpenClawCompatibilityTools(taskTools, webSearchTools), "openclaw");

        kernel.Plugins.AddFromObject(new SessionOrchestrationTools(session), "sessions");
        kernel.Plugins.AddFromObject(new SchedulerTools(), "scheduler");
        kernel.Plugins.AddFromObject(
            new GatewayOpsTools(Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL")), "gateway");

        // Channel ops
        var channelRegistry = new ChannelRegistry();
        kernel.Plugins.AddFromObject(new ChannelOpsTools(channelRegistry), "channels");

        return new ToolRegistration(usageTools, taskTools, webSearchTools, processSessionManager);
    }
}
