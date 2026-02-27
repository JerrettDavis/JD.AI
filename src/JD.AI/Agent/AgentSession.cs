using JD.AI.Tui.Persistence;
using JD.AI.Tui.Providers;
using JD.SemanticKernel.Extensions.Compaction;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tui.Agent;

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

    public ChatHistory History { get; } = new();
    public ProviderModelInfo? CurrentModel { get; private set; }
    public bool AutoRunEnabled { get; set; }

    /// <summary>
    /// When true, ALL tool confirmations are bypassed — no safety prompts at all.
    /// Set via --dangerously-skip-permissions or /permissions off.
    /// </summary>
    public bool SkipPermissions { get; set; }
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
