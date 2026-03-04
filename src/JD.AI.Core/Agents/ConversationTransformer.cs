using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Agents;

/// <summary>
/// Defines how conversation history is handled during a model switch.
/// </summary>
public enum SwitchMode
{
    /// <summary>Keep full history, just swap model.</summary>
    Preserve,

    /// <summary>Summarize with current model, feed to new.</summary>
    Compact,

    /// <summary>Smart briefing optimized for handoff.</summary>
    Transform,

    /// <summary>Clear history, start clean.</summary>
    Fresh,

    /// <summary>No changes — abort the switch.</summary>
    Cancel,
}

/// <summary>
/// Transforms conversation history when switching between AI models.
/// </summary>
public sealed class ConversationTransformer
{
    private const string TransformPrompt =
        """
        You are reviewing a conversation to prepare a handoff to a different AI model.
        Extract and organize:
        - Key decisions made and their rationale
        - Current task state and progress
        - File paths and code locations mentioned
        - Code changes in progress or completed
        - Pending questions or blockers
        - User preferences and communication style observed
        Format as a concise briefing document that gives the receiving model full context to continue seamlessly.
        """;

    /// <summary>
    /// Transforms <paramref name="currentHistory"/> according to the requested <paramref name="mode"/>.
    /// </summary>
    public async Task<(ChatHistory history, string? briefing)> TransformAsync(
        ChatHistory currentHistory,
        Kernel? currentKernel,
        ProviderModelInfo targetModel,
        SwitchMode mode,
        CancellationToken ct = default)
    {
        return mode switch
        {
            SwitchMode.Preserve => (currentHistory, null),
            SwitchMode.Compact => await CompactAsync(currentHistory, currentKernel, ct).ConfigureAwait(false),
            SwitchMode.Transform => await TransformHandoffAsync(currentHistory, currentKernel, ct).ConfigureAwait(false),
            SwitchMode.Fresh => (new ChatHistory(), null),
            SwitchMode.Cancel => throw new OperationCanceledException("Model switch cancelled."),
            _ => (currentHistory, null),
        };
    }

    private static async Task<(ChatHistory history, string? briefing)> CompactAsync(
        ChatHistory currentHistory,
        Kernel? currentKernel,
        CancellationToken ct)
    {
        var chatService = currentKernel?.GetAllServices<IChatCompletionService>().FirstOrDefault();
        if (chatService is null)
        {
            // Fall back to Preserve when no chat completion service is available.
            return (currentHistory, null);
        }

        var summaryChat = new ChatHistory();
        summaryChat.AddSystemMessage("Summarize the following conversation concisely, preserving key context and decisions.");

        foreach (var msg in currentHistory)
        {
            summaryChat.Add(msg);
        }

        var response = await chatService.GetChatMessageContentsAsync(
            summaryChat, cancellationToken: ct).ConfigureAwait(false);

        var summary = response.Count > 0 ? response[0].Content ?? string.Empty : string.Empty;

        var compacted = new ChatHistory();
        compacted.AddSystemMessage(summary);
        return (compacted, null);
    }

    private static async Task<(ChatHistory history, string? briefing)> TransformHandoffAsync(
        ChatHistory currentHistory,
        Kernel? currentKernel,
        CancellationToken ct)
    {
        var chatService = currentKernel?.GetAllServices<IChatCompletionService>().FirstOrDefault();
        if (chatService is null)
        {
            // Fall back to Preserve when no chat completion service is available.
            return (currentHistory, null);
        }

        var briefingChat = new ChatHistory();
        briefingChat.AddSystemMessage(TransformPrompt);

        foreach (var msg in currentHistory)
        {
            briefingChat.Add(msg);
        }

        var response = await chatService.GetChatMessageContentsAsync(
            briefingChat, cancellationToken: ct).ConfigureAwait(false);

        var briefing = response.Count > 0 ? response[0].Content ?? string.Empty : string.Empty;

        var transformed = new ChatHistory();
        transformed.AddSystemMessage(briefing);
        return (transformed, briefing);
    }
}
