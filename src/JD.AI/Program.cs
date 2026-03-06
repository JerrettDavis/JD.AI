using JD.AI;
using JD.AI.Agent;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Channels;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Mcp;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Providers.ModelSearch;
using JD.AI.Core.Safety;
using JD.AI.Core.Skills;
using JD.AI.Core.Tools;
using JD.AI.Core.Usage;
using JD.AI.Rendering;
using JD.AI.Startup;
using JD.AI.Tools;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using JD.SemanticKernel.Extensions.Compaction;
using JD.SemanticKernel.Extensions.Hooks;
using JD.SemanticKernel.Extensions.Plugins;
using JD.SemanticKernel.Extensions.Skills;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Spectre.Console;

// ──────────────────────────────────────────────────────────
//  jdai — Semantic Kernel TUI Agent
// ──────────────────────────────────────────────────────────

// Ensure console uses UTF-8 so Unicode glyphs (Braille spinners, box-drawing,
// checkmarks, etc.) render correctly on all platforms.
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

// Parse CLI flags
var opts = await CliArgumentParser.ParseAsync(args).ConfigureAwait(false);

// Handle CLI subcommands early (before provider detection)
if (opts.Subcommand != null)
{
    return opts.Subcommand switch
    {
        "mcp" => await McpCliHandler.RunAsync(opts.SubcommandArgs).ConfigureAwait(false),
        "plugin" => await PluginCliHandler.RunAsync(opts.SubcommandArgs).ConfigureAwait(false),
        "onboard" or "wizard" => await OnboardingCliHandler.RunAsync(opts.SubcommandArgs).ConfigureAwait(false),
        "update" or "install" => await UpdateCliHandler.RunAsync(opts.Subcommand, opts.SubcommandArgs).ConfigureAwait(false),
        _ => 1,
    };
}

// --gateway: start the Gateway as an embedded ASP.NET host alongside the TUI
Microsoft.AspNetCore.Builder.WebApplication? gatewayHost = null;
if (opts.GatewayMode)
{
    var port = opts.GatewayPort ?? "5100";
    var gwBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(
        ["--urls", $"http://{GatewayRuntimeDefaults.DefaultHost}:{port}"]);
    gwBuilder.Logging.SetMinimumLevel(LogLevel.Warning);

    var gwApp = gwBuilder.Build();
    gwApp.MapGet(GatewayRuntimeDefaults.HealthPath, () => Results.Ok(new { Status = "Healthy" }));
    gwApp.MapGet(GatewayRuntimeDefaults.ReadyPath, () => Results.Ok(new { Status = "Ready" }));

    gatewayHost = gwApp;
    _ = gwApp.StartAsync();
    if (!opts.PrintMode)
    {
        AnsiConsole.MarkupLine($"[dim]Gateway started on http://{GatewayRuntimeDefaults.DefaultHost}:{port}[/]");
    }
}

// Clean up leftover backup from a previous native binary update
var oldBinary = Environment.ProcessPath + ".old";
if (oldBinary is not null && File.Exists(oldBinary))
{
    try { File.Delete(oldBinary); }
#pragma warning disable CA1031
    catch { /* best-effort cleanup */ }
#pragma warning restore CA1031
}

// Fire background update check immediately (non-blocking)
var updateCheckTask = UpdateChecker.CheckAsync(opts.ForceUpdateCheck);

// 1-3. Detect providers, list models, select model
using var configStore = new AtomicConfigStore();
var providerSetup = await ProviderOrchestrator.DetectAndSelectAsync(opts, configStore).ConfigureAwait(false);
if (providerSetup is null) return 1;

var registry = providerSetup.Registry;
var providerConfig = providerSetup.ProviderConfig;
var metadataProvider = providerSetup.MetadataProvider;
var allModels = providerSetup.AllModels;
var selectedModel = providerSetup.SelectedModel;
var kernel = providerSetup.Kernel;

// 4-5. Create and configure agent session (persistence, resume, worktree, etc.)
var sessionSetup = await SessionConfigurator.ConfigureAsync(opts, providerSetup).ConfigureAwait(false);
var session = sessionSetup.Session;
selectedModel = sessionSetup.SelectedModel;
kernel = sessionSetup.Kernel;
var projectPath = sessionSetup.ProjectPath;
var worktreeManager = sessionSetup.WorktreeManager;

