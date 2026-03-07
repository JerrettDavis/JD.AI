using System.Text.Json;
using System.Text.Json.Nodes;

namespace JD.AI.Core.Skills;

/// <summary>
/// Loads and merges JD.AI skill runtime config from user and workspace files.
/// </summary>
public static class SkillRuntimeConfigLoader
{
    public static SkillRuntimeConfig Load(string? userConfigPath, string? workspaceConfigPath)
    {
        var user = LoadSingle(userConfigPath);
        var workspace = LoadSingle(workspaceConfigPath);

        var entries = new Dictionary<string, SkillEntryConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in user.Entries.Keys.Concat(workspace.Entries.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            user.Entries.TryGetValue(key, out var lower);
            workspace.Entries.TryGetValue(key, out var higher);
            entries[key] = SkillEntryConfig.Merge(lower, higher);
        }

        var allowBundled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (user.HasAllowBundled)
        {
            allowBundled.UnionWith(user.AllowBundled);
        }

        if (workspace.HasAllowBundled)
        {
            allowBundled.Clear();
            allowBundled.UnionWith(workspace.AllowBundled);
        }

        return new SkillRuntimeConfig
        {
            Watch = workspace.Watch ?? user.Watch ?? true,
            WatchDebounceMs = Math.Clamp(workspace.WatchDebounceMs ?? user.WatchDebounceMs ?? 250, 50, 5000),
            AllowBundled = allowBundled,
            Entries = entries,
            RootConfig = MergeNodes(user.RootConfig, workspace.RootConfig),
        };
    }

    private static PartialConfig LoadSingle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return PartialConfig.Empty;

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            if (root is not JsonObject rootObject)
                return PartialConfig.Empty;

            var skillsNode = rootObject["skills"] as JsonObject ?? rootObject;
            var loadNode = skillsNode["load"] as JsonObject;

            var watch = ReadBool(loadNode, "watch");
            var debounce = ReadInt(loadNode, "watchDebounceMs");

            var allowBundled = ReadStringSet(skillsNode["allowBundled"]);
            var hasAllowBundled = skillsNode.ContainsKey("allowBundled");

            var entries = new Dictionary<string, SkillEntryConfig>(StringComparer.OrdinalIgnoreCase);
            if (skillsNode["entries"] is JsonObject entriesNode)
            {
                foreach (var (key, value) in entriesNode)
                {
                    if (value is not JsonObject entryObj)
                        continue;

                    entries[key] = ParseEntry(entryObj);
                }
            }

            return new PartialConfig(
                Watch: watch,
                WatchDebounceMs: debounce,
                HasAllowBundled: hasAllowBundled,
                AllowBundled: allowBundled,
                Entries: entries,
                RootConfig: rootObject.DeepClone());
        }
        catch (JsonException)
        {
            return PartialConfig.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PartialConfig.Empty;
        }
    }

    private static SkillEntryConfig ParseEntry(JsonObject entryObj)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entryObj["env"] is JsonObject envObj)
        {
            foreach (var (name, value) in envObj)
            {
                if (value is null)
                    continue;

                var stringValue = value.GetValueKind() == JsonValueKind.String
                    ? value.GetValue<string>()
                    : value.ToJsonString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                    env[name] = stringValue;
            }
        }

        string? apiKey = null;
        if (entryObj["apiKey"] is JsonNode apiNode)
        {
            if (apiNode.GetValueKind() == JsonValueKind.String)
            {
                apiKey = apiNode.GetValue<string>();
            }
            else if (apiNode is JsonObject apiRef && apiRef["value"] is JsonNode valueNode && valueNode.GetValueKind() == JsonValueKind.String)
            {
                apiKey = valueNode.GetValue<string>();
            }
        }

        return new SkillEntryConfig
        {
            Enabled = ReadBool(entryObj, "enabled"),
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            Env = env,
            Config = entryObj["config"]?.DeepClone(),
        };
    }

    private static HashSet<string> ReadStringSet(JsonNode? node)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is null || item.GetValueKind() != JsonValueKind.String)
                    continue;

                var text = item.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(text))
                    set.Add(text);
            }
        }

        return set;
    }

    private static bool? ReadBool(JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null)
            return null;

        return node.GetValueKind() == JsonValueKind.True || node.GetValueKind() == JsonValueKind.False
            ? node.GetValue<bool>()
            : null;
    }

    private static int? ReadInt(JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null)
            return null;

        return node.GetValueKind() == JsonValueKind.Number && node is JsonValue valueNode && valueNode.TryGetValue<int>(out var value)
            ? value
            : null;
    }

    private static JsonNode? MergeNodes(JsonNode? lower, JsonNode? higher)
    {
        if (lower is null)
            return higher?.DeepClone();

        if (higher is null)
            return lower.DeepClone();

        if (lower is JsonObject lowerObj && higher is JsonObject higherObj)
        {
            var merged = new JsonObject();
            foreach (var (key, value) in lowerObj)
                merged[key] = value?.DeepClone();
            foreach (var (key, value) in higherObj)
            {
                merged[key] = merged.TryGetPropertyValue(key, out var existing)
                    ? MergeNodes(existing, value)
                    : value?.DeepClone();
            }

            return merged;
        }

        return higher.DeepClone();
    }

    private sealed record PartialConfig(
        bool? Watch,
        int? WatchDebounceMs,
        bool HasAllowBundled,
        ISet<string> AllowBundled,
        IReadOnlyDictionary<string, SkillEntryConfig> Entries,
        JsonNode? RootConfig)
    {
        public static PartialConfig Empty { get; } = new(
            Watch: null,
            WatchDebounceMs: null,
            HasAllowBundled: false,
            AllowBundled: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Entries: new Dictionary<string, SkillEntryConfig>(StringComparer.OrdinalIgnoreCase),
            RootConfig: null);
    }
}
