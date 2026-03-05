using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Security;

/// <summary>
/// JWT bearer token authentication provider. Validates HMAC-SHA256 signed
/// JWT tokens and extracts identity claims.
/// </summary>
public sealed class JwtAuthProvider : IAuthProvider
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private readonly byte[] _signingKey;
    private readonly string _issuer;
    private readonly TimeSpan _clockSkew;

    /// <param name="signingKey">HMAC-SHA256 signing key (minimum 32 bytes).</param>
    /// <param name="issuer">Expected issuer claim. Tokens with different issuers are rejected.</param>
    /// <param name="clockSkew">Tolerance for clock differences. Default 5 minutes.</param>
    public JwtAuthProvider(byte[] signingKey, string issuer = "jdai", TimeSpan? clockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (signingKey.Length < 32)
            throw new ArgumentException("Signing key must be at least 32 bytes", nameof(signingKey));

        _signingKey = signingKey;
        _issuer = issuer;
        _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Issues a JWT token for the given identity.
    /// </summary>
    public string IssueToken(string subject, string displayName, GatewayRole role, TimeSpan? expiry = null)
    {
        var exp = DateTimeOffset.UtcNow + (expiry ?? TimeSpan.FromHours(1));

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "HS256", typ = "JWT" }, JsonOptions));

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new JwtPayload
        {
            Sub = subject,
            Name = displayName,
            Role = role.ToString(),
            Iss = _issuer,
            Iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp = exp.ToUnixTimeSeconds(),
        }, JsonOptions));

        var signature = ComputeSignature($"{header}.{payload}");
        return $"{header}.{payload}.{signature}";
    }

    public Task<GatewayIdentity?> AuthenticateAsync(string credential, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(credential))
            return Task.FromResult<GatewayIdentity?>(null);

        // Strip "Bearer " prefix if present
        var token = credential.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? credential["Bearer ".Length..]
            : credential;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return Task.FromResult<GatewayIdentity?>(null);

        // Verify signature
        var expectedSig = ComputeSignature($"{parts[0]}.{parts[1]}");
        if (!string.Equals(parts[2], expectedSig, StringComparison.Ordinal))
            return Task.FromResult<GatewayIdentity?>(null);

        // Decode payload
        JwtPayload? payload;
        try
        {
            var json = Base64UrlDecode(parts[1]);
            payload = JsonSerializer.Deserialize<JwtPayload>(json, JsonOptions);
        }
        catch
        {
            return Task.FromResult<GatewayIdentity?>(null);
        }

        if (payload is null)
            return Task.FromResult<GatewayIdentity?>(null);

        // Validate issuer
        if (!string.Equals(payload.Iss, _issuer, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<GatewayIdentity?>(null);

        // Validate expiry
        var now = DateTimeOffset.UtcNow;
        var expiry = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (now > expiry + _clockSkew)
            return Task.FromResult<GatewayIdentity?>(null);

        // Parse role
        if (!Enum.TryParse<GatewayRole>(payload.Role, ignoreCase: true, out var role))
            role = GatewayRole.User;

        var identity = new GatewayIdentity(
            payload.Sub ?? "unknown",
            payload.Name ?? payload.Sub ?? "unknown",
            role,
            DateTimeOffset.FromUnixTimeSeconds(payload.Iat))
        {
            Claims = new Dictionary<string, string>
            {
                ["iss"] = payload.Iss ?? "",
                ["sub"] = payload.Sub ?? "",
                ["role"] = role.ToString(),
            },
        };

        return Task.FromResult<GatewayIdentity?>(identity);
    }

    private string ComputeSignature(string input)
    {
        var hash = HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes(input));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}

/// <summary>JWT payload claims.</summary>
internal sealed class JwtPayload
{
    public string? Sub { get; set; }
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Iss { get; set; }
    public long Iat { get; set; }
    public long Exp { get; set; }
}
