using JD.AI.Core.Agents;
using JD.AI.Core.Channels;
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
        // Stateless tools
        kernel.Plugins.AddFromType<FileTools>("file");
        kernel.Plugins.AddFromType<SearchTools>("search");
        kernel.Plugins.AddFromType<ShellTools>("shell");
        kernel.Plugins.AddFromType<GitTools>("git");
        kernel.Plugins.AddFromType<GitHubTools>("github");
        kernel.Plugins.AddFromType<WebTools>("web");
        kernel.Plugins.AddFromType<BrowserTools>("browser");
        kernel.Plugins.AddFromType<ThinkTools>("think");
        kernel.Plugins.AddFromType<EnvironmentTools>("environment");
        kernel.Plugins.AddFromType<NotebookTools>("notebook");
        kernel.Plugins.AddFromType<ClipboardTools>("clipboard");
        kernel.Plugins.AddFromType<DiffTools>("diff");
        kernel.Plugins.AddFromType<BatchEditTools>("batchEdit");
        kernel.Plugins.AddFromType<MultimodalTools>("multimodal");
        kernel.Plugins.AddFromType<ParityDocsTools>("parityDocs");
        kernel.Plugins.AddFromType<McpTransportTools>("mcp");
        kernel.Plugins.AddFromType<MigrationTools>("migration");
        kernel.Plugins.AddFromType<SkillParityTools>("skillParity");
        kernel.Plugins.AddFromType<McpEcosystemTools>("mcpEcosystem");
        kernel.Plugins.AddFromType<TailscaleTools>("tailscale");
        kernel.Plugins.AddFromType<EncodingCryptoTools>("encoding");

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
