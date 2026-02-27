using JD.AI.Tui.Providers;
using JD.SemanticKernel.Extensions.Compaction;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tui.Agent;

/// <summary>
/// Manages the conversation state, kernel, and compaction.
/// </summary>
public sealed class AgentSession
{
    private readonly IProviderRegistry _registry;
    private Kernel _kernel;

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
