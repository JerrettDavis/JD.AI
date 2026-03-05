using System.Text.Json;
using JD.AI.Plugins.SDK;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Reads and validates <c>plugin.json</c> manifests.
/// </summary>
public static class PluginManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<PluginManifest> ReadAsync(string manifestPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("plugin.json was not found.", manifestPath);
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer
            .DeserializeAsync<PluginManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);

        if (manifest is null)
        {
            throw new InvalidDataException("plugin.json could not be parsed.");
        }

        Validate(manifest, manifestPath);
        return manifest;
    }

    internal static void Validate(PluginManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException($"Manifest '{manifestPath}' is missing required field 'id'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException($"Manifest '{manifestPath}' is missing required field 'name'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidDataException($"Manifest '{manifestPath}' is missing required field 'version'.");
        }

        if (manifest.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException(
                $"Manifest '{manifestPath}' has invalid id '{manifest.Id}'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.EntryAssemblySha256))
        {
            var normalizedHash = manifest.EntryAssemblySha256
                .Trim()
                .Replace("-", string.Empty, StringComparison.Ordinal);
            if (normalizedHash.Length != 64 || !normalizedHash.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException(
                    $"Manifest '{manifestPath}' has invalid field 'entryAssemblySha256'. " +
                    "Expected 64 hex characters.");
            }
        }

        if (manifest.Permissions.Any(static p => string.IsNullOrWhiteSpace(p)))
        {
            throw new InvalidDataException(
                $"Manifest '{manifestPath}' contains empty values in 'permissions'.");
        }
    }
}
