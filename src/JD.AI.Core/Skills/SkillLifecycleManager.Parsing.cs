using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace JD.AI.Core.Skills;

public sealed partial class SkillLifecycleManager
{
    private static readonly HashSet<string> AllowedFrontmatterKeys = new(KeyComparer)
    {
        "name",
        "description",
        "metadata",
        "homepage",
        "user-invocable",
        "disable-model-invocation",
        "command-dispatch",
        "command-tool",
        "command-arg-mode",
        "allowed-tools",
    };

    private static readonly HashSet<string> AllowedProviderMetadataKeys = new(KeyComparer)
    {
        "always",
        "emoji",
        "homepage",
        "os",
        "requires",
        "primaryEnv",
        "skillKey",
        "install",
    };

    private static readonly HashSet<string> AllowedRequiresKeys = new(KeyComparer)
    {
        "bins",
        "anyBins",
        "env",
        "config",
    };

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    private static bool TryParseMetadata(
        string markdown,
        out SkillMetadata? metadata,
        out string reasonCode,
        out string reasonDetail)
    {
        metadata = null;
        reasonCode = SkillReasonCodes.None;
        reasonDetail = string.Empty;

        if (!TryExtractFrontmatter(markdown, out var frontmatter, out var frontmatterError))
        {
            reasonCode = SkillReasonCodes.InvalidFrontmatter;
            reasonDetail = frontmatterError;
            return false;
        }

        Dictionary<string, object?> root;
        try
        {
            var raw = YamlDeserializer.Deserialize(frontmatter);
            root = NormalizeAsMap(raw);
        }
        catch (Exception ex)
        {
            reasonCode = SkillReasonCodes.InvalidFrontmatter;
            reasonDetail = ex.Message;
            return false;
        }

        var unknownTopLevel = root.Keys.Where(k => !AllowedFrontmatterKeys.Contains(k)).ToArray();
        if (unknownTopLevel.Length > 0)
        {
            reasonCode = SkillReasonCodes.InvalidSchema;
            reasonDetail = $"Unknown frontmatter keys: {string.Join(", ", unknownTopLevel)}";
            return false;
        }

        if (!TryReadRequiredString(root, "name", out var name))
        {
            reasonCode = SkillReasonCodes.InvalidSchema;
            reasonDetail = "Frontmatter requires non-empty 'name'.";
            return false;
        }

        if (!TryReadRequiredString(root, "description", out var description))
        {
            reasonCode = SkillReasonCodes.InvalidSchema;
            reasonDetail = "Frontmatter requires non-empty 'description'.";
            return false;
        }

        if (!TryParseProviderMetadata(root.TryGetValue("metadata", out var rawMetadata) ? rawMetadata : null,
                out var provider,
                out reasonDetail))
        {
            reasonCode = SkillReasonCodes.InvalidSchema;
            return false;
        }

        var skillKey = ReadString(provider, "skillKey") ?? name;

        metadata = new SkillMetadata(
            Name: name,
            Description: description,
            SkillKey: skillKey,
            Always: ReadBool(provider, "always") ?? false,
            PrimaryEnv: ReadString(provider, "primaryEnv"),
            Os: ReadStringList(provider, "os"),
            RequiredBins: ReadStringList(ReadMap(provider, "requires"), "bins"),
            RequiredAnyBins: ReadStringList(ReadMap(provider, "requires"), "anyBins"),
            RequiredEnvironment: ReadStringList(ReadMap(provider, "requires"), "env"),
            RequiredConfigPaths: ReadStringList(ReadMap(provider, "requires"), "config"));

        reasonCode = SkillReasonCodes.None;
        reasonDetail = string.Empty;
        return true;
    }

