using System.Collections.ObjectModel;

namespace JD.AI.Tui.Persistence;

/// <summary>
/// Top-level session metadata. Stored in SQLite and as the root of JSON exports.
/// </summary>
public sealed class SessionInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string? Name { get; set; }
    public string ProjectPath { get; init; } = string.Empty;
    public string ProjectHash { get; init; } = string.Empty;
    public string? ModelId { get; init; }
    public string? ProviderName { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long TotalTokens { get; set; }
    public int MessageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public Collection<TurnRecord> Turns { get; init; } = [];
}

/// <summary>
/// A single conversation turn (user message or assistant response).
/// </summary>
public sealed class TurnRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string SessionId { get; init; } = string.Empty;
    public int TurnIndex { get; init; }
    public string Role { get; init; } = string.Empty; // user | assistant | system
    public string? Content { get; set; }
    public string? ThinkingText { get; set; }
    public string? ModelId { get; set; }
    public string? ProviderName { get; set; }
    public long TokensIn { get; set; }
    public long TokensOut { get; set; }
    public long DurationMs { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public Collection<ToolCallRecord> ToolCalls { get; init; } = [];
    public Collection<FileTouchRecord> FilesTouched { get; init; } = [];
}

/// <summary>
/// A single tool invocation within a turn.
/// </summary>
public sealed class ToolCallRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string TurnId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string? Arguments { get; set; }
    public string? Result { get; set; }
    public string Status { get; set; } = "ok"; // ok | error | denied
    public long DurationMs { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A file operation recorded during a turn.
/// </summary>
public sealed class FileTouchRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string TurnId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // read | write | edit | delete | exec
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Helpers for project path hashing.</summary>
public static class ProjectHasher
{
    public static string Hash(string projectPath)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(projectPath));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
