using JD.AI.Core.Security;

namespace JD.AI.Gateway.Endpoints;

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/gateway/apikeys").WithTags("API Keys");

        // GET /api/v1/gateway/apikeys — list all API keys (masked)
        group.MapGet("/", (ApiKeyRotation keyRotation) =>
        {
            var keys = keyRotation.GetAllKeys();
            return Results.Ok(keys.Select(k => new ApiKeyResponse
            {
                Key = MaskKey(k.Key),
                Name = k.Name,
                Role = k.Role.ToString(),
                CreatedAt = k.CreatedAt,
                ExpiresAt = k.ExpiresAt,
                IsRevoked = k.IsRevoked,
                RevokedAt = k.RevokedAt,
                LastUsedAt = k.LastUsedAt,
                UsageCount = k.UsageCount,
                PreviousKey = k.PreviousKey is not null ? MaskKey(k.PreviousKey) : null,
            }).ToList());
        })
        .WithName("GetApiKeys")
        .WithDescription("List all API keys with masked values.");

        // POST /api/v1/gateway/apikeys — create a new API key
        group.MapPost("/", (CreateApiKeyRequest request, ApiKeyRotation keyRotation) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { Error = "Name is required" });

            var role = Enum.TryParse<GatewayRole>(request.Role, true, out var r)
                ? r
                : GatewayRole.User;

            var expiry = request.ExpiryDays.HasValue
                ? TimeSpan.FromDays(request.ExpiryDays.Value)
                : (TimeSpan?)null;

            var key = keyRotation.GenerateKey(request.Name, role, expiry);

            return Results.Created($"/api/v1/gateway/apikeys", new CreateApiKeyResponse
            {
                Key = key,
                Name = request.Name,
                Role = role.ToString(),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiry.HasValue ? DateTimeOffset.UtcNow + expiry : null,
            });
        })
        .WithName("CreateApiKey")
        .WithDescription("Create a new API key. Returns the full key value (only shown once).");

        // DELETE /api/v1/gateway/apikeys/{key} — revoke an API key
        group.MapDelete("/{key}", (string key, ApiKeyRotation keyRotation) =>
        {
            var masked = MaskKey(key);
            var success = keyRotation.RevokeKey(key);
            if (!success)
                return Results.NotFound(new { Error = $"API key '{masked}' not found" });

            return Results.Ok(new { Message = $"API key '{masked}' revoked successfully" });
        })
        .WithName("RevokeApiKey")
        .WithDescription("Revoke an API key.");

        // POST /api/v1/gateway/apikeys/{key}/rotate — rotate an API key
        group.MapPost("/{key}/rotate", (string key, RotateApiKeyRequest? request, ApiKeyRotation keyRotation) =>
        {
            var masked = MaskKey(key);
            var newExpiry = request?.ExpiryDays.HasValue == true
                ? TimeSpan.FromDays(request.ExpiryDays.Value)
                : (TimeSpan?)null;

            var newKey = keyRotation.RotateKey(key, newExpiry);
            if (newKey is null)
                return Results.NotFound(new { Error = $"API key '{masked}' not found" });

            return Results.Ok(new RotateApiKeyResponse
            {
                NewKey = newKey,
                OldKey = masked,
                RotatedAt = DateTimeOffset.UtcNow,
            });
        })
        .WithName("RotateApiKey")
        .WithDescription("Rotate an API key. The old key is revoked and a new one is returned.");

        // POST /api/v1/gateway/apikeys/{key}/touch — record usage (called by middleware internally)
        group.MapPost("/{key}/touch", (string key, ApiKeyRotation keyRotation) =>
        {
            var success = keyRotation.TouchKey(key);
            if (!success)
                return Results.NotFound(new { Error = $"API key '{MaskKey(key)}' not found or revoked" });

            return Results.Ok(new { Message = "Usage recorded" });
        })
        .WithName("TouchApiKey")
        .WithDescription("Record API key usage. Called by the auth middleware.");
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "***";

        return $"{key[..4]}...{key[^4..]}";
    }
}

// Request/Response DTOs

public sealed record CreateApiKeyRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string Role { get; init; } = "User";
    public int? ExpiryDays { get; init; }
}

public sealed record CreateApiKeyResponse
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record ApiKeyResponse
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public required bool IsRevoked { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public long UsageCount { get; init; }
    public string? PreviousKey { get; init; }
}

public sealed record RotateApiKeyRequest
{
    public int? ExpiryDays { get; init; }
}

public sealed record RotateApiKeyResponse
{
    public required string NewKey { get; init; }
    public required string OldKey { get; init; }
    public required DateTimeOffset RotatedAt { get; init; }
}
