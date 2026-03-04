using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.SemanticKernel.Extensions.Compaction;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Agents;

/// <summary>
/// Manages the conversation state, kernel, compaction, and persistence.
/// </summary>
public sealed class AgentSession
{
    private readonly IProviderRegistry _registry;
    private readonly List<ModelSwitchRecord> _modelSwitchHistory = [];
    private readonly List<ForkPoint> _forkPoints = [];
    private Kernel _kernel;
    private int _turnIndex;

    public AgentSession(
        IProviderRegistry registry,
        Kernel initialKernel,
        ProviderModelInfo initialModel)
    {
        _registry = registry;
        _kernel = initialKernel;
        CurrentModel = initialModel;
    }

    public ChatHistory History { get; } = new();
    public ProviderModelInfo? CurrentModel { get; private set; }
    public IReadOnlyList<ModelSwitchRecord> ModelSwitchHistory => _modelSwitchHistory;
    public IReadOnlyList<ForkPoint> ForkPoints => _forkPoints;
    public bool AutoRunEnabled { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "ProviderModelInfo is the event data")]
    public event EventHandler<ProviderModelInfo>? ModelChanged;

    /// <summary>
    /// When true, ALL tool confirmations are bypassed — no safety prompts at all.
    /// Set via --dangerously-skip-permissions or /permissions off.
    /// </summary>
    public bool SkipPermissions { get; set; }

    /// <summary>
    /// When true, the agent operates in plan-only mode (read/explore, no file writes).
    /// Toggled via the /plan slash command.
    /// </summary>
    public bool PlanMode { get; set; }

    /// <summary>
    /// When true, supported providers can automatically enable prompt caching.
    /// </summary>
    public bool PromptCachingEnabled { get; set; } = true;

    /// <summary>
    /// Prompt cache time-to-live used when prompt caching is enabled.
    /// </summary>
    public PromptCacheTtl PromptCacheTtl { get; set; } = PromptCacheTtl.FiveMinutes;

    /// <summary>
    /// When true, emit verbose diagnostics (tool calls, arguments) to stderr.
    /// Set via --verbose CLI flag.
    /// </summary>
    public bool Verbose { get; set; }

    public long TotalTokens { get; set; }

    public Kernel Kernel => _kernel;

    // ── Persistence ───────────────────────────────────────
    public SessionStore? Store { get; set; }
    public SessionInfo? SessionInfo { get; set; }

    /// <summary>Current turn being tracked (set by AgentLoop before each turn).</summary>
    public TurnRecord? CurrentTurn { get; set; }

    /// <summary>Record a user message turn and persist.</summary>
    public async Task RecordUserTurnAsync(string content)
    {
        if (SessionInfo == null || Store == null) return;

        var turn = new TurnRecord
        {
            SessionId = SessionInfo.Id,
            TurnIndex = _turnIndex++,
            Role = "user",
            Content = content,
        };
        SessionInfo.Turns.Add(turn);
        SessionInfo.MessageCount++;
        SessionInfo.UpdatedAt = DateTime.UtcNow;

        await Store.SaveTurnAsync(turn).ConfigureAwait(false);
        await Store.UpdateSessionAsync(SessionInfo).ConfigureAwait(false);
    }

    /// <summary>Record an assistant response turn and persist.</summary>
    public async Task RecordAssistantTurnAsync(
        string content,
        string? thinkingText = null,
        long tokensIn = 0,
        long tokensOut = 0,
        long durationMs = 0)
    {
        if (SessionInfo == null || Store == null) return;

        var turn = new TurnRecord
        {
            SessionId = SessionInfo.Id,
            TurnIndex = _turnIndex++,
            Role = "assistant",
            Content = content,
            ThinkingText = thinkingText,
            ModelId = CurrentModel?.Id,
            ProviderName = CurrentModel?.ProviderName,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            DurationMs = durationMs,
        };
        CurrentTurn = turn;
        SessionInfo.Turns.Add(turn);
        SessionInfo.MessageCount++;
        SessionInfo.TotalTokens += tokensIn + tokensOut;
        TotalTokens += tokensIn + tokensOut;
        SessionInfo.UpdatedAt = DateTime.UtcNow;

        await Store.SaveTurnAsync(turn).ConfigureAwait(false);
        SyncModelHistoryToSession();
        await Store.UpdateSessionAsync(SessionInfo).ConfigureAwait(false);
    }

    /// <summary>Sync in-memory model switch history and fork points to SessionInfo for persistence.</summary>
    private void SyncModelHistoryToSession()
    {
        if (SessionInfo == null) return;
        SessionInfo.ModelSwitchHistory.Clear();
        SessionInfo.ModelSwitchHistory.AddRange(_modelSwitchHistory);
        SessionInfo.ForkPoints.Clear();
        SessionInfo.ForkPoints.AddRange(_forkPoints);
    }
    public void RecordToolCall(string toolName, string? arguments, string? result, string status, long durationMs)
    {
        if (CurrentTurn == null) return;
        CurrentTurn.ToolCalls.Add(new ToolCallRecord
        {
            TurnId = CurrentTurn.Id,
            ToolName = toolName,
            Arguments = arguments,
            Result = result,
            Status = status,
            DurationMs = durationMs,
        });
    }

    /// <summary>Record a file operation on the current turn.</summary>
    public void RecordFileTouch(string filePath, string operation)
    {
        if (CurrentTurn == null) return;
        CurrentTurn.FilesTouched.Add(new FileTouchRecord
        {
            TurnId = CurrentTurn.Id,
            FilePath = filePath,
            Operation = operation,
        });
    }

    /// <summary>Initialize persistence — creates or resumes a session.</summary>
    public async Task InitializePersistenceAsync(string projectPath, string? resumeId = null)
    {
        Store = new SessionStore();
        await Store.InitializeAsync().ConfigureAwait(false);

        if (resumeId != null)
        {
            SessionInfo = await Store.GetSessionAsync(resumeId).ConfigureAwait(false);
            if (SessionInfo != null)
            {
                _turnIndex = SessionInfo.Turns.Count;
                // Restore model switch history and fork points
                _modelSwitchHistory.Clear();
                _modelSwitchHistory.AddRange(SessionInfo.ModelSwitchHistory);
                _forkPoints.Clear();
                _forkPoints.AddRange(SessionInfo.ForkPoints);
                // Restore chat history from persisted turns
                foreach (var turn in SessionInfo.Turns)
                {
                    if (string.Equals(turn.Role, "user", StringComparison.Ordinal))
                        History.AddUserMessage(turn.Content ?? string.Empty);
                    else if (string.Equals(turn.Role, "assistant", StringComparison.Ordinal))
                        History.AddAssistantMessage(turn.Content ?? string.Empty);
                }
                SessionInfo.IsActive = true;
                await Store.UpdateSessionAsync(SessionInfo).ConfigureAwait(false);
                return;
            }
        }

        SessionInfo = new SessionInfo
        {
            ProjectPath = projectPath,
            ProjectHash = ProjectHasher.Hash(projectPath),
            ModelId = CurrentModel?.Id,
            ProviderName = CurrentModel?.ProviderName,
        };
        await Store.CreateSessionAsync(SessionInfo).ConfigureAwait(false);
    }

    /// <summary>Export the current session to JSON.</summary>
    public async Task ExportSessionAsync()
    {
        if (SessionInfo == null) return;
        await SessionExporter.ExportAsync(SessionInfo).ConfigureAwait(false);
    }

    /// <summary>Close the session (mark inactive, export).</summary>
    public async Task CloseSessionAsync()
    {
        if (SessionInfo == null || Store == null) return;
        SessionInfo.IsActive = false;
        await Store.CloseSessionAsync(SessionInfo.Id).ConfigureAwait(false);
        await ExportSessionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Switches provider/model with conversation transformation.
    /// </summary>
    public async Task SwitchProviderAsync(
        ProviderModelInfo newModel,
        SwitchMode mode,
        CancellationToken ct = default)
    {
        var transformer = new ConversationTransformer();
        var (transformed, briefing) = await transformer.TransformAsync(
            History, _kernel, newModel, mode, ct).ConfigureAwait(false);

        if (!ReferenceEquals(transformed, History))
        {
            History.Clear();
            foreach (var msg in transformed)
            {
                History.Add(msg);
            }
        }

        SwitchModel(newModel, mode.ToString());

        if (mode == SwitchMode.Transform && briefing is not null)
        {
            History.AddAssistantMessage(briefing);
        }
    }

    /// <summary>
    /// Switches the backing LLM while preserving chat history and tools.
    /// </summary>
    public void SwitchModel(ProviderModelInfo model) => SwitchModel(model, "preserve");

    /// <summary>
    /// Switches the backing LLM with an explicit switch mode.
    /// </summary>
    public void SwitchModel(ProviderModelInfo model, string switchMode)
    {
        // Capture fork point from current state
        _forkPoints.Add(new ForkPoint
        {
            Id = _forkPoints.Count + 1,
            Timestamp = DateTimeOffset.UtcNow,
            ModelId = CurrentModel?.Id ?? string.Empty,
            ProviderName = CurrentModel?.ProviderName ?? string.Empty,
            TurnIndex = _turnIndex,
            MessageCount = History.Count,
        });

        var newKernel = _registry.BuildKernel(model);

        // Re-register plugins from the old kernel
        foreach (var plugin in _kernel.Plugins)
        {
            newKernel.Plugins.Add(plugin);
        }

        _kernel = newKernel;
        CurrentModel = model;

        // Record switch history
        _modelSwitchHistory.Add(new ModelSwitchRecord(
            DateTimeOffset.UtcNow,
            model.Id,
            model.ProviderName,
            switchMode));

        ModelChanged?.Invoke(this, model);
    }

    /// <summary>
    /// Fork the current session: clone history into a new session.
    /// </summary>
    public async Task<SessionInfo?> ForkSessionAsync(string? forkName = null)
    {
        if (SessionInfo == null || Store == null) return null;

        var forked = new SessionInfo
        {
            ProjectPath = SessionInfo.ProjectPath,
            ProjectHash = SessionInfo.ProjectHash,
            ModelId = CurrentModel?.Id,
            ProviderName = CurrentModel?.ProviderName,
            Name = forkName ?? $"fork of {SessionInfo.Name ?? SessionInfo.Id}",
        };
        await Store.CreateSessionAsync(forked).ConfigureAwait(false);

        // Copy turns
        var idx = 0;
        foreach (var turn in SessionInfo.Turns)
        {
            var clone = new TurnRecord
            {
                SessionId = forked.Id,
                TurnIndex = idx++,
                Role = turn.Role,
                Content = turn.Content,
                ThinkingText = turn.ThinkingText,
                ModelId = turn.ModelId,
                ProviderName = turn.ProviderName,
                TokensIn = turn.TokensIn,
                TokensOut = turn.TokensOut,
                DurationMs = turn.DurationMs,
            };
            forked.Turns.Add(clone);
            await Store.SaveTurnAsync(clone).ConfigureAwait(false);
        }

        forked.MessageCount = SessionInfo.MessageCount;
        forked.TotalTokens = SessionInfo.TotalTokens;
        await Store.UpdateSessionAsync(forked).ConfigureAwait(false);

        return forked;
    }

    /// <summary>
    /// Clears conversation history.
    /// </summary>
    public void ClearHistory()
    {
        History.Clear();
        TotalTokens = 0;
    }

    /// <summary>
    /// Forces compaction of the chat history using hierarchical summarization.
    /// </summary>
    public async Task CompactAsync(CancellationToken ct = default)
    {
        var tokenCount = TokenEstimator.EstimateTokens(History);
        if (tokenCount <= 2000)
        {
            return;
        }

        var strategy = new HierarchicalSummarizationStrategy();
        var options = new CompactionOptions
        {
            MaxContextWindowTokens = 4000,
            TargetCompressionRatio = 0.4,
            MinMessagesBeforeCompaction = 1,
        };

        var compacted = await strategy.CompactAsync(
            History, _kernel, options, ct).ConfigureAwait(false);

        History.Clear();
        foreach (var msg in compacted)
        {
            History.Add(msg);
        }
    }
}
