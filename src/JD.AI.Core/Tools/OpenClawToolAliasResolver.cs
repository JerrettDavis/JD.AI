namespace JD.AI.Core.Tools;

/// <summary>
/// Resolves OpenClaw-compatible tool aliases to JD.AI canonical tool names.
/// </summary>
public static class OpenClawToolAliasResolver
{
    private static readonly Dictionary<string, string> AliasToCanonical =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bash"] = "run_command",
            ["shell"] = "run_command",
            ["read"] = "read_file",
            ["write"] = "write_file",
            ["edit"] = "edit_file",
            ["ls"] = "list_directory",
            ["webfetch"] = "web_fetch",
            ["websearch"] = "web_search",
            ["todo_read"] = "list_tasks",
            ["todo_write"] = "update_task",
            ["exec"] = "run_command",
            ["process"] = "process",
        };

    public static string Resolve(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return toolName;

        return AliasToCanonical.TryGetValue(toolName, out var canonical)
            ? canonical
            : toolName;
    }
}
