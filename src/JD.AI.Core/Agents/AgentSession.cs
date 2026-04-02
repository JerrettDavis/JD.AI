using System.Diagnostics;
using JD.AI.Core.Events;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Memory;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;
using JD.AI.Core.Tracing;
using JD.AI.Core.Usage;
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
    private readonly Lock _toolFingerprintLock = new();
    private readonly HashSet<string> _toolFingerprintsThisTurn = new(StringComparer.OrdinalIgnoreCase);
    private Kernel _kernel;
    private int _turnIndex;
    private readonly List<PendingToolCall> _pendingToolCalls = [];

    /// <summary>Current turn index (0-based).</summary>
    public int TurnIndex => _turnIndex;

    public AgentSession(
        IProviderRegistry registry,
        Kernel initialKernel,
        ProviderModelInfo initialModel)
    {
        _registry = registry;
        _kernel = initialKernel;
        CurrentModel = initialModel;
    }

    /// <summary>Optional audit service for emitting session lifecycle events.</summary>
    public AuditService? AuditService { get; set; }

    /// <summary>Optional event bus for emitting tool audit events.</summary>
    public IEventBus? EventBus { get; set; }

    /// <summary>Optional usage meter for centralized metering.</summary>
    public IUsageMeter? UsageMeter { get; set; }

    /// <summary>Optional memory service for per-project daily logs and long-term memory.</summary>
    public IMemoryService? MemoryService { get; set; }

    // ── System prompt cache ──────────────────────────────────
    private string? _cachedSystemPromptText;
    private int _cachedSystemPromptTokens;
    private string? _originalSystemPromptText;

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
    /// Controls the permission model for tool invocations within the session.
    /// </summary>
    public PermissionMode PermissionMode { get; set; } = PermissionMode.Normal;

    /// <summary>
    /// Explicit allow/deny rules for tool invocations.
    /// A tool must either match an allow rule or receive per-call user confirmation.
    /// </summary>
    public ToolPermissionProfile ToolPermissionProfile { get; set; } = new();

    /// <summary>
    /// Fallback model chain — used when the primary model returns 429/503/timeout.
    /// </summary>
    public IReadOnlyList<string> FallbackModels { get; set; } = [];

    /// <summary>
    /// When true, session persistence is disabled entirely.
    /// </summary>
    public bool NoSessionPersistence { get; set; }

    /// <summary>
    /// Per-session budget limit in USD (set via <c>--max-budget-usd</c>).
    /// When exceeded the agent stops processing turns.
    /// </summary>
    public decimal? MaxBudgetUsd { get; set; }

    /// <summary>
    /// Accumulated estimated spend for this session in USD.
    /// Updated after each turn by the budget tracker.
    /// </summary>
    public decimal SessionSpendUsd { get; set; }

    /// <summary>
    /// When true, the agent operates in plan-only mode (read/explore, no file writes).
    /// Toggled via the /plan slash command.
    /// </summary>
    public bool PlanMode { get; set; }

    /// <summary>
    /// The name of the active <see cref="JD.AI.Core.Tools.ToolLoadout"/> for this session.
    /// When set, agents and tooling systems may use this to filter which plugins are
    /// exposed. Set to <see langword="null"/> to expose all registered plugins (default).
    /// </summary>
    public string? ActiveLoadoutName { get; set; }

    /// <summary>
    /// The tool loadout registry for this session, used by loadout-aware scoping.
    /// </summary>
    public IToolLoadoutRegistry? LoadoutRegistry { get; set; }

    /// <summary>
    /// The name of the currently executing workflow, if any.
    /// When set, tool calls are recognized as workflow-coordinated and skip the
    /// workflow enforcement prompt.
    /// </summary>
    public string? ActiveWorkflowName { get; set; }

    /// <summary>
    /// When true, the user has declined the workflow prompt for the current turn.
    /// Reset at the start of each turn by <see cref="ResetTurnState"/>.
    /// </summary>
    public bool WorkflowDeclinedThisTurn { get; set; }

    /// <summary>
    /// Tool calls captured during an active workflow recording session.
    /// Populated by ToolConfirmationFilter, consumed by AgentLoop at turn end.
    /// </summary>
    public List<(string ToolName, string? Args)> CapturedWorkflowSteps { get; } = [];

    /// <summary>
    /// Tools the user has already approved once for this session.
    /// Shared across structured and text-emitted tool call paths.
    /// </summary>
    public HashSet<string> ConfirmedOnceTools { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Callback to save a captured workflow. Set during initialization by the host
    /// (e.g., InteractiveLoop) that has access to the workflow catalog.
    /// Parameters: (workflowName, steps as list of (toolName, args)), returns saved workflow name.
    /// </summary>
    public Func<string, IReadOnlyList<(string ToolName, string? Args)>, CancellationToken, Task<string>>?
        SaveCapturedWorkflowAsync
    { get; set; }

    /// <summary>
    /// Resets per-turn transient state. Called at the start of each agent turn.
    /// </summary>
    public void ResetTurnState()
    {
        WorkflowDeclinedThisTurn = false;
        _pendingToolCalls.Clear();
        lock (_toolFingerprintLock)
        {
            _toolFingerprintsThisTurn.Clear();
        }
        // Don't clear CapturedWorkflowSteps here — they persist across the turn
    }

    /// <summary>
    /// Registers a tool-call fingerprint for the current turn.
    /// Returns false when the same tool/argument signature was already seen.
    /// </summary>
    public bool TryRegisterToolCallForCurrentTurn(string canonicalToolName, string? argsSummary)
    {
        var key = $"{canonicalToolName.Trim()}|{(argsSummary ?? string.Empty).Trim()}";
        lock (_toolFingerprintLock)
        {
            return _toolFingerprintsThisTurn.Add(key);
        }
    }

    /// <summary>
    /// Snapshot of all registered plugins before loadout scoping is applied.
    /// </summary>
    public IReadOnlyList<KernelPlugin>? AllPlugins { get; set; }

    /// <summary>
    /// Registry of agent definitions loaded from <c>*.agent.yaml</c> files.
    /// </summary>
    public IAgentDefinitionRegistry? AgentDefinitionRegistry { get; set; }

    /// <summary>
    /// Safety tier map for text-based tool call validation.
    /// Set during initialization from ToolAssemblyScanner.
    /// </summary>
    public IReadOnlyDictionary<string, Tools.SafetyTier>? ToolSafetyTiers { get; set; }

    /// <summary>
    /// Approval service for human-in-the-loop or policy-based operation gates.
    /// When <c>null</c>, approval is not enforced.
    /// </summary>
    public JD.AI.Core.Governance.IApprovalService? ApprovalService { get; set; }

    /// <summary>
    /// When true, supported providers can automatically enable prompt caching.
    /// </summary>
    public bool PromptCachingEnabled { get; set; } = true;

    /// <summary>
    /// Prompt cache time-to-live used when prompt caching is enabled.
    /// </summary>
    public PromptCacheTtl PromptCacheTtl { get; set; } = PromptCacheTtl.FiveMinutes;

    /// <summary>
    /// Session-level reasoning effort override. Null means provider/model default ("auto").
    /// </summary>
    public ReasoningEffort? ReasoningEffortOverride { get; set; }

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

    /// <summary>Execution timeline from the most recent turn, used by <c>/trace</c>.</summary>
    public ExecutionTimeline? LastTimeline { get; set; }

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
            ContextWindowTokens = CurrentModel?.ContextWindowTokens ?? 0,
        };
        CurrentTurn = turn;
        foreach (var pending in _pendingToolCalls)
        {
            turn.ToolCalls.Add(new ToolCallRecord
            {
                TurnId = turn.Id,
                ToolName = pending.ToolName,
                Arguments = pending.Arguments,
                Result = pending.Result,
                Status = pending.Status,
                DurationMs = pending.DurationMs,
                CreatedAt = pending.CreatedAt,
            });
        }
        _pendingToolCalls.Clear();
        SessionInfo.Turns.Add(turn);
        SessionInfo.MessageCount++;
        SessionInfo.TotalTokens += tokensIn + tokensOut;
        TotalTokens += tokensIn + tokensOut;
        SessionInfo.UpdatedAt = DateTime.UtcNow;
        turn.CumulativeContextTokens = TokenEstimator.EstimateTokens(History);

        await Store.SaveTurnAsync(turn).ConfigureAwait(false);
        SyncModelHistoryToSession();
        await Store.UpdateSessionAsync(SessionInfo).ConfigureAwait(false);

        // Fire-and-forget centralized metering
        if (UsageMeter is not null)
        {
            _ = UsageMeter.RecordTurnAsync(new TurnUsageRecord
            {
                SessionId = SessionInfo.Id,
                ProviderId = CurrentModel?.ProviderName ?? "unknown",
                ModelId = CurrentModel?.Id ?? "unknown",
                PromptTokens = tokensIn,
                CompletionTokens = tokensOut,
                ToolCalls = 0,
                DurationMs = durationMs,
                ProjectPath = SessionInfo.ProjectPath,
            });
        }
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
        if (CurrentTurn is not null)
        {
            CurrentTurn.ToolCalls.Add(new ToolCallRecord
            {
                TurnId = CurrentTurn.Id,
                ToolName = toolName,
                Arguments = arguments,
                Result = result,
                Status = status,
                DurationMs = durationMs,
            });
            return;
        }

        _pendingToolCalls.Add(new PendingToolCall(
            ToolName: toolName,
            Arguments: arguments,
            Result: result,
            Status: status,
            DurationMs: durationMs,
            CreatedAt: DateTime.UtcNow));
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

        if (AuditService is not null)
        {
            await AuditService.EmitAsync(new AuditEvent
            {
                Action = "session.create",
                SessionId = SessionInfo.Id,
                Resource = projectPath,
                TraceId = Activity.Current?.TraceId.ToString(),
                Severity = AuditSeverity.Info,
            }).ConfigureAwait(false);
        }
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

        if (AuditService is not null)
        {
            await AuditService.EmitAsync(new AuditEvent
            {
                Action = "session.close",
                SessionId = SessionInfo.Id,
                Resource = SessionInfo.ProjectPath,
                TraceId = Activity.Current?.TraceId.ToString(),
                Detail = $"turns={SessionInfo.MessageCount}; tokens={SessionInfo.TotalTokens}",
                Severity = AuditSeverity.Info,
            }).ConfigureAwait(false);
        }

        SessionInfo.IsActive = false;
        await Store.CloseSessionAsync(SessionInfo.Id).ConfigureAwait(false);
        await ExportSessionAsync().ConfigureAwait(false);
    }

    /// <summary>Cached token count for the current system prompt. Recomputed only when prompt text changes.</summary>
    public int SystemPromptTokens
    {
        get
        {
            var current = History.FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;
            if (current == null) return 0;
            if (string.Equals(current, _cachedSystemPromptText, StringComparison.Ordinal))
                return _cachedSystemPromptTokens;
            _cachedSystemPromptText = current;
            _cachedSystemPromptTokens = TokenEstimator.EstimateTokens(current);
            return _cachedSystemPromptTokens;
        }
    }

    /// <summary>
    /// Original system prompt captured at session startup (before compaction or runtime edits).
    /// </summary>
    public string? OriginalSystemPrompt => _originalSystemPromptText;

    /// <summary>
    /// Captures the original system prompt if it has not already been captured.
    /// </summary>
    public void CaptureOriginalSystemPromptIfUnset(string? prompt = null)
    {
        if (!string.IsNullOrWhiteSpace(_originalSystemPromptText))
            return;

        var candidate = prompt;
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = History.FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;

        if (!string.IsNullOrWhiteSpace(candidate))
            _originalSystemPromptText = candidate;
    }

    /// <summary>
    /// Replaces the current system prompt in chat history, inserting one if absent.
    /// </summary>
    public void ReplaceSystemPrompt(string prompt)
    {
        var normalized = prompt ?? string.Empty;
        var idx = -1;
        for (var i = 0; i < History.Count; i++)
        {
            if (History[i].Role == AuthorRole.System)
            {
                idx = i;
                break;
            }
        }

        var message = new ChatMessageContent(AuthorRole.System, normalized);
        if (idx >= 0)
        {
            History.RemoveAt(idx);
            History.Insert(idx, message);
        }
        else
        {
            History.Insert(0, message);
        }

        _cachedSystemPromptText = normalized;
        _cachedSystemPromptTokens = TokenEstimator.EstimateTokens(normalized);
    }

    /// <summary>
    /// Restores the current system prompt to the originally captured startup text.
    /// </summary>
    public bool TryResetSystemPrompt()
    {
        CaptureOriginalSystemPromptIfUnset();
        if (string.IsNullOrWhiteSpace(_originalSystemPromptText))
            return false;

        ReplaceSystemPrompt(_originalSystemPromptText);
        return true;
    }

    /// <summary>
    /// Compacts the system prompt using the LLM to summarize it while preserving key instructions.
    /// Returns the new token count. Skips if already within budget.
    /// </summary>
    public async Task<int> CompactSystemPromptAsync(int targetTokens, CancellationToken ct = default)
    {
        var currentTokens = SystemPromptTokens;
        if (currentTokens <= targetTokens) return currentTokens;

        var systemMsg = History.FirstOrDefault(m => m.Role == AuthorRole.System);
        if (systemMsg == null) return 0;

        var prompt = $"""
            Compress the following system prompt to under {targetTokens} tokens while preserving:
            1. All tool names and their descriptions
            2. All code style rules and conventions
            3. All build/test commands
            4. Project-specific architecture notes
            5. Safety and permission rules

            Remove verbose explanations, examples, and redundant text. Keep bullet points.
            Output ONLY the compressed system prompt, nothing else.

            --- SYSTEM PROMPT ---
            {systemMsg.Content}
            """;

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var compactHistory = new ChatHistory();
        compactHistory.AddUserMessage(prompt);
        var result = await chat.GetChatMessageContentAsync(compactHistory, cancellationToken: ct).ConfigureAwait(false);

        var compacted = result.Content ?? systemMsg.Content ?? "";

        ReplaceSystemPrompt(compacted);
        return SystemPromptTokens;
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
    /// Updates only the metadata fields on the current model (no kernel rebuild or fork point).
    /// </summary>
    public void UpdateModelMetadata(ProviderModelInfo enrichedModel)
    {
        CurrentModel = enrichedModel;
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
        var previousKernel = _kernel;

        // Preserve governance/safety filters (tool confirmations, workflows, audit hooks)
        // so model/provider switches keep the same enforcement pipeline.
        foreach (var filter in previousKernel.AutoFunctionInvocationFilters)
        {
            if (!newKernel.AutoFunctionInvocationFilters.Any(f => ReferenceEquals(f, filter)))
                newKernel.AutoFunctionInvocationFilters.Add(filter);
        }

        // Re-register plugins from the old kernel
        foreach (var plugin in previousKernel.Plugins)
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
    /// Attempts to resolve a model by name/id and switch to it.
    /// Returns true if the switch succeeded, false if the model was not found.
    /// </summary>
    public async Task<bool> TrySwitchModelAsync(string modelNameOrId, CancellationToken ct = default)
    {
        var allModels = await _registry.GetModelsAsync(ct).ConfigureAwait(false);

        // Try exact ID match first, then display name, then contains
        var match = allModels.FirstOrDefault(m =>
                        string.Equals(m.Id, modelNameOrId, StringComparison.OrdinalIgnoreCase))
                    ?? allModels.FirstOrDefault(m =>
                        string.Equals(m.DisplayName, modelNameOrId, StringComparison.OrdinalIgnoreCase))
                    ?? allModels.FirstOrDefault(m =>
                        m.Id.Contains(modelNameOrId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return false;

        SwitchModel(match, "fallback");
        return true;
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
                CumulativeContextTokens = turn.CumulativeContextTokens,
                ContextWindowTokens = turn.ContextWindowTokens,
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
    /// Cycles reasoning effort for quick keyboard toggles:
    /// auto -> low -> medium -> high -> max -> auto.
    /// </summary>
    public ReasoningEffort? CycleReasoningEffort()
    {
        ReasoningEffortOverride = ReasoningEffortOverride switch
        {
            null => ReasoningEffort.Low,
            ReasoningEffort.Low => ReasoningEffort.Medium,
            ReasoningEffort.Medium => ReasoningEffort.High,
            ReasoningEffort.High => ReasoningEffort.Max,
            _ => null,
        };

        return ReasoningEffortOverride;
    }

    /// <summary>
    /// Forces compaction of the chat history using hierarchical summarization.
    /// </summary>
    public async Task CompactAsync(CancellationToken ct = default)
    {
        var modelWindow = CurrentModel?.ContextWindowTokens is > 0
            ? CurrentModel.ContextWindowTokens
            : 4000;

        var tokenCount = TokenEstimator.EstimateTokens(History);
        // Don't compact if already comfortably under a quarter of the window
        if (tokenCount <= modelWindow / 4)
            return;

        var strategy = new HierarchicalSummarizationStrategy();
        var options = new CompactionOptions
        {
            MaxContextWindowTokens = modelWindow,
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

    private sealed record PendingToolCall(
        string ToolName,
        string? Arguments,
        string? Result,
        string Status,
        long DurationMs,
        DateTime CreatedAt);
}
