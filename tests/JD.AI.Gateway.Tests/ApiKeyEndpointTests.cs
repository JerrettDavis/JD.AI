using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Core.Security;
using JD.AI.Gateway.Endpoints;

namespace JD.AI.Gateway.Tests;

public sealed class ApiKeyEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public ApiKeyEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/v1/gateway/apikeys ────────────────────────────────────────

    [Fact]
    public async Task GetApiKeys_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/gateway/apikeys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetApiKeys_ReturnsList()
    {
        var response = await _client.GetAsync("/api/v1/gateway/apikeys");

        var keys = await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();

        keys.Should().NotBeNull();
        keys.Should().BeOfType<List<ApiKeyResponse>>();
    }

    // ── POST /api/v1/gateway/apikeys ───────────────────────────────────────

    [Fact]
    public async Task CreateApiKey_WithValidName_ReturnsCreated()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "test-key-" + Guid.NewGuid().ToString("N")[..8],
            Role = "Admin",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateApiKey_WithValidName_ReturnsFullKey()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "test-key-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        var created = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        created!.Key.Should().NotBeNullOrEmpty();
        created.Name.Should().Be(request.Name);
        created.Role.Should().Be("User");
        created.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateApiKey_WithExpiryDays_SetsExpiresAt()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "test-key-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
            ExpiryDays = 30,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        var created = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        created!.ExpiresAt.Should().NotBeNull();
        var expectedExpiry = DateTimeOffset.UtcNow.AddDays(30);
        created.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateApiKey_WithoutExpiryDays_ExpiresAtIsNull()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "test-key-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        var created = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        created!.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateApiKey_WithNullName_ReturnsBadRequest()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "",
            Role = "User",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_WithWhitespaceName_ReturnsBadRequest()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "   ",
            Role = "User",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_WithInvalidRole_DefaultsToUser()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "test-key-" + Guid.NewGuid().ToString("N")[..8],
            Role = "InvalidRole",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        var created = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        created!.Role.Should().Be("User");
    }

    // ── DELETE /api/v1/gateway/apikeys/{key} ───────────────────────────────

    [Fact]
    public async Task RevokeApiKey_WithInvalidKey_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/v1/gateway/apikeys/invalid-key-12345678");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeApiKey_AfterCreation_Revokes()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = "revoke-test-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var keyValue = created!.Key;

        var revokeResponse = await _client.DeleteAsync($"/api/v1/gateway/apikeys/{keyValue}");

        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/v1/gateway/apikeys/{key}/rotate ──────────────────────────

    [Fact]
    public async Task RotateApiKey_WithInvalidKey_ReturnsNotFound()
    {
        var request = new RotateApiKeyRequest { ExpiryDays = 60 };

        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys/invalid-key-12345678/rotate", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RotateApiKey_AfterCreation_ReturnsNewKey()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = "rotate-test-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var oldKey = created!.Key;

        var rotateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/gateway/apikeys/{oldKey}/rotate",
            new RotateApiKeyRequest { ExpiryDays = 60 });
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<RotateApiKeyResponse>();

        rotated!.NewKey.Should().NotBeNullOrEmpty();
        rotated.NewKey.Should().NotBe(oldKey);
        rotated.OldKey.Should().NotBeNullOrEmpty();
        rotated.RotatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RotateApiKey_WithoutExpiryDays_OldExpiryPreserved()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = "rotate-no-expiry-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
            ExpiryDays = 30,
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var oldKey = created!.Key;

        var rotateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/gateway/apikeys/{oldKey}/rotate",
            new RotateApiKeyRequest { ExpiryDays = null });

        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RotateApiKey_WithExpiryDays_UpdatesExpiry()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = "rotate-new-expiry-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
            ExpiryDays = 30,
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var oldKey = created!.Key;

        var rotateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/gateway/apikeys/{oldKey}/rotate",
            new RotateApiKeyRequest { ExpiryDays = 90 });
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<RotateApiKeyResponse>();

        rotated.Should().NotBeNull();
        rotated!.RotatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    // ── POST /api/v1/gateway/apikeys/{key}/touch ───────────────────────────

    [Fact]
    public async Task TouchApiKey_WithInvalidKey_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/api/v1/gateway/apikeys/invalid-key-12345678/touch", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TouchApiKey_RecordsUsage()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = "touch-test-" + Guid.NewGuid().ToString("N")[..8],
            Role = "User",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var keyValue = created!.Key;

        var touchResponse = await _client.PostAsync($"/api/v1/gateway/apikeys/{keyValue}/touch", null);

        touchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