// 6. Register built-in tools
var toolReg = ToolRegistrar.RegisterAll(kernel, session, selectedModel);

// 7. Load managed skills with precedence + metadata gating + hot reload support
var userClaudeSkillsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");
var workspaceClaudeSkillsDir = Path.Combine(Directory.GetCurrentDirectory(), ".claude", "skills");
var managedSkillsDir = Path.Combine(DataDirectories.Root, "skills");
var workspaceSkillsDir = Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "skills");
var bundledSkillsDir = Path.Combine(AppContext.BaseDirectory, "skills");
var userSkillsConfigPath = Path.Combine(DataDirectories.Root, "skills.json");
var workspaceSkillsConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "skills.json");

using var skillLifecycleManager = new SkillLifecycleManager(
    [
        new SkillSourceDirectory("bundled", bundledSkillsDir, SkillSourceKind.Bundled, 0),
        new SkillSourceDirectory("managed-legacy", userClaudeSkillsDir, SkillSourceKind.Managed, -1),
        new SkillSourceDirectory("managed", managedSkillsDir, SkillSourceKind.Managed, 0),
        new SkillSourceDirectory("workspace-legacy", workspaceClaudeSkillsDir, SkillSourceKind.Workspace, -1),
        new SkillSourceDirectory("workspace", workspaceSkillsDir, SkillSourceKind.Workspace, 0),
    ],
    userConfigPath: userSkillsConfigPath,
    workspaceConfigPath: workspaceSkillsConfigPath);

var loadedSkillPluginNames = new HashSet<string>(StringComparer.Ordinal);
var skillsStagingDir = Path.Combine(DataDirectories.Root, "runtime", "skills");

void CopyDirectory(string sourceDir, string targetDir)
{
    Directory.CreateDirectory(targetDir);
    foreach (var file in Directory.EnumerateFiles(sourceDir))
    {
        var destination = Path.Combine(targetDir, Path.GetFileName(file));
        File.Copy(file, destination, overwrite: true);
    }

    foreach (var dir in Directory.EnumerateDirectories(sourceDir))
    {
        var destination = Path.Combine(targetDir, Path.GetFileName(dir));
        CopyDirectory(dir, destination);
    }
}

void StageSkills(IReadOnlyList<ActiveSkill> activeSkills)
{
    if (Directory.Exists(skillsStagingDir))
        Directory.Delete(skillsStagingDir, recursive: true);

    Directory.CreateDirectory(skillsStagingDir);

    foreach (var skill in activeSkills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
    {
        var folderName = string.Concat(
            skill.SkillKey.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = $"skill_{Guid.NewGuid():N}";

        var destination = Path.Combine(skillsStagingDir, folderName);
        if (Directory.Exists(destination))
            destination = Path.Combine(skillsStagingDir, $"{folderName}_{Guid.NewGuid():N}");

        CopyDirectory(skill.DirectoryPath, destination);
    }
}

void UnloadManagedSkillPlugins()
{
    foreach (var pluginName in loadedSkillPluginNames.ToArray())
    {
        if (kernel.Plugins.TryGetPlugin(pluginName, out var plugin))
            kernel.Plugins.Remove(plugin);
    }

    loadedSkillPluginNames.Clear();
}

void ApplySkillsSnapshot(SkillSnapshot snapshot, bool announceReload)
{
    UnloadManagedSkillPlugins();

    if (snapshot.ActiveSkills.Count == 0)
    {
        if (!opts.PrintMode && announceReload)
            ChatRenderer.RenderInfo("  Skills reloaded (0 active).");
        return;
    }

    StageSkills(snapshot.ActiveSkills);
    var builder = Kernel.CreateBuilder();
    JD.SemanticKernel.Extensions.Skills.KernelBuilderExtensions.UseSkills(
        builder, skillsStagingDir, opts => opts.PluginName = "skillsManaged");
    var skillKernel = builder.Build();

    foreach (var plugin in skillKernel.Plugins)
    {
        if (kernel.Plugins.TryGetPlugin(plugin.Name, out var existing))
            kernel.Plugins.Remove(existing);

        kernel.Plugins.Add(plugin);
        loadedSkillPluginNames.Add(plugin.Name);
    }

    if (!opts.PrintMode && announceReload)
        ChatRenderer.RenderInfo($"  Skills reloaded ({snapshot.ActiveSkills.Count} active).");
}

void RefreshSkills(bool announceReload)
{
    if (!skillLifecycleManager.TryRefresh(out var snapshot))
        return;

    ApplySkillsSnapshot(snapshot, announceReload);
}

RefreshSkills(announceReload: false);

var pluginDirs = new[]
{
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "plugins"),
    Path.Combine(Directory.GetCurrentDirectory(), ".claude", "plugins"),
    Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "plugins"),
};

