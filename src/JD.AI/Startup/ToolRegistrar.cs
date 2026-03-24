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
/// Registers all built-in kernel tool plugins for the CLI.
/// Delegates core tool registration to <see cref="CoreToolRegistrar"/>
/// (shared with Daemon/Gateway), then adds CLI-specific session-dependent tools.
/// </summary>
internal static class ToolRegistrar
{
    public static ToolRegistration RegisterAll(
        Kernel kernel,
        AgentSession session,
        ProviderModelInfo selectedModel)
    {
        // Register core tools (shared path — also used by Daemon/Gateway)
        var core = CoreToolRegistrar.Register(kernel, selectedModel);

        // Keep SystemInfoTools in sync when the model changes
        session.ModelChanged += (_, model) => core.SystemInfoTools.SetModel(model);

        // CLI-only session-dependent tools below

        var usageTools = new UsageTools();
        usageTools.SetModel(selectedModel);
        session.ModelChanged += (_, model) => usageTools.SetModel(model);
        kernel.Plugins.AddFromObject(usageTools, "usage");

        kernel.Plugins.AddFromObject(new BenchmarkTools(kernel), "benchmark");
        kernel.Plugins.AddFromObject(
            new QuestionTools(req => QuestionnaireSession.Run(req)), "questions");

        kernel.ImportPluginFromObject(
            new OpenClawCompatibilityTools(core.TaskTools, core.WebSearchTools), "openclaw");

        kernel.Plugins.AddFromObject(new SessionOrchestrationTools(session), "sessions");

        // Channel ops
        var channelRegistry = new ChannelRegistry();
        kernel.Plugins.AddFromObject(new ChannelOpsTools(channelRegistry), "channels");

        return new ToolRegistration(
            usageTools, core.TaskTools, core.WebSearchTools, core.ProcessSessionManager);
    }
}
