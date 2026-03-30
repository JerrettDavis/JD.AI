using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace JD.AI.Gateway.Tests;

public sealed class ApiKeyEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public ApiKeyEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetApiKeys_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/gateway/apikeys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var keys = await response.Content.ReadFromJsonAsync<JsonElement>();
        keys.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CreateApiKey_WithValidName_ReturnsCreated()
    {
        var request = new { Name = "test-key", Role = "User" };
        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("test-key");
        body.GetProperty("key").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("key").GetString().Should().StartWith("jdai_");
    }

    [Fact]
    public async Task CreateApiKey_WithMissingName_ReturnsBadRequest()
    {
        var request = new { Role = "User" };
        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_WithExpiry_SetsExpiresAt()
    {
        var request = new { Name = "expiring-key", Role = "User", ExpiryDays = 30 };
        var response = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("expiresAt", out var expiresAt).Should().BeTrue();
        expiresAt.ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task RotateApiKey_WithValidKey_ReturnsNewKey()
    {
        // Create a key first
        var createRequest = new { Name = "rotate-test", Role = "User" };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var originalKey = created.GetProperty("key").GetString()!;

        // Rotate it (rotate endpoint takes masked key - we use the full key path for testing)
        var rotateResponse = await _client.PostAsync(
            $"/api/v1/gateway/apikeys/{Uri.EscapeDataString(originalKey)}/rotate",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = await rotateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newKey = rotated.GetProperty("newKey").GetString();
        newKey.Should().NotBeNullOrEmpty();
        newKey.Should().StartWith("jdai_");
        string.Equals(newKey, originalKey, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public async Task GetApiKeys_AfterCreate_ShowsMaskedKey()
    {
        // Create a key
        var createRequest = new { Name = "masked-test", Role = "User" };
        await _client.PostAsJsonAsync("/api/v1/gateway/apikeys", createRequest);

        // List keys
        var listResponse = await _client.GetAsync("/api/v1/gateway/apikeys");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var keys = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var maskedKey = keys.EnumerateArray()
            .FirstOrDefault(k => string.Equals(k.GetProperty("name").GetString(), "masked-test", StringComparison.Ordinal))
            .GetProperty("key").GetString();

        // Key should be masked: xxxx...xxxx
        maskedKey.Should().NotBeNullOrEmpty();
        maskedKey.Should().NotStartWith("jdai_");
        maskedKey.Should().Contain("...");
    }
}
