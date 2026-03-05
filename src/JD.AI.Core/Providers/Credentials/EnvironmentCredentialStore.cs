namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Reads credentials from environment variables. Container-safe — works with
/// Kubernetes Secrets mounted as env vars, Docker --env, and systemd EnvironmentFile.
/// <para>
/// Keys are mapped to env var names by uppercasing and replacing colons/dots with underscores.
/// For example, <c>jdai:provider:openai:apikey</c> → <c>JDAI_PROVIDER_OPENAI_APIKEY</c>.
/// </para>
/// </summary>
public sealed class EnvironmentCredentialStore : ICredentialStore
{
    private const string Prefix = "JDAI_";

    public bool IsAvailable => true;
    public string StoreName => "Environment Variable Store";

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var envName = KeyToEnvVar(key);
        var value = Environment.GetEnvironmentVariable(envName);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        // Environment variables are read-only in production; ignore writes
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var envPrefix = KeyToEnvVar(prefix);
        var vars = Environment.GetEnvironmentVariables();
        var keys = new List<string>();
        foreach (System.Collections.DictionaryEntry entry in vars)
        {
            if (entry.Key is string name &&
                name.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase) &&
                entry.Value is string { Length: > 0 })
            {
                keys.Add(EnvVarToKey(name));
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    internal static string KeyToEnvVar(string key) =>
        key.Replace(':', '_').Replace('.', '_').ToUpperInvariant();

    private static string EnvVarToKey(string envVar) =>
        envVar.Replace('_', ':').ToLowerInvariant();
}
