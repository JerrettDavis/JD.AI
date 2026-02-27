using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Tui.Persistence;

/// <summary>
/// Exports and imports sessions as human-readable JSON files.
/// </summary>
public static class SessionExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Export a session to a JSON file under ~/.jdai/projects/{hash}/sessions/{id}.json.
    /// </summary>
    public static async Task ExportAsync(SessionInfo session, string? basePath = null)
    {
        basePath ??= GetDefaultBasePath();
        var dir = Path.Combine(basePath, "projects", session.ProjectHash, "sessions");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{session.Id}.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Import a session from a JSON file.
    /// </summary>
    public static async Task<SessionInfo?> ImportAsync(string path)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SessionInfo>(json, JsonOptions);
    }

    /// <summary>
    /// List exported JSON sessions for a project.
    /// </summary>
    public static IEnumerable<string> ListExportedFiles(string projectHash, string? basePath = null)
    {
        basePath ??= GetDefaultBasePath();
        var dir = Path.Combine(basePath, "projects", projectHash, "sessions");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.json");
    }

    private static string GetDefaultBasePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai");
}
