using System.Security.Cryptography;
using System.Text;

namespace JD.AI.Core.Governance;

/// <summary>
/// Signs and verifies policy documents using HMAC-SHA256.
/// Signatures are stored as a <c># jdai-signature: {hex}</c> comment line
/// at the end of the YAML file.
/// </summary>
public static class PolicySignature
{
    private const string SignaturePrefix = "# jdai-signature: ";

    /// <summary>
    /// Signs a YAML policy file by appending an HMAC-SHA256 signature line.
    /// </summary>
    public static string Sign(string yaml, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(key);

        var content = StripSignature(yaml);
        var hash = ComputeHmac(content, key);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return content.TrimEnd() + Environment.NewLine + SignaturePrefix + hex + Environment.NewLine;
    }

    /// <summary>
    /// Verifies the HMAC-SHA256 signature embedded in a YAML policy file.
    /// Returns <c>true</c> if the signature is valid, <c>false</c> if missing or invalid.
    /// </summary>
    public static bool Verify(string yaml, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(key);

        var embeddedHex = ExtractSignature(yaml);
        if (embeddedHex is null) return false;

        var content = StripSignature(yaml);
        var expected = ComputeHmac(content, key);
        var expectedHex = Convert.ToHexString(expected).ToLowerInvariant();

        return string.Equals(embeddedHex, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the hex signature from a signed YAML file, or <c>null</c> if unsigned.
    /// </summary>
    public static string? ExtractSignature(string yaml)
    {
        using var reader = new StringReader(yaml);
        string? lastSignatureLine = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(SignaturePrefix, StringComparison.Ordinal))
                lastSignatureLine = line[SignaturePrefix.Length..].Trim();
        }

        return lastSignatureLine;
    }

    /// <summary>
    /// Strips the signature line from a YAML file, returning only the content.
    /// </summary>
    public static string StripSignature(string yaml)
    {
        var sb = new StringBuilder();
        using var reader = new StringReader(yaml);
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith(SignaturePrefix, StringComparison.Ordinal))
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static byte[] ComputeHmac(string content, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes(content);
        return HMACSHA256.HashData(key, data);
    }
}
