using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Encoding, decoding, hashing, and cryptographic utility tools
/// for common developer operations.
/// </summary>
[ToolPlugin("encoding")]
public sealed class EncodingCryptoTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    // ── Base64 ──────────────────────────────────────────────

    [KernelFunction("encode_base64")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Encode text to Base64. Useful for encoding credentials, binary data, or payloads.")]
    public static string EncodeBase64(
        [Description("The text to encode")] string text,
        [Description("Use URL-safe Base64 (default: false)")] bool urlSafe = false)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var encoded = Convert.ToBase64String(bytes);

        if (urlSafe)
        {
            encoded = encoded
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        return $"**Base64 encoded**{(urlSafe ? " (URL-safe)" : "")}:\n```\n{encoded}\n```";
    }

    [KernelFunction("decode_base64")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Decode a Base64-encoded string back to plain text.")]
    public static string DecodeBase64(
        [Description("The Base64 string to decode")] string encoded)
    {
        try
        {
            // Handle URL-safe Base64
            var normalized = encoded
                .Replace('-', '+')
                .Replace('_', '/');

            // Pad if needed
            var mod = normalized.Length % 4;
            if (mod == 2) normalized += "==";
            else if (mod == 3) normalized += "=";

            var bytes = Convert.FromBase64String(normalized);
            var decoded = Encoding.UTF8.GetString(bytes);
            return $"**Decoded text**:\n```\n{decoded}\n```";
        }
        catch (FormatException)
        {
            return "❌ Invalid Base64 input. Ensure the string is properly Base64-encoded.";
        }
    }

    // ── URL Encoding ────────────────────────────────────────

    [KernelFunction("encode_url")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("URL-encode a string for safe use in URLs and query parameters.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Returns markdown-formatted text, not a URI")]
    public static string EncodeUrl(
        [Description("The text to URL-encode")] string text)
    {
        var encoded = Uri.EscapeDataString(text);
        return $"**URL encoded**:\n```\n{encoded}\n```";
    }

    [KernelFunction("decode_url")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Decode a URL-encoded string back to plain text.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Returns markdown-formatted text, not a URI")]
    public static string DecodeUrl(
        [Description("The URL-encoded string to decode")] string encoded)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(encoded);
            return $"**URL decoded**:\n```\n{decoded}\n```";
        }
        catch (Exception ex)
        {
            return $"❌ Failed to decode URL string: {ex.Message}";
        }
    }

    // ── JWT Decode ──────────────────────────────────────────

    [KernelFunction("decode_jwt")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Decode a JWT token to inspect its header and payload. WARNING: This does NOT verify the signature — use only for inspection/debugging.")]
    public static string DecodeJwt(
        [Description("The JWT token string")] string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
            return "❌ Invalid JWT format. Expected at least 2 dot-separated parts (header.payload).";

        var sb = new StringBuilder();
        sb.AppendLine("## JWT Decode");
        sb.AppendLine();
        sb.AppendLine("⚠️ **Signature NOT verified** — this is for inspection only.");
        sb.AppendLine();

        // Header
        try
        {
            var headerJson = DecodeBase64Segment(parts[0]);
            var headerDoc = JsonDocument.Parse(headerJson);
            sb.AppendLine("### Header");
            sb.AppendLine("```json");
            sb.AppendLine(JsonSerializer.Serialize(headerDoc.RootElement, s_jsonOptions));
            sb.AppendLine("```");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"### Header\n❌ Failed to decode: {ex.Message}");
            sb.AppendLine();
        }

        // Payload
        try
        {
            var payloadJson = DecodeBase64Segment(parts[1]);
            var payloadDoc = JsonDocument.Parse(payloadJson);
            sb.AppendLine("### Payload");
            sb.AppendLine("```json");
            sb.AppendLine(JsonSerializer.Serialize(payloadDoc.RootElement, s_jsonOptions));
            sb.AppendLine("```");
            sb.AppendLine();

            // Extract common claims
            var root = payloadDoc.RootElement;
            sb.AppendLine("### Claims Summary");

            if (root.TryGetProperty("sub", out var sub))
                sb.AppendLine($"- **Subject**: `{sub.GetString()}`");
            if (root.TryGetProperty("iss", out var iss))
                sb.AppendLine($"- **Issuer**: `{iss.GetString()}`");
            if (root.TryGetProperty("aud", out var aud))
                sb.AppendLine($"- **Audience**: `{FormatJsonValue(aud)}`");
            if (root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expUnix))
            {
                var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var isExpired = expTime < DateTimeOffset.UtcNow;
                sb.AppendLine($"- **Expires**: {expTime:yyyy-MM-dd HH:mm:ss UTC} {(isExpired ? "🔴 **EXPIRED**" : "🟢 Valid")}");
            }
            if (root.TryGetProperty("iat", out var iat) && iat.TryGetInt64(out var iatUnix))
            {
                var iatTime = DateTimeOffset.FromUnixTimeSeconds(iatUnix);
                sb.AppendLine($"- **Issued at**: {iatTime:yyyy-MM-dd HH:mm:ss UTC}");
            }
            if (root.TryGetProperty("nbf", out var nbf) && nbf.TryGetInt64(out var nbfUnix))
            {
                var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbfUnix);
                sb.AppendLine($"- **Not before**: {nbfTime:yyyy-MM-dd HH:mm:ss UTC}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"### Payload\n❌ Failed to decode: {ex.Message}");
        }

        // Signature
        sb.AppendLine();
        if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
            sb.AppendLine($"### Signature\n`{parts[2][..Math.Min(parts[2].Length, 20)]}...` (not verified)");
        else
            sb.AppendLine("### Signature\nNone (unsigned token)");

        return sb.ToString();
    }

    // ── Hashing ─────────────────────────────────────────────

    [KernelFunction("hash_compute")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Compute a cryptographic hash of the input text. Supports SHA256 (default), SHA512, SHA384, SHA1, and MD5.")]
    public static string ComputeHash(
        [Description("The text to hash")] string text,
        [Description("Hash algorithm: sha256 (default), sha512, sha384, sha1, md5")] string algorithm = "sha256")
    {
        byte[] hashBytes;
        var textBytes = Encoding.UTF8.GetBytes(text);
        var algoUpper = algorithm.ToUpperInvariant();

        switch (algoUpper)
        {
            case "SHA256":
                hashBytes = SHA256.HashData(textBytes);
                break;
            case "SHA512":
                hashBytes = SHA512.HashData(textBytes);
                break;
            case "SHA384":
                hashBytes = SHA384.HashData(textBytes);
                break;
            case "SHA1":
#pragma warning disable CA5350 // User explicitly requested SHA1
                hashBytes = SHA1.HashData(textBytes);
#pragma warning restore CA5350
                break;
            case "MD5":
#pragma warning disable CA5351 // User explicitly requested MD5
                hashBytes = MD5.HashData(textBytes);
#pragma warning restore CA5351
                break;
            default:
                return $"❌ Unknown algorithm `{algorithm}`. Supported: sha256, sha512, sha384, sha1, md5";
        }

        var hex = Convert.ToHexStringLower(hashBytes);

        var sb = new StringBuilder();
        sb.AppendLine($"**{algoUpper} Hash**:");
        sb.AppendLine($"```");
        sb.AppendLine(hex);
        sb.AppendLine($"```");

        if (string.Equals(algoUpper, "MD5", StringComparison.Ordinal))
            sb.AppendLine("⚠️ **MD5 is cryptographically broken** — do not use for security purposes.");
        else if (string.Equals(algoUpper, "SHA1", StringComparison.Ordinal))
            sb.AppendLine("⚠️ **SHA1 is deprecated** for security use — prefer SHA256 or SHA512.");

        return sb.ToString();
    }

    // ── GUID Generator ──────────────────────────────────────

    [KernelFunction("generate_guid")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate one or more GUIDs/UUIDs (v4). Useful for creating unique identifiers, correlation IDs, or test data.")]
    public static string GenerateGuid(
        [Description("Number of GUIDs to generate (default: 1, max: 20)")] int count = 1)
    {
        count = Math.Clamp(count, 1, 20);

        var sb = new StringBuilder();
        sb.AppendLine($"**Generated GUID(s)**:");
        sb.AppendLine("```");

        for (var i = 0; i < count; i++)
            sb.AppendLine(Guid.NewGuid().ToString());

        sb.AppendLine("```");

        if (count > 1)
            sb.AppendLine($"Generated {count.ToString(CultureInfo.InvariantCulture)} unique v4 UUIDs.");

        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string DecodeBase64Segment(string segment)
    {
        var padded = segment
            .Replace('-', '+')
            .Replace('_', '/');

        var mod = padded.Length % 4;
        if (mod == 2) padded += "==";
        else if (mod == 3) padded += "=";

        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string FormatJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(e => e.GetString())),
            JsonValueKind.String => element.GetString() ?? "",
            _ => element.GetRawText(),
        };
    }
}
