using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using JD.AI.Core.Infrastructure;
using JD.SemanticKernel.Connectors.OpenAICodex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a local OpenAI Codex session and exposes its models.
/// When running as a Windows service, scans user profiles for credentials.
/// Supports automatic token refresh and device code login.
/// </summary>
public sealed class OpenAICodexDetector : IProviderDetector
{
    private const string CodexProviderName = "OpenAI Codex";
    private static readonly Regex CliAuthCredentialsStorePattern = new(
        @"(?im)^\s*cli_auth_credentials_store\s*=\s*[""'](?<mode>file|keyring|auto)[""']",
        RegexOptions.CultureInvariant);

    public string ProviderName => CodexProviderName;

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        CodexSessionProvider? provider = null;
        try
        {
            var options = BuildSessionOptions();
            provider = CreateSessionProvider(options);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                // Token exchange may have failed — try refreshing via codex CLI.
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (refreshed)
                {
                    provider.Dispose();
                    provider = CreateSessionProvider(options);
                    isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);
                }

                if (!isAuth)
                {
                    return new ProviderInfo(
                        ProviderName,
                        IsAvailable: false,
                        StatusMessage: "Not authenticated — run 'codex login' (or 'codex login --device-auth')",
                        Models: []);
                }
            }

            // Force credential materialization once at startup. This triggers token
            // refresh/exchange logic and prevents false "Authenticated" status when
            // the current token is already expired.
            try
            {
                _ = await provider.GetApiKeyAsync(ct).ConfigureAwait(false);
            }
            catch (CodexSessionException)
            {
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (!refreshed)
                    throw;

                provider.Dispose();
                provider = CreateSessionProvider(options);
                _ = await provider.GetApiKeyAsync(ct).ConfigureAwait(false);
            }

            // Use live model discovery first, then local cache as a supplement.
            var models = new List<ProviderModelInfo>();
            try
            {
                var discovery = new CodexModelDiscovery();
                var discovered = await discovery.DiscoverModelsAsync(ct).ConfigureAwait(false);
                AddUniqueModels(models, discovered.Select(m =>
                    new ProviderModelInfo(m.Id, m.Name ?? m.Id, ProviderName)));
            }
#pragma warning disable CA1031 // catch broad — discovery is optional
            catch
