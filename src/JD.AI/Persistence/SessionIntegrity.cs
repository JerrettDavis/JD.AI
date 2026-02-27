using System.Collections.ObjectModel;

namespace JD.AI.Tui.Persistence;

/// <summary>
/// Cross-checks SQLite store ↔ JSON exports and repairs inconsistencies.
/// </summary>
public static class SessionIntegrity
{
    public sealed record IntegrityResult(
        int SessionsChecked,
        int MismatchesFound,
        ReadOnlyCollection<string> Issues);

    /// <summary>
    /// Check all sessions in the store against their JSON exports.
    /// Optionally repairs by re-exporting from SQLite or re-importing from JSON.
    /// </summary>
    public static async Task<IntegrityResult> CheckAsync(
        SessionStore store,
        string? basePath = null,
        bool autoRepairFromSqlite = false)
    {
        var issuesList = new List<string>();
        var sessions = await store.ListSessionsAsync(limit: 1000).ConfigureAwait(false);

        foreach (var stub in sessions)
        {
            var full = await store.GetSessionAsync(stub.Id).ConfigureAwait(false);
            if (full == null)
            {
                issuesList.Add($"Session {stub.Id} listed but not retrievable");
                continue;
            }

            // Check JSON export exists
            var jsonFiles = SessionExporter.ListExportedFiles(full.ProjectHash, basePath).ToList();
            var jsonPath = jsonFiles.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f), full.Id, StringComparison.Ordinal));

            if (jsonPath == null)
            {
                issuesList.Add($"Session {full.Id}: missing JSON export");
                if (autoRepairFromSqlite)
                    await SessionExporter.ExportAsync(full, basePath).ConfigureAwait(false);
                continue;
            }

            // Compare turn counts
            var imported = await SessionExporter.ImportAsync(jsonPath).ConfigureAwait(false);
            if (imported == null)
            {
                issuesList.Add($"Session {full.Id}: JSON file corrupt/unreadable");
                if (autoRepairFromSqlite)
                    await SessionExporter.ExportAsync(full, basePath).ConfigureAwait(false);
                continue;
            }

            if (imported.Turns.Count != full.Turns.Count)
            {
                issuesList.Add($"Session {full.Id}: turn count mismatch (SQLite={full.Turns.Count}, JSON={imported.Turns.Count})");
                if (autoRepairFromSqlite)
                    await SessionExporter.ExportAsync(full, basePath).ConfigureAwait(false);
            }
        }

        return new IntegrityResult(sessions.Count, issuesList.Count, issuesList.AsReadOnly());
    }
}
