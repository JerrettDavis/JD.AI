using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace JD.AI.Core.Providers.Credentials;

internal sealed class VaultCredentialBackend
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _vaultAddress;
    private readonly string _vaultToken;
    private readonly string _mount;
    private readonly string _prefix;

    public VaultCredentialBackend(HttpClient httpClient, EncryptedFileStoreOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _vaultAddress = options.VaultAddress!.TrimEnd('/');
        _vaultToken = options.VaultToken!;
        _mount = options.VaultMount.Trim('/');
        _prefix = options.VaultPrefix.Trim('/');
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        var encodedKey = EncodeKey(key);
        var url = $"{_vaultAddress}/v1/{_mount}/data/{_prefix}/{encodedKey}";

        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        return json.RootElement
            .GetProperty("data")
            .GetProperty("data")
            .GetProperty("value")
            .GetString();
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var encodedKey = EncodeKey(key);
        var url = $"{_vaultAddress}/v1/{_mount}/data/{_prefix}/{encodedKey}";

        using var request = CreateRequest(HttpMethod.Post, url);
        request.Content = JsonContent.Create(new
        {
            data = new { value },
        }, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var encodedKey = EncodeKey(key);
        var url = $"{_vaultAddress}/v1/{_mount}/metadata/{_prefix}/{encodedKey}";

        using var request = CreateRequest(HttpMethod.Delete, url);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct)
    {
        var url = $"{_vaultAddress}/v1/{_mount}/metadata/{_prefix}?list=true";

        using var request = CreateRequest(new HttpMethod("LIST"), url);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        if (!json.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("keys", out var keysElement))
        {
            return [];
        }

        var keys = new List<string>();
        foreach (var encoded in keysElement.EnumerateArray())
        {
            var key = DecodeKey(encoded.GetString());
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Vault-Token", _vaultToken);
        return request;
    }

    private static string EncodeKey(string key)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    private static string? DecodeKey(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        var payload = encoded
            .Replace("-", "+", StringComparison.Ordinal)
            .Replace("_", "/", StringComparison.Ordinal);

        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        var bytes = Convert.FromBase64String(payload);
        return Encoding.UTF8.GetString(bytes);
    }
}