    private static bool TryParseProviderMetadata(
        object? rawMetadata,
        out Dictionary<string, object?> providerMetadata,
        out string error)
    {
        providerMetadata = new Dictionary<string, object?>(KeyComparer);
        error = string.Empty;

        if (rawMetadata is null)
            return true;

        var metadataMap = AsDictionary(rawMetadata);
        if (metadataMap is null)
        {
            error = "metadata must be an object or JSON object string.";
            return false;
        }

        var provider = ReadMap(metadataMap, "jdai") ?? ReadMap(metadataMap, "openclaw");
        if (provider is null)
            return true;

        var unknown = provider.Keys.Where(k => !AllowedProviderMetadataKeys.Contains(k)).ToArray();
        if (unknown.Length > 0)
        {
            error = $"Unknown metadata.jdai/openclaw keys: {string.Join(", ", unknown)}";
            return false;
        }

        var requires = ReadMap(provider, "requires");
        if (requires is not null)
        {
            var unknownRequires = requires.Keys.Where(k => !AllowedRequiresKeys.Contains(k)).ToArray();
            if (unknownRequires.Length > 0)
            {
                error = $"Unknown requires keys: {string.Join(", ", unknownRequires)}";
                return false;
            }
        }

        providerMetadata = provider;
        return true;
    }

    private static bool TryExtractFrontmatter(string markdown, out string frontmatter, out string error)
    {
        frontmatter = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(markdown))
        {
            error = "SKILL.md is empty.";
            return false;
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        if (lines.Length < 3 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            error = "Missing YAML frontmatter delimiter (---).";
            return false;
        }

        var endIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), "---", StringComparison.Ordinal))
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex < 0)
        {
            error = "Unterminated YAML frontmatter block.";
            return false;
        }

        frontmatter = string.Join('\n', lines.Skip(1).Take(endIndex - 1));
        return true;
    }

    private static Dictionary<string, object?> NormalizeAsMap(object? value)
    {
        return AsDictionary(value) ?? new Dictionary<string, object?>(KeyComparer);
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
            return null;

        if (value is IDictionary dictionary)
        {
            var normalized = new Dictionary<string, object?>(KeyComparer);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                normalized[key] = NormalizeValue(entry.Value);
            }

            return normalized;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
                list.Add(NormalizeValue(item));
            return list;
        }

        return value;
    }

    private static Dictionary<string, object?>? AsDictionary(object? value)
    {
        if (value is null)
            return null;

        if (value is Dictionary<string, object?> typed)
            return typed;

        if (value is IDictionary dictionary)
        {
            var normalized = new Dictionary<string, object?>(KeyComparer);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                normalized[key] = NormalizeValue(entry.Value);
            }

            return normalized;
        }

        if (value is string json && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json) is JsonObject obj)
                    return ConvertJsonObject(obj);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static Dictionary<string, object?> ConvertJsonObject(JsonObject obj)
    {
        var map = new Dictionary<string, object?>(KeyComparer);
        foreach (var (key, value) in obj)
            map[key] = ConvertJsonNode(value);
        return map;
    }

    private static object? ConvertJsonNode(JsonNode? node)
    {
        if (node is null)
            return null;

        var kind = node.GetValueKind();

        if (kind == JsonValueKind.Object)
            return ConvertJsonObject((JsonObject)node);

        if (kind == JsonValueKind.Array)
            return ((JsonArray)node).Select(ConvertJsonNode).ToList();

        if (kind == JsonValueKind.String)
            return node.GetValue<string>();

        if (kind == JsonValueKind.True)
            return true;

        if (kind == JsonValueKind.False)
            return false;

        if (kind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return node.ToJsonString();
    }

    private static bool TryReadRequiredString(Dictionary<string, object?> map, string key, out string value)
    {
        value = ReadString(map, key) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? ReadString(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw is null)
            return null;

        return raw switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
            _ => null,
        };
    }

    private static bool? ReadBool(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw is null)
            return null;

        return raw switch
        {
            bool value => value,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => null,
        };
    }

    private static Dictionary<string, object?>? ReadMap(Dictionary<string, object?> map, string key)
    {
        return map.TryGetValue(key, out var raw) ? AsDictionary(raw) : null;
    }

    private static List<string> ReadStringList(Dictionary<string, object?>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var raw) || raw is null)
            return [];

        return NormalizeStringList(raw);
    }

    private static List<string> NormalizeStringList(object raw)
    {
        if (raw is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? []
                : [text.Trim()];
        }

        if (raw is IEnumerable enumerable)
        {
            var values = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is string str && !string.IsNullOrWhiteSpace(str))
                    values.Add(str.Trim());
                else if (item is not null)
                    values.Add(item.ToString()!.Trim());
            }

            return values;
        }

        return [];
    }
}
