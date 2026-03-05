using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JD.AI.Core.Governance;

/// <summary>
/// Tracks policy version history as an append-only JSON log file.
/// Each entry records the policy content hash, timestamp, and metadata
/// to support auditing and rollback.
/// </summary>
public sealed class PolicyVersionHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly string _historyPath;
    private readonly object _lock = new();

    public PolicyVersionHistory(string historyFilePath)
    {
        _historyPath = historyFilePath ?? throw new ArgumentNullException(nameof(historyFilePath));
        var dir = Path.GetDirectoryName(_historyPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Records a policy version snapshot.
    /// </summary>
    public void Record(string policyName, string yamlContent, string? author = null)
    {
        var entry = new PolicyVersionEntry
        {
            PolicyName = policyName,
            ContentHash = ComputeHash(yamlContent),
            Timestamp = DateTimeOffset.UtcNow,
            Author = author ?? Environment.UserName,
            ContentLength = yamlContent.Length,
        };

        var line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (_lock)
        {
            File.AppendAllText(_historyPath, line + Environment.NewLine);
        }
    }

    /// <summary>
    /// Returns all version history entries, newest first.
    /// </summary>
    public IReadOnlyList<PolicyVersionEntry> GetHistory(string? policyName = null, int limit = 50)
    {
        if (!File.Exists(_historyPath))
            return [];

        var entries = new List<PolicyVersionEntry>();
        lock (_lock)
        {
            foreach (var line in File.ReadAllLines(_historyPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<PolicyVersionEntry>(line, JsonOptions);
                    if (entry is not null)
                    {
                        if (policyName is null ||
                            string.Equals(entry.PolicyName, policyName, StringComparison.OrdinalIgnoreCase))
                        {
                            entries.Add(entry);
                        }
                    }
                }
                catch
                {
                    // Skip malformed entries
                }
            }
        }

        return entries
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Checks if a policy has changed since the last recorded version.
    /// </summary>
    public bool HasChanged(string policyName, string currentContent)
    {
        var history = GetHistory(policyName, limit: 1);
        if (history.Count == 0) return true;

        var currentHash = ComputeHash(currentContent);
        return !string.Equals(history[0].ContentHash, currentHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// A single entry in the policy version history log.
/// </summary>
public sealed class PolicyVersionEntry
{
    public string PolicyName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Author { get; set; }
    public int ContentLength { get; set; }
}
