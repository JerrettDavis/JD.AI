using JD.AI.Core.Governance.Audit;
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

    /// <summary>Optional audit service for emitting session lifecycle events.</summary>
    public AuditService? AuditService { get; set; }

    public ChatHistory History { get; } = new();
    public ProviderModelInfo? CurrentModel { get; private set; }
    public bool AutoRunEnabled { get; set; }

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
        await Store.UpdateSessionAsync(SessionInfo).ConfigureAwait(false);
    }

    /// <summary>Record a tool call on the current turn.</summary>
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
                Detail = $"turns={SessionInfo.MessageCount}; tokens={SessionInfo.TotalTokens}",
                Severity = AuditSeverity.Info,
            }).ConfigureAwait(false);
        }

        SessionInfo.IsActive = false;
        await Store.CloseSessionAsync(SessionInfo.Id).ConfigureAwait(false);
        await ExportSessionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Switches the backing LLM while preserving chat history and tools.
    /// </summary>
    public void SwitchModel(ProviderModelInfo model)
    {
        var newKernel = _registry.BuildKernel(model);

        // Re-register plugins from the old kernel
        foreach (var plugin in _kernel.Plugins)
        {
            newKernel.Plugins.Add(plugin);
        }

        _kernel = newKernel;
        CurrentModel = model;
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