foreach (var dir in pluginDirs.Where(Directory.Exists))
{
    try
    {
        var builder = Kernel.CreateBuilder();
        JD.SemanticKernel.Extensions.Plugins.KernelBuilderExtensions.UseAllPlugins(builder, dir);
        var pluginKernel = builder.Build();
        foreach (var plugin in pluginKernel.Plugins)
        {
            if (kernel.Plugins.TryGetPlugin(plugin.Name, out _))
            {
                if (!opts.PrintMode) ChatRenderer.RenderWarning($"  Skipped duplicate plugin '{plugin.Name}' from {dir}");
                continue;
            }

            kernel.Plugins.Add(plugin);
        }

        if (!opts.PrintMode) ChatRenderer.RenderInfo($"  Loaded plugins from {dir}");
    }
#pragma warning disable CA1031
    catch (Exception ex)
    {
        if (!opts.PrintMode) ChatRenderer.RenderWarning($"  Failed to load plugins from {dir}: {ex.Message}");
    }
#pragma warning restore CA1031
}

// 7b. Load installed SDK plugins from the JD.AI plugin registry
var pluginLoader = new JD.AI.Core.Plugins.PluginLoader(
    NullLogger<JD.AI.Core.Plugins.PluginLoader>.Instance);
var pluginRegistry = new PluginRegistryStore();
var pluginInstaller = new PluginInstaller(
    new HttpClient(),
    NullLogger<PluginInstaller>.Instance);
var pluginContextFactory = new DelegatePluginContextFactory(
    () => new TerminalPluginContext(kernel));
var pluginManager = new PluginLifecycleManager(
    pluginInstaller,
    pluginRegistry,
    pluginLoader,
    pluginContextFactory,
    NullLogger<PluginLifecycleManager>.Instance);
await pluginManager.LoadEnabledAsync().ConfigureAwait(false);

// 8. Governance, audit, budget, circuit breaker, tool filtering
var governance = GovernanceInitializer.Initialize(projectPath, session, kernel, opts, opts.MaxBudgetUsd);
using var _budgetTracker = governance.BudgetTracker;
using var _fileAuditSink = governance.FileAuditSink;

// 9. Build system prompt
var systemPrompt = await SystemPromptBuilder.BuildAsync(opts, governance.Instructions, session.PlanMode)
    .ConfigureAwait(false);
session.History.AddSystemMessage(systemPrompt);

// 10. Print mode: non-interactive execution
if (opts.PrintMode)
{
    return await PrintModeRunner.RunAsync(opts, session, selectedModel, skillLifecycleManager)
        .ConfigureAwait(false);
}

// 11. Show update notification
var pendingUpdate = await updateCheckTask.ConfigureAwait(false);
if (pendingUpdate is not null)
{
    AnsiConsole.MarkupLine(UpdatePrompter.FormatNotification(pendingUpdate));
    AnsiConsole.WriteLine();
}

// 12. Interactive TUI loop
var loop = new InteractiveLoop(
    session, opts, selectedModel, allModels, kernel, registry,
    providerConfig, configStore, metadataProvider, governance,
    skillLifecycleManager, RefreshSkills, systemPrompt,
    pluginLoader, pluginManager);
var exitCode = await loop.RunAsync().ConfigureAwait(false);

// Cleanup
if (worktreeManager is not null)
{
    ChatRenderer.RenderInfo("Cleaning up worktree...");
    await worktreeManager.DisposeAsync().ConfigureAwait(false);
}

if (gatewayHost is not null)
{
    await gatewayHost.StopAsync().ConfigureAwait(false);
    (gatewayHost as IDisposable)?.Dispose();
}

return exitCode;
