using System.Text.Json;
using System.Text.Json.Nodes;

namespace JD.AI.Core.Skills;

public sealed partial class SkillLifecycleManager
{
    private static int GetTier(SkillSourceKind kind)
    {
        return kind switch
        {
            SkillSourceKind.Workspace => 3,
            SkillSourceKind.Managed => 2,
            _ => 1,
        };
    }

    private static string DetectPlatform() =>
        OperatingSystem.IsWindows() ? "win32" :
        OperatingSystem.IsMacOS() ? "darwin" : "linux";

    private bool BinaryExistsOnPath(string binary)
    {
        if (string.IsNullOrWhiteSpace(binary))
            return false;

        if (Path.IsPathRooted(binary))
            return File.Exists(binary);

        var isWindows = _isWindows();
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return false;

        var extensions = isWindows
            ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                ?? [".EXE", ".CMD", ".BAT"])
            : [string.Empty];

        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(directory, isWindows ? binary + ext : binary);
                if (File.Exists(candidate))
                    return true;
            }
        }

        return false;
    }

    private bool IsEnvironmentSatisfied(string name, SkillMetadata metadata, SkillEntryConfig entry)
    {
        if (!string.IsNullOrWhiteSpace(_getEnvironmentVariable(name)))
            return true;

        if (entry.Env.TryGetValue(name, out var configured) && !string.IsNullOrWhiteSpace(configured))
            return true;

        if (!string.IsNullOrWhiteSpace(metadata.PrimaryEnv) &&
            string.Equals(metadata.PrimaryEnv, name, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(entry.ApiKey))
        {
            return true;
        }

        return false;
    }

    private static bool IsConfigSatisfied(string path, JsonNode? rootConfig, JsonNode? entryConfig)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return IsTruthy(FindNode(entryConfig, tokens)) || IsTruthy(FindNode(rootConfig, tokens));
    }

    private static JsonNode? FindNode(JsonNode? node, IReadOnlyList<string> tokens)
    {
        var current = node;
        foreach (var token in tokens)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(token, out current))
                return null;
        }

        return current;
    }

    private static bool IsTruthy(JsonNode? node)
    {
        if (node is null)
            return false;

        return node.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(node.GetValue<string>()) &&
                                    !string.Equals(node.GetValue<string>(), "false", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(node.GetValue<string>(), "0", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => !string.Equals(node.ToJsonString(), "0", StringComparison.Ordinal),
            JsonValueKind.Array => ((JsonArray)node).Count > 0,
            JsonValueKind.Object => ((JsonObject)node).Count > 0,
            _ => false,
        };
    }

    private sealed record AppliedEnvironmentValue(string Name, string? OriginalValue);

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly IReadOnlyList<AppliedEnvironmentValue> _applied;
        private int _disposed;

        public EnvironmentScope(IReadOnlyList<AppliedEnvironmentValue> applied)
        {
            _applied = applied;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            for (var i = _applied.Count - 1; i >= 0; i--)
            {
                var value = _applied[i];
                Environment.SetEnvironmentVariable(value.Name, value.OriginalValue);
            }
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();
        public void Dispose() { }
    }
}