#pragma warning restore CA1031
            {
                // Keep going to cache/fallback discovery.
            }

            AddUniqueModels(models, DiscoverModelsFromCache(options));

            if (models.Count == 0)
            {
                AddUniqueModels(models,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", ProviderName),
                    new ProviderModelInfo("gpt-5.2-codex", "gpt-5.2-codex", ProviderName),
                    new ProviderModelInfo("gpt-5.1-codex-max", "gpt-5.1-codex-max", ProviderName),
                    new ProviderModelInfo("gpt-5.2", "gpt-5.2", ProviderName),
                    new ProviderModelInfo("gpt-5.1-codex-mini", "gpt-5.1-codex-mini", ProviderName),
                ]);
            }

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"Authenticated — {models.Count} model(s)",
                Models: models);
        }
        catch (CodexSessionException ex)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: NormalizeStatusMessage(ex.Message),
                Models: []);
        }
        catch (Exception ex)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: NormalizeStatusMessage(ex.Message),
                Models: []);
        }
        finally
        {
            provider?.Dispose();
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var options = BuildSessionOptions();
        var builder = Kernel.CreateBuilder();
        builder.UseCodexChatCompletion(
            modelId: model.Id,
            configure: opts =>
            {
                opts.CredentialsPath = options.CredentialsPath;
                opts.ApiKey = options.ApiKey;
                opts.AccessToken = options.AccessToken;
            });
        return builder.Build();
    }

    /// <summary>
    /// Builds session options, scanning user profiles for credentials when
    /// running as a service account (LocalSystem, NetworkService, etc.).
    /// </summary>
    private static CodexSessionOptions BuildSessionOptions()
    {
        var options = new CodexSessionOptions();

        // Check if the default path resolves to a service account home.
        // If so, scan real user profiles for Codex credentials.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home) || UserProfileScanner.IsServiceAccount(home))
        {
            var credPath = UserProfileScanner.FindInUserProfiles(
                Path.Combine(".codex", "auth.json"));
            if (credPath is not null)
                options.CredentialsPath = credPath;
        }

        ApplyCredentialOverridesFromAuthFile(options);

        return options;
    }

    /// <summary>
    /// Reads Codex auth.json (if present) and promotes file API keys into explicit
    /// connector options so they win over environment-variable fallback precedence.
    ///
    /// We set OAuth bearer tokens as <see cref="CodexSessionOptions.ApiKey"/> intentionally:
    /// Semantic Kernel uses this value in an Authorization bearer header, which lets us
    /// avoid connector-specific token-exchange paths that can produce billing-scoped API keys
    /// and false 429 insufficient_quota results for ChatGPT-authenticated Codex sessions.
    /// </summary>
    internal static void ApplyCredentialOverridesFromAuthFile(
        CodexSessionOptions options,
        string? envApiKeyOverride = null)
    {
        if (!TryReadCodexCredentials(options, out var creds, out var authMode, out var source))
            return;
        // Prefer OAuth access_token over id_token so the connector can use direct
        // bearer auth before falling back to token-exchange behavior.
        var token = !string.IsNullOrWhiteSpace(creds.EffectiveAccessToken)
            ? creds.EffectiveAccessToken
            : creds.EffectiveIdToken;
        var envApiKey = envApiKeyOverride ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (source == CredentialSource.Keyring)
        {
            // Keyring-backed auth isn't read by this connector path, so provide
            // explicit credentials resolved from the keyring snapshot.
            if (ShouldPreferTokens(creds, authMode, envApiKey, token))
            {
                SetBearerTokenOverride(options, token!);
                return;
            }

            if (!string.IsNullOrWhiteSpace(creds.OpenAIApiKey))
            {
                options.ApiKey = creds.OpenAIApiKey;
                options.AccessToken = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                SetBearerTokenOverride(options, token);
            }

            return;
        }

        // Match Codex CLI intent: when auth.json is explicitly ChatGPT-based, prefer
        // OAuth tokens over API keys so account subscription auth isn't shadowed by
        // stale/billing-limited API keys in env or auth.json.
        if (ShouldPreferTokens(creds, authMode, envApiKey, token))
        {
            SetBearerTokenOverride(options, token!);
            return;
        }

        if (!string.IsNullOrWhiteSpace(creds.OpenAIApiKey))
        {
            options.ApiKey = creds.OpenAIApiKey;
            options.AccessToken = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(token) &&
            !string.IsNullOrWhiteSpace(envApiKey))
        {
            SetBearerTokenOverride(options, token);
        }
    }

    internal static IReadOnlyList<ProviderModelInfo> ReadModelsFromCache(string cachePath)
    {
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            return [];

        try
        {
            using var stream = File.OpenRead(cachePath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement) ||
                modelsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var models = new List<(int Priority, ProviderModelInfo Model)>();
            foreach (var entry in modelsElement.EnumerateArray())
            {
                var id = GetString(entry, "slug");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var visibility = GetString(entry, "visibility");
                if (!string.Equals(visibility, "list", StringComparison.OrdinalIgnoreCase))
                    continue;

                var supportedInApi = !entry.TryGetProperty("supported_in_api", out var supportedElement)
                    || supportedElement.ValueKind == JsonValueKind.True;
                if (!supportedInApi)
                    continue;

                var displayName = GetString(entry, "display_name") ?? id;
                var priority = entry.TryGetProperty("priority", out var priorityElement)
                    && priorityElement.TryGetInt32(out var parsedPriority)
                    ? parsedPriority
                    : int.MaxValue;

                models.Add((priority, new ProviderModelInfo(id, displayName, CodexProviderName)));
            }

            return models
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Model.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Model)
                .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ProviderModelInfo> DiscoverModelsFromCache(CodexSessionOptions options)
    {
        var cachePath = ResolveModelsCachePath(options);
        return cachePath is null ? [] : ReadModelsFromCache(cachePath);
    }

    private static string? ResolveModelsCachePath(CodexSessionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
        {
            var credentialsDirectory = Path.GetDirectoryName(options.CredentialsPath);
            if (!string.IsNullOrWhiteSpace(credentialsDirectory))
            {
                var sibling = Path.Combine(credentialsDirectory, "models_cache.json");
                if (File.Exists(sibling))
                    return sibling;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && !UserProfileScanner.IsServiceAccount(home))
        {
            var localCache = Path.Combine(home, ".codex", "models_cache.json");
            if (File.Exists(localCache))
                return localCache;
        }

        return UserProfileScanner.FindInUserProfiles(
            Path.Combine(".codex", "models_cache.json"));
    }

    private static string? ResolveAuthPath(CodexSessionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
            return options.CredentialsPath;

        var codexHome = ResolveCodexHomePath(options);
        if (string.IsNullOrWhiteSpace(codexHome))
            return null;

        return Path.Combine(codexHome, "auth.json");
    }

    private static bool TryReadCodexCredentials(
        CodexSessionOptions options,
        out CodexCredentialsFile creds,
        out string? authMode,
        out CredentialSource source)
    {
        var storeMode = ResolveAuthStoreMode(options);
        var authPath = ResolveAuthPath(options);
        var hasAuthPath = !string.IsNullOrWhiteSpace(authPath);

        switch (storeMode)
        {
            case AuthStoreMode.Keyring:
                if (TryReadCredentialsFromKeyring(options, out creds, out authMode))
                {
                    source = CredentialSource.Keyring;
                    return true;
                }
                break;
            case AuthStoreMode.Auto:
                if (TryReadCredentialsFromKeyring(options, out creds, out authMode))
                {
                    source = CredentialSource.Keyring;
                    return true;
                }

                if (hasAuthPath && TryReadCredentialsFromJsonFile(authPath!, out creds, out authMode))
                {
                    source = CredentialSource.File;
                    return true;
                }
                break;
            case AuthStoreMode.File:
            default:
                if (hasAuthPath && TryReadCredentialsFromJsonFile(authPath!, out creds, out authMode))
                {
                    source = CredentialSource.File;
                    return true;
                }
                break;
        }

        creds = default!;
        authMode = null;
        source = CredentialSource.File;
        return false;
    }

    private static bool TryReadCredentialsFromJsonFile(
        string authPath,
        out CodexCredentialsFile creds,
        out string? authMode)
    {
        creds = default!;
        authMode = null;

        if (!File.Exists(authPath))
            return false;

        try
        {
            using var stream = File.OpenRead(authPath);
            using var doc = JsonDocument.Parse(stream);
            creds = JsonSerializer.Deserialize<CodexCredentialsFile>(doc.RootElement.GetRawText())
                ?? default!;
            if (creds is null)
                return false;

            authMode = ReadAuthMode(doc);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void AddUniqueModels(
        List<ProviderModelInfo> target,
        IEnumerable<ProviderModelInfo> candidates)
    {
        foreach (var model in candidates)
        {
            if (!target.Any(existing =>
                string.Equals(existing.Id, model.Id, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(model);
            }
        }
    }

    private static string? GetString(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string? ReadAuthMode(string authPath)
    {
        try
        {
            using var stream = File.OpenRead(authPath);
            using var doc = JsonDocument.Parse(stream);
            return ReadAuthMode(doc);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadAuthMode(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("auth_mode", out var mode) &&
            mode.ValueKind == JsonValueKind.String)
        {
            return mode.GetString();
        }

        return null;
    }

    private static bool IsChatGptAuthMode(string? authMode) =>
        !string.IsNullOrWhiteSpace(authMode) &&
        authMode.Contains("chatgpt", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldPreferTokens(
        CodexCredentialsFile creds,
        string? authMode,
        string? envApiKey,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (IsChatGptAuthMode(authMode))
            return true;

        var apiKeyWouldShadow = !string.IsNullOrWhiteSpace(creds.OpenAIApiKey) ||
                                !string.IsNullOrWhiteSpace(envApiKey);
        if (!apiKeyWouldShadow)
            return false;

        // Match Codex CLI behavior for explicit ChatGPT auth mode and also recover
        // cases where auth_mode is missing but token payload still looks like OAuth
        // JWT output from `codex login`.
        return LooksLikeOAuthJwt(token);
    }

    private static void SetBearerTokenOverride(CodexSessionOptions options, string token)
    {
        options.ApiKey = token;
        options.AccessToken = null;
    }

    private static bool LooksLikeOAuthJwt(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("eyJ", StringComparison.Ordinal))
            return false;

        var firstDot = token.IndexOf('.');
        if (firstDot <= 0)
            return false;

        var secondDot = token.IndexOf('.', firstDot + 1);
        return secondDot > firstDot + 1 && secondDot < token.Length - 1;
    }

    private static bool TryReadCredentialsFromKeyring(
        CodexSessionOptions options,
        out CodexCredentialsFile creds,
        out string? authMode)
    {
        creds = default!;
        authMode = null;

        if (!OperatingSystem.IsWindows())
            return false;

        var codexHome = ResolveCodexHomePath(options);
        if (string.IsNullOrWhiteSpace(codexHome))
            return false;

        var account = ComputeCodexKeyringAccountKey(codexHome);
        if (string.IsNullOrWhiteSpace(account))
            return false;

        if (!TryReadWindowsCodexKeyringJson(account, out var json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            creds = JsonSerializer.Deserialize<CodexCredentialsFile>(doc.RootElement.GetRawText())
                ?? default!;
            if (creds is null)
                return false;

            authMode = ReadAuthMode(doc);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ResolveCodexHomePath(CodexSessionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
        {
            var dir = Path.GetDirectoryName(options.CredentialsPath);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;
        }

        var envCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(envCodexHome))
            return envCodexHome;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            return null;

        return Path.Combine(home, ".codex");
    }

    private static AuthStoreMode ResolveAuthStoreMode(CodexSessionOptions options)
    {
        var codexHome = ResolveCodexHomePath(options);
        if (string.IsNullOrWhiteSpace(codexHome))
            return AuthStoreMode.File;

        var configPath = Path.Combine(codexHome, "config.toml");
        if (!File.Exists(configPath))
            return AuthStoreMode.File;

        try
        {
            var config = File.ReadAllText(configPath);
            var match = CliAuthCredentialsStorePattern.Match(config);
            if (!match.Success)
                return AuthStoreMode.File;

            var mode = match.Groups["mode"].Value;
            return mode.ToLowerInvariant() switch
            {
                "keyring" => AuthStoreMode.Keyring,
                "auto" => AuthStoreMode.Auto,
                _ => AuthStoreMode.File,
            };
        }
        catch (IOException)
        {
            return AuthStoreMode.File;
        }
        catch (UnauthorizedAccessException)
        {
            return AuthStoreMode.File;
        }
    }

    internal static string ComputeCodexKeyringAccountKey(string codexHomePath)
    {
        var normalized = codexHomePath;
        try
        {
            normalized = Path.GetFullPath(codexHomePath);
        }
        catch
        {
            // Keep original path if normalization fails.
        }

        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        var shortHex = hex.Length >= 16 ? hex[..16] : hex;
        return $"cli|{shortHex}";
    }

    private static bool TryReadWindowsCodexKeyringJson(string account, out string json)
    {
        json = string.Empty;
        const string service = "Codex Auth";

        // Common Windows target shapes used by keyring wrappers.
        var targets = new[]
        {
            $"{service}:{account}",
            $"{service}/{account}",
            account,
            service,
        };

        foreach (var target in targets)
        {
            if (TryCredReadJson(target, out json))
                return true;
        }

        return TryCredEnumerateJson(account, out json);
    }

    private static bool TryCredReadJson(string targetName, out string json)
    {
        json = string.Empty;
        if (!CredRead(targetName, CRED_TYPE_GENERIC, 0, out var credPtr) || credPtr == IntPtr.Zero)
            return false;

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            var payload = CredentialBlobToString(
                credential.CredentialBlob,
                credential.CredentialBlobSize);
            if (!string.IsNullOrWhiteSpace(payload) &&
                payload.TrimStart().StartsWith('{'))
            {
                json = payload;
                return true;
            }
        }
        finally
        {
            CredFree(credPtr);
        }

        return false;
    }

    private static bool TryCredEnumerateJson(string account, out string json)
    {
        json = string.Empty;
        if (!CredEnumerate(null, 0, out var count, out var credsPtr) || credsPtr == IntPtr.Zero)
            return false;

        try
        {
            for (var i = 0; i < count; i++)
            {
                var credPtr = Marshal.ReadIntPtr(credsPtr, i * IntPtr.Size);
                if (credPtr == IntPtr.Zero)
                    continue;

                var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (credential.Type != CRED_TYPE_GENERIC)
                    continue;

                var target = Marshal.PtrToStringUni(credential.TargetName) ?? string.Empty;
                if (!target.Contains("Codex", StringComparison.OrdinalIgnoreCase) &&
                    !target.Contains(account, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var payload = CredentialBlobToString(
                    credential.CredentialBlob,
                    credential.CredentialBlobSize);
                if (string.IsNullOrWhiteSpace(payload))
                    continue;

                var trimmed = payload.TrimStart();
                if (!trimmed.StartsWith('{'))
                    continue;

                json = payload;
                return true;
            }
        }
        finally
        {
            CredFree(credsPtr);
        }

        return false;
    }

    private static string CredentialBlobToString(IntPtr blobPtr, uint blobSize)
    {
        if (blobPtr == IntPtr.Zero || blobSize == 0)
            return string.Empty;

        var bytes = new byte[blobSize];
        Marshal.Copy(blobPtr, bytes, 0, (int)blobSize);

        var utf8 = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        if (utf8.StartsWith('{'))
            return utf8;

        if (bytes.Length % 2 == 0)
        {
            var utf16 = System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            if (utf16.StartsWith('{'))
                return utf16;
        }

        return utf8;
    }

    private static CodexSessionProvider CreateSessionProvider(CodexSessionOptions options) =>
        new(
            Options.Create(options),
            NullLogger<CodexSessionProvider>.Instance);

    private static async Task<bool> TryRefreshAuthAsync(CancellationToken ct)
    {
        try
        {
            var codexPath = ClaudeCodeDetector.FindCli("codex");
            if (codexPath is null)
                return false;

            Console.WriteLine("  ↻ Attempting Codex session refresh...");

            var status = await ProcessExecutor.RunAsync(
                codexPath,
                "login status",
                timeout: TimeSpan.FromSeconds(15),
                cancellationToken: ct).ConfigureAwait(false);

            if (status.Success)
                return true;

            // Fallback probe that can still trigger auth file/keyring re-read paths.
            var version = await ProcessExecutor.RunAsync(
                codexPath,
                "--version",
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: ct).ConfigureAwait(false);

            return version.Success;
        }
#pragma warning disable CA1031 // best-effort refresh
        catch { return false; }
#pragma warning restore CA1031
    }

    private static string NormalizeStatusMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Unavailable";

        if (message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("billing", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenAI quota/billing unavailable (429 insufficient_quota). " +
                   "Codex ChatGPT OAuth may be authenticated but still lack OpenAI API billing quota; " +
                   "use an API key with available credits, or re-run `codex login` if the session is stale.";
        }

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return "Rate limited by OpenAI API (429). Retry shortly.";
        }

        return message;
    }

    private enum CredentialSource
    {
        File,
        Keyring,
    }

    private enum AuthStoreMode
    {
        File,
        Keyring,
        Auto,
    }

    private const uint CRED_TYPE_GENERIC = 1;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credentialPtr);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(
        string? filter,
        uint flags,
        out uint count,
        out IntPtr credentialsPtr);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
