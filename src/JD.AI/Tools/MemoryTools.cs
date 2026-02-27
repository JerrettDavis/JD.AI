using System.ComponentModel;
using JD.SemanticKernel.Extensions.Memory;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Semantic memory tools — store, search, and forget.
/// </summary>
public sealed class MemoryTools
{
    private readonly ISemanticMemory? _memory;

    public MemoryTools(ISemanticMemory? memory = null)
    {
        _memory = memory;
    }

    [KernelFunction("memory_store")]
    [Description("Store text in semantic memory for future recall.")]
    public async Task<string> MemoryStoreAsync(
        [Description("Text to store")] string text,
        [Description("Optional category (e.g. 'decision', 'fact', 'code')")] string? category = null)
    {
        if (_memory is null)
        {
            return "Memory is not available (no embedding model configured).";
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(category))
        {
            metadata["category"] = category;
        }

        var id = await _memory.StoreAsync(text, metadata).ConfigureAwait(false);
        return $"Stored in memory with ID: {id}";
    }

    [KernelFunction("memory_search")]
    [Description("Search semantic memory for relevant information.")]
    public async Task<string> MemorySearchAsync(
        [Description("Search query")] string query,
        [Description("Maximum results (default 5)")] int maxResults = 5)
    {
        if (_memory is null)
        {
            return "Memory is not available (no embedding model configured).";
        }

        var options = new MemorySearchOptions
        {
            TopK = maxResults,
            MinRelevanceScore = 0.1,
        };

        var results = await _memory.SearchAsync(query, options).ConfigureAwait(false);

        if (results.Count == 0)
        {
            return "No relevant memories found.";
        }

        var lines = results.Select((r, i) =>
            $"{i + 1}. [{r.RelevanceScore:P0}] {r.Record.Text}");

        return string.Join('\n', lines);
    }

    [KernelFunction("memory_forget")]
    [Description("Remove a memory entry by its ID.")]
    public async Task<string> MemoryForgetAsync(
        [Description("Memory ID to remove")] string id)
    {
        if (_memory is null)
        {
            return "Memory is not available (no embedding model configured).";
        }

        await _memory.ForgetAsync(id).ConfigureAwait(false);
        return $"Removed memory {id}.";
    }
}
