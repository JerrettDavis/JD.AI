using JD.AI.Core.Agents;
using JD.AI.Core.Memory;
using JD.AI.Core.Providers;
using JD.AI.Rendering;
using Microsoft.SemanticKernel;

namespace JD.AI.Startup;

/// <summary>
/// Creates and configures <see cref="AgentSession"/> instances, including
/// persistence, resume, fork, worktree, and model restoration.
/// Extracted from Program.cs lines 338-539.
/// </summary>
internal static class SessionConfigurator
{
    public static async Task<SessionSetup> ConfigureAsync(
        CliOptions opts,
        ProviderSetup providerSetup)
    {
        var selectedModel = providerSetup.SelectedModel;
        var kernel = providerSetup.Kernel;
        var registry = providerSetup.Registry;
        var allModels = providerSetup.AllModels;

        var session = new AgentSession(registry, kernel, selectedModel);

        // Memory service for per-project daily logs and long-term context
        session.MemoryService = new MemoryService();

        // Apply CLI flags
        if (opts.SkipPermissions)
        {
            session.SkipPermissions = true;
            if (!opts.PrintMode) ChatRenderer.RenderWarning("--dangerously-skip-permissions: ALL tool confirmations disabled.");
        }

        if (opts.VerboseMode)
        {
            session.Verbose = true;
        }

        // Permission mode
        if (opts.PermissionModeStr != null)
        {
            session.PermissionMode = opts.PermissionModeStr.ToUpperInvariant() switch
            {
                "PLAN" => PermissionMode.Plan,
                "ACCEPTEDITS" => PermissionMode.AcceptEdits,
                "DONTASK" => PermissionMode.BypassAll,
                "NORMAL" => PermissionMode.Normal,
                _ => PermissionMode.Normal,
            };
            if (!opts.PrintMode)
            {
                ChatRenderer.RenderInfo($"Permission mode: {session.PermissionMode}");
            }
        }

        // Fallback models
        if (opts.FallbackModels.Length > 0)
        {
            session.FallbackModels = opts.FallbackModels;
            if (!opts.PrintMode)
            {
                ChatRenderer.RenderInfo($"Fallback models: {string.Join(" → ", opts.FallbackModels)}");
            }
        }

        else if (providerSetup.RoutedFallbackModels.Count > 0)
        {
            session.FallbackModels = providerSetup.RoutedFallbackModels;
            if (!opts.PrintMode)
            {
                ChatRenderer.RenderInfo($"Routed fallback models: {string.Join(" → ", providerSetup.RoutedFallbackModels)}");
            }
        }

        if (opts.NoSessionPersistence)
        {
            session.NoSessionPersistence = true;
        }

        // Budget
        if (opts.MaxBudgetUsd.HasValue)
        {
            session.MaxBudgetUsd = opts.MaxBudgetUsd;
            if (!opts.PrintMode) ChatRenderer.RenderInfo($"Budget limit: ${opts.MaxBudgetUsd:F2}");
        }

        // Debug logging
        if (opts.DebugMode)
        {
            session.Verbose = true;
            var parsedCategories = JD.AI.Core.Tracing.DebugLogger.ParseCategories(opts.DebugCategories);
            JD.AI.Core.Tracing.DebugLogger.Enable(parsedCategories);
            if (!opts.PrintMode)
            {
                var cats = opts.DebugCategories != null ? $" (categories: {opts.DebugCategories})" : "";
                ChatRenderer.RenderInfo($"Debug logging enabled{cats}");
            }
        }

        // Worktree
        var projectPath = Directory.GetCurrentDirectory();
        JD.AI.Core.Tools.WorktreeManager? worktreeManager = null;
        if (opts.UseWorktree)
        {
            try
            {
                worktreeManager = new JD.AI.Core.Tools.WorktreeManager(projectPath);
                var wtPath = await worktreeManager.CreateAsync().ConfigureAwait(false);
                projectPath = wtPath;
                Directory.SetCurrentDirectory(wtPath);
                if (!opts.PrintMode)
                {
                    ChatRenderer.RenderInfo($"Worktree created: {wtPath}");
                    ChatRenderer.RenderInfo($"  Branch: {worktreeManager.BranchName}");
                }
            }
#pragma warning disable CA1031 // best effort for worktree
            catch (Exception ex)
            {
                ChatRenderer.RenderWarning($"Failed to create worktree: {ex.Message}");
                worktreeManager = null;
            }
#pragma warning restore CA1031
        }

        // Session persistence
        var resumeId = opts.ResumeId;
        if (opts.NoSessionPersistence)
        {
            if (!opts.PrintMode) ChatRenderer.RenderInfo("Session persistence disabled.");
        }
        else if (!opts.IsNewSession)
        {
            if (opts.CliSessionId != null)
            {
                resumeId = opts.CliSessionId;
            }

            if (opts.ContinueSession && resumeId == null)
            {
                using var store = new JD.AI.Core.Sessions.SessionStore();
                await store.InitializeAsync().ConfigureAwait(false);
                var projectHash = JD.AI.Core.Sessions.ProjectHasher.Hash(projectPath);
                var recentSessions = await store.ListSessionsAsync(projectHash, 1).ConfigureAwait(false);
                if (recentSessions.Count > 0)
                {
                    resumeId = recentSessions[0].Id;
                }
            }

            await session.InitializePersistenceAsync(projectPath, resumeId).ConfigureAwait(false);
            if (resumeId != null && session.SessionInfo != null)
            {
                RestoreSessionModel(session, allModels, registry, opts.PrintMode, out selectedModel, out kernel);

                if (!opts.PrintMode) ChatRenderer.RenderInfo($"Resumed session: {session.SessionInfo.Name ?? session.SessionInfo.Id} ({session.SessionInfo.Turns.Count} turns)");

                if (opts.ForkSession)
                {
                    await session.ForkSessionAsync("CLI fork").ConfigureAwait(false);
                    if (!opts.PrintMode) ChatRenderer.RenderInfo("Forked session — changes diverge from here.");
                }
            }
        }
        else
        {
            await session.InitializePersistenceAsync(projectPath).ConfigureAwait(false);
        }

        return new SessionSetup(session, selectedModel, kernel, projectPath, worktreeManager);
    }

