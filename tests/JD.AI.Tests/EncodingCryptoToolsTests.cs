using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class EncodingCryptoToolsTests
{
    // ── Base64 Encode ───────────────────────────────────────

    [Fact]
    public void EncodeBase64_SimpleText_ReturnsCorrectEncoding()
    {
        var result = EncodingCryptoTools.EncodeBase64("Hello, World!");

        Assert.Contains("SGVsbG8sIFdvcmxkIQ==", result);
        Assert.Contains("Base64 encoded", result);
    }

    [Fact]
    public void EncodeBase64_EmptyString_ReturnsEmptyEncoding()
    {
        var result = EncodingCryptoTools.EncodeBase64("");

        // Base64 of empty string is empty
        Assert.Contains("Base64 encoded", result);
    }

    [Fact]
    public void EncodeBase64_UrlSafe_RemovesPaddingAndSwapsChars()
    {
        // "subjects?_d" produces Base64 with + and / characters
        var result = EncodingCryptoTools.EncodeBase64("subjects?_d", urlSafe: true);

        Assert.Contains("URL-safe", result);
        Assert.DoesNotContain("+", result.Split('\n')[1]); // Check encoded line
        Assert.DoesNotContain("=", result.Split('\n')[1]); // No padding
    }

    // ── Base64 Decode ───────────────────────────────────────

    [Fact]
    public void DecodeBase64_ValidInput_ReturnsOriginalText()
    {
        var result = EncodingCryptoTools.DecodeBase64("SGVsbG8sIFdvcmxkIQ==");

        Assert.Contains("Hello, World!", result);
        Assert.Contains("Decoded text", result);
    }

    [Fact]
    public void DecodeBase64_UrlSafeInput_HandlesCorrectly()
    {
        // URL-safe variant without padding
        var standard = Convert.ToBase64String("test+value/here"u8.ToArray());
        var urlSafe = standard.Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var result = EncodingCryptoTools.DecodeBase64(urlSafe);

        Assert.Contains("test+value/here", result);
    }

    [Fact]
    public void DecodeBase64_InvalidInput_ReturnsError()
    {
        var result = EncodingCryptoTools.DecodeBase64("not-valid-base64!!!");

        Assert.Contains("❌", result);
        Assert.Contains("Invalid Base64", result);
    }

    [Fact]
    public void Base64_RoundTrip_PreservesText()
    {
        const string original = "The quick brown fox jumps over the lazy dog 🦊";
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(original));
        var result = EncodingCryptoTools.DecodeBase64(encoded);

        Assert.Contains(original, result);
    }

    // ── URL Encode ──────────────────────────────────────────

    [Fact]
    public void EncodeUrl_SpecialCharacters_EncodesCorrectly()
    {
        var result = EncodingCryptoTools.EncodeUrl("hello world&foo=bar");

        Assert.Contains("hello%20world%26foo%3Dbar", result);
        Assert.Contains("URL encoded", result);
    }

    [Fact]
    public void EncodeUrl_AlreadySafe_PassesThrough()
    {
        var result = EncodingCryptoTools.EncodeUrl("simple");

        Assert.Contains("simple", result);
    }

    [Fact]
    public void EncodeUrl_Unicode_EncodesCorrectly()
    {
        var result = EncodingCryptoTools.EncodeUrl("café");

        Assert.Contains("caf%C3%A9", result);
    }

    // ── URL Decode ──────────────────────────────────────────

    [Fact]
    public void DecodeUrl_EncodedString_DecodesCorrectly()
    {
        var result = EncodingCryptoTools.DecodeUrl("hello%20world%26foo%3Dbar");

        Assert.Contains("hello world&foo=bar", result);
        Assert.Contains("URL decoded", result);
    }

    [Fact]
    public void DecodeUrl_PlainString_PassesThrough()
    {
        var result = EncodingCryptoTools.DecodeUrl("simple");

        Assert.Contains("simple", result);
    }

    [Fact]
    public void Url_RoundTrip_PreservesText()
    {
        const string original = "q=hello world&lang=c#&version=10.0";
        var encoded = Uri.EscapeDataString(original);
        var result = EncodingCryptoTools.DecodeUrl(encoded);

        Assert.Contains(original, result);
    }

    // ── JWT Decode ──────────────────────────────────────────

    [Fact]
    public void DecodeJwt_ValidToken_ShowsHeaderAndPayload()
    {
        // Build a real JWT: header.payload.signature
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode("{\"sub\":\"1234567890\",\"name\":\"John Doe\",\"iat\":1516239022}");
        var token = $"{header}.{payload}.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        var result = EncodingCryptoTools.DecodeJwt(token);

        Assert.Contains("## JWT Decode", result);
        Assert.Contains("HS256", result);
        Assert.Contains("1234567890", result);
        Assert.Contains("John Doe", result);
        Assert.Contains("Signature NOT verified", result);
    }

    [Fact]
    public void DecodeJwt_WithExpClaim_ShowsExpiry()
    {
        // Token with exp in the past
        var header = Base64UrlEncode("{\"alg\":\"HS256\"}");
        var payload = Base64UrlEncode("{\"sub\":\"user\",\"exp\":1000000000}");
        var token = $"{header}.{payload}.sig";

        var result = EncodingCryptoTools.DecodeJwt(token);

        Assert.Contains("Expires", result);
        Assert.Contains("EXPIRED", result);
    }

    [Fact]
    public void DecodeJwt_InvalidFormat_ReturnsError()
    {
        var result = EncodingCryptoTools.DecodeJwt("not-a-jwt");

        Assert.Contains("❌", result);
        Assert.Contains("Invalid JWT format", result);
    }

    [Fact]
    public void DecodeJwt_UnsignedToken_ShowsNoSignature()
    {
        var header = Base64UrlEncode("{\"alg\":\"none\"}");
        var payload = Base64UrlEncode("{\"sub\":\"test\"}");
        var token = $"{header}.{payload}.";

        var result = EncodingCryptoTools.DecodeJwt(token);

        Assert.Contains("None (unsigned token)", result);
    }

    // ── Hashing ─────────────────────────────────────────────

    [Fact]
    public void ComputeHash_Sha256_ReturnsCorrectHash()
    {
        var result = EncodingCryptoTools.ComputeHash("hello");

        // Known SHA256 of "hello"
        Assert.Contains("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
        Assert.Contains("SHA256", result);
    }

    [Fact]
    public void ComputeHash_Sha512_ReturnsCorrectHash()
    {
        var result = EncodingCryptoTools.ComputeHash("hello", "sha512");

        // Known SHA512 of "hello"
        Assert.Contains("9b71d224bd62f3785d96d46ad3ea3d73319bfbc2890caadae2dff72519673ca72323c3d99ba5c11d7c7acc6e14b8c5da0c4663475c2e5c3adef46f73bcdec043", result);
        Assert.Contains("SHA512", result);
    }

    [Fact]
    public void ComputeHash_Md5_ReturnsHashWithWarning()
    {
        var result = EncodingCryptoTools.ComputeHash("hello", "md5");

        // Known MD5 of "hello"
        Assert.Contains("5d41402abc4b2a76b9719d911017c592", result);
        Assert.Contains("MD5", result);
        Assert.Contains("cryptographically broken", result);
    }

    [Fact]
    public void ComputeHash_Sha1_ReturnsHashWithWarning()
    {
        var result = EncodingCryptoTools.ComputeHash("hello", "sha1");

        // Known SHA1 of "hello"
        Assert.Contains("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", result);
        Assert.Contains("SHA1", result);
        Assert.Contains("deprecated", result);
    }

    [Fact]
    public void ComputeHash_Sha384_ReturnsCorrectHash()
    {
        var result = EncodingCryptoTools.ComputeHash("hello", "sha384");

        Assert.Contains("SHA384", result);
        Assert.Contains("59e1748777448c69de6b800d7a33bbfb9ff1b463e44354c3553bcdb9c666fa90125a3c79f90397bdf5f6a13de828684f", result);
    }

    [Fact]
    public void ComputeHash_UnknownAlgorithm_ReturnsError()
    {
        var result = EncodingCryptoTools.ComputeHash("hello", "unknown");

        Assert.Contains("❌", result);
        Assert.Contains("Unknown algorithm", result);
    }

    [Fact]
    public void ComputeHash_DefaultIsSha256()
    {
        var result = EncodingCryptoTools.ComputeHash("test");

        Assert.Contains("SHA256", result);
    }

    // ── GUID Generator ──────────────────────────────────────

    [Fact]
    public void GenerateGuid_Default_ReturnsSingleGuid()
    {
        var result = EncodingCryptoTools.GenerateGuid();

        Assert.Contains("Generated GUID", result);
        // Should contain exactly 1 GUID (36 chars: 8-4-4-4-12)
        var lines = result.Split('\n')
            .Where(l => Guid.TryParse(l.Trim(), out _))
            .ToList();
        Assert.Single(lines);
    }

    [Fact]
    public void GenerateGuid_Multiple_ReturnsRequestedCount()
    {
        var result = EncodingCryptoTools.GenerateGuid(5);

        var lines = result.Split('\n')
            .Where(l => Guid.TryParse(l.Trim(), out _))
            .ToList();
        Assert.Equal(5, lines.Count);
        Assert.Contains("5 unique v4 UUIDs", result);
    }

    [Fact]
    public void GenerateGuid_Unique_AllDifferent()
    {
        var result = EncodingCryptoTools.GenerateGuid(10);

        var guids = result.Split('\n')
            .Where(l => Guid.TryParse(l.Trim(), out _))
            .Select(l => l.Trim())
            .ToList();
        Assert.Equal(10, guids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GenerateGuid_ClampedToMax20()
    {
        var result = EncodingCryptoTools.GenerateGuid(100);

        var lines = result.Split('\n')
            .Where(l => Guid.TryParse(l.Trim(), out _))
            .ToList();
        Assert.Equal(20, lines.Count);
    }

    [Fact]
    public void GenerateGuid_ClampedToMin1()
    {
        var result = EncodingCryptoTools.GenerateGuid(0);

        var lines = result.Split('\n')
            .Where(l => Guid.TryParse(l.Trim(), out _))
            .ToList();
        Assert.Single(lines);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string Base64UrlEncode(string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
