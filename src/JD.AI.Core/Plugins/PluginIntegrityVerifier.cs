using System.Security.Cryptography;
using JD.AI.Plugins.SDK;

namespace JD.AI.Core.Plugins;

internal static class PluginIntegrityVerifier
{
    public static void VerifyEntryAssemblyHash(PluginManifest manifest, string entryAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

        if (string.IsNullOrWhiteSpace(manifest.EntryAssemblySha256))
        {
            return;
        }

        var expected = NormalizeHex(manifest.EntryAssemblySha256);
        var actual = ComputeSha256(entryAssemblyPath);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' entry assembly hash verification failed. " +
                $"Expected SHA-256 '{expected}', but found '{actual}'.");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeHex(string value)
    {
        return value
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