    private static void RestoreSessionModel(
        AgentSession session,
        IReadOnlyList<ProviderModelInfo> allModels,
        ProviderRegistry registry,
        bool printMode,
        out ProviderModelInfo selectedModel,
        out Kernel kernel)
    {
        selectedModel = session.CurrentModel!;
        kernel = session.Kernel;

        var lastSwitch = session.SessionInfo!.ModelSwitchHistory.LastOrDefault();
        ProviderModelInfo? restored = null;

        if (lastSwitch != null)
        {
            restored = allModels.FirstOrDefault(m =>
                string.Equals(m.Id, lastSwitch.ModelId, StringComparison.Ordinal) &&
                string.Equals(m.ProviderName, lastSwitch.ProviderName, StringComparison.Ordinal));
        }
        else if (session.SessionInfo.ModelId != null && session.SessionInfo.ProviderName != null)
        {
            restored = allModels.FirstOrDefault(m =>
                string.Equals(m.Id, session.SessionInfo.ModelId, StringComparison.Ordinal) &&
                string.Equals(m.ProviderName, session.SessionInfo.ProviderName, StringComparison.Ordinal));

            if (restored != null && string.Equals(restored.Id, selectedModel.Id, StringComparison.Ordinal))
            {
                restored = null; // same model, no need to rebuild
            }
        }

        if (restored is null)
        {
            return;
        }

        selectedModel = restored;
        kernel = registry.BuildKernel(selectedModel);
        session.RestorePersistedModel(selectedModel, kernel);
        if (!printMode) ChatRenderer.RenderInfo($"Restored model: [{restored.ProviderName}] {restored.DisplayName}");
    }
}

/// <summary>
/// Result of session configuration.
/// </summary>
internal sealed record SessionSetup(
    AgentSession Session,
    ProviderModelInfo SelectedModel,
    Kernel Kernel,
    string ProjectPath,
    JD.AI.Core.Tools.WorktreeManager? WorktreeManager);
