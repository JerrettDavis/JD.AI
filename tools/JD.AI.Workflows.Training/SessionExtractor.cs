using System.Text.RegularExpressions;

namespace JD.AI.Workflows.Training;

/// <summary>
/// Extracts labeled prompts from OpenClaw session transcripts.
/// Labeling heuristic: if the assistant's response to a user prompt involves
/// file system or process operations (exec, write, read), it is a workflow;
/// otherwise it is a conversation.
/// </summary>
public static class SessionExtractor
{
    // Events in a transcript line
    private interface TranscriptEvent { string? ParentId { get; } }
    private sealed record TranscriptToolResult(string Id, string? ParentId, string Tool, string Content) : TranscriptEvent;

    // Tool names that indicate workflow intent
    private static readonly HashSet<string> WorkflowTools =
    [
        "exec", "write", "edit", "read", "delete", "move", "copy",
        "mkdir", "touch", "chmod", "chown", "run", "command",
    ];

    /// <summary>
    /// Extracts labeled prompts from a transcript JSONL file.
    /// </summary>
    /// <param name="transcriptPath">Path to the .jsonl transcript file.</param>
    /// <param name="minAssistantMessages">Minimum assistant responses needed to consider a user prompt valid.</param>
    /// <returns>Labeled prompts extracted from the transcript.</returns>
    public static List<TrainingDataGenerator.LabeledPrompt> ExtractFromTranscript(
        string transcriptPath,
        int minAssistantMessages = 1)
    {
        var events = new Dictionary<string, TranscriptEvent>();
        var prompts = new List<TrainingDataGenerator.LabeledPrompt>();

        foreach (var line in File.ReadAllLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl)) continue;
            var type = typeEl.GetString();
            var id = root.GetProperty("id").GetString() ?? "";
            var parentId = root.TryGetProperty("parentId", out var pEl) && pEl.ValueKind != System.Text.Json.JsonValueKind.Null
                ? pEl.GetString() : null;

            switch (type)
            {
                case "message" when root.TryGetProperty("message", out var msg):
                    var role = msg.GetProperty("role").GetString() ?? "";
                    if (role != "user") break;

                    var text = ExtractDiscordBody(msg);
                    if (string.IsNullOrWhiteSpace(text)) break;

                    var isWorkflow = IsWorkflowFromHistory(events, id, minAssistantMessages);
                    prompts.Add(new TrainingDataGenerator.LabeledPrompt(text, isWorkflow));
                    break;

                case "tool_result" when root.TryGetProperty("tool", out var toolEl)
                    && root.TryGetProperty("result", out var resultEl):
                    var toolName = toolEl.GetString() ?? "";
                    var content = resultEl.TryGetProperty("content", out var cEl)
                        ? cEl.GetString() ?? "" : resultEl.GetString() ?? "";
                    events[id] = new TranscriptToolResult(id, parentId, toolName, content);
                    break;
            }
        }

        return prompts;
    }

    /// <summary>
    /// Extracts the actual Discord message body from the envelope-wrapped content.
    /// </summary>
    private static string ExtractDiscordBody(System.Text.Json.JsonElement msg)
    {
        if (!msg.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != System.Text.Json.JsonValueKind.Array)
            return "";

        foreach (var block in contentEl.EnumerateArray())
        {
            if (!block.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "text"
                || !block.TryGetProperty("text", out var textEl))
                continue;

            var text = textEl.GetString() ?? "";

            // Extract content between the opening and closing EXTERNAL_UNTRUSTED_CONTENT markers
            var openMarker = "<<<EXTERNAL_UNTRUSTED_CONTENT";
            var closeMarker = "<<<END_EXTERNAL_UNTRUSTED_CONTENT";
            var openIdx = text.IndexOf(openMarker, StringComparison.Ordinal);
            if (openIdx < 0) continue;
            var closeIdx = text.IndexOf(closeMarker, openIdx, StringComparison.Ordinal);
            if (closeIdx < 0) continue;

            text = text.Substring(openIdx + openMarker.Length, closeIdx - openIdx - openMarker.Length);

            // Strip the "Source: External\n---\nUNTRUSTED Discord message body\n" header
            text = Regex.Replace(text, @"^\s*Source: External\s*[-]+\s*UNTRUSTED Discord message body\s*",
                "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // Remove leading @mention (e.g. "@Minimax ")
            text = Regex.Replace(text, @"^@\w+\s+", "", RegexOptions.Multiline);

            text = text.Trim();

            if (text.Length >= 3)
                return text;
        }

        return "";
    }

    /// <summary>
    /// Determines if a user prompt was a workflow by checking if the assistant's
    /// response involved tool calls.
    /// </summary>
    private static bool IsWorkflowFromHistory(Dictionary<string, TranscriptEvent> events, string userMsgId, int minToolCalls)
    {
        var toolCallCount = 0;
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        // BFS through the conversation subtree rooted at userMsgId
        queue.Enqueue(userMsgId);
        while (queue.TryDequeue(out var currentId))
        {
            if (!visited.Add(currentId)) continue;

            // Find all direct children of currentId:
            // children are events whose ParentId == currentId
            foreach (var (childId, evt) in events)
            {
                if (evt.ParentId == currentId)
                    queue.Enqueue(childId);
            }
        }

        // Count workflow tool calls among all visited events
        foreach (var evtId in visited)
        {
            if (events.TryGetValue(evtId, out var evt) && evt is TranscriptToolResult tr)
            {
                if (IsWorkflowTool(tr.Tool))
                    toolCallCount++;
            }
        }

        return toolCallCount >= minToolCalls;
    }

    private static bool IsWorkflowTool(string tool) =>
        WorkflowTools.Any(t => tool.Contains(t, StringComparison.OrdinalIgnoreCase));
}
