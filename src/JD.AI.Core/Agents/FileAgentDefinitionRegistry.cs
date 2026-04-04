using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Agents;

/// <summary>
/// File-backed <see cref="IVersionedAgentDefinitionRegistry"/> that stores
/// <see cref="AgentDefinition"/> instances as <c>*.agent.yaml</c> files inside
/// environment-scoped sub-directories of a root path.
/// </summary>
/// <remarks>
/// Directory layout:
/// <code>
///   {root}/dev/     → development agent definitions
///   {root}/staging/ → staging agent definitions
///   {root}/prod/    → production agent definitions
/// </code>
/// Files are named <c>{name}@{version}.agent.yaml</c>.
/// A companion <c>{name}@{version}.sha256</c> file stores the checksum.
/// </remarks>
public sealed class FileAgentDefinitionRegistry : IVersionedAgentDefinitionRegistry
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly string _rootPath;

    // In-memory layer for fast synchronous lookups (IAgentDefinitionRegistry compat)
    private readonly AgentDefinitionRegistry _memCache = new();

    public FileAgentDefinitionRegistry(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(rootPath);

        // Pre-warm in-memory cache from dev by default (used by sync IAgentDefinitionRegistry)
        // synchronously so callers don't race background file reads against writes/deletes.
        var all = ListAsync(AgentEnvironments.Dev).ConfigureAwait(false).GetAwaiter().GetResult();
        foreach (var definition in all
                     .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(group => ResolveLatest(group.ToList())))
        {
            _memCache.Register(definition);
        }
    }

    // ── IAgentDefinitionRegistry (sync, in-memory) ─────────────────────────

    public IReadOnlyList<AgentDefinition> GetAll() => _memCache.GetAll();
    public AgentDefinition? GetByName(string name) => _memCache.GetByName(name);
    public void Register(AgentDefinition definition) => _memCache.Register(definition);
    public IReadOnlyList<AgentDefinition> GetByTag(string tag) => _memCache.GetByTag(tag);

    // ── IVersionedAgentDefinitionRegistry ─────────────────────────────────

    public async Task RegisterAsync(
        AgentDefinition definition,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateKeyPart(definition.Name, nameof(definition.Name));
        AgentVersions.Normalize(definition.Version, nameof(definition.Version));

        var dir = GetEnvDir(environment);
        Directory.CreateDirectory(dir);

        var yaml = _serializer.Serialize(definition);
        var filePath = GetFilePath(dir, definition.Name, definition.Version);
        var checksumPath = GetChecksumPath(dir, definition.Name, definition.Version);

        await File.WriteAllTextAsync(filePath, yaml, cancellationToken).ConfigureAwait(false);

        var checksum = ComputeChecksum(yaml);
        await File.WriteAllTextAsync(checksumPath, checksum, cancellationToken).ConfigureAwait(false);

        if (IsDevEnvironment(environment))
            await RefreshDevCacheEntryAsync(definition.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentDefinition?> ResolveAsync(
        string name,
        string? version = null,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default)
    {
        var all = await ListAsync(environment, cancellationToken).ConfigureAwait(false);
        var matches = all
            .Where(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return null;

        if (version is null or "latest")
            return ResolveLatest(matches);

        return matches.FirstOrDefault(d =>
            d.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<AgentDefinition>> ListAsync(
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default)
    {
        var dir = GetEnvDir(environment);
        if (!Directory.Exists(dir)) return [];

        var results = new List<AgentDefinition>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.agent.yaml"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var def = await LoadAndVerifyAsync(file, cancellationToken).ConfigureAwait(false);
            if (def is not null) results.Add(def);
        }
        return results.AsReadOnly();
    }

    public async Task UnregisterAsync(
        string name,
        string version,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPart(name, nameof(name));
        AgentVersions.Normalize(version, nameof(version));

        var dir = GetEnvDir(environment);
        var filePath = GetFilePath(dir, name, version);
        var checksumPath = GetChecksumPath(dir, name, version);

        if (File.Exists(filePath))
            File.Delete(filePath);
        if (File.Exists(checksumPath))
            File.Delete(checksumPath);

        if (IsDevEnvironment(environment))
            await RefreshDevCacheEntryAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public async Task PromoteAsync(
        string name,
        string version,
        string fromEnvironment,
        string toEnvironment,
        CancellationToken cancellationToken = default)
    {
        fromEnvironment = AgentEnvironments.Normalize(fromEnvironment, nameof(fromEnvironment));
        toEnvironment = AgentEnvironments.Normalize(toEnvironment, nameof(toEnvironment));

        var expectedTarget = AgentEnvironments.NextAfter(fromEnvironment);
        if (!string.Equals(toEnvironment, expectedTarget, StringComparison.OrdinalIgnoreCase))
        {
            var destination = expectedTarget ?? "no further environment";
            throw new InvalidOperationException(
                $"Invalid promotion path: '{fromEnvironment}' can only promote to '{destination}'.");
        }

        var definition = await ResolveAsync(name, version, fromEnvironment, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
            throw new InvalidOperationException(
                $"Agent '{name}@{version}' not found in '{fromEnvironment}' environment.");

        await RegisterAsync(definition, toEnvironment, cancellationToken).ConfigureAwait(false);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private string GetEnvDir(string environment) =>
        Path.Combine(_rootPath, AgentEnvironments.Normalize(environment));

    private static bool IsDevEnvironment(string environment) =>
        environment.Equals(AgentEnvironments.Dev, StringComparison.OrdinalIgnoreCase);

    private static string GetFilePath(string dir, string name, string version) =>
        Path.Combine(dir, $"{SanitizeName(name)}@{SanitizeName(version)}.agent.yaml");

    private static string GetChecksumPath(string dir, string name, string version) =>
        Path.Combine(dir, $"{SanitizeName(name)}@{SanitizeName(version)}.sha256");

    private static string SanitizeName(string s) =>
        string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));

    private static void ValidateKeyPart(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (value.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '-'))
        {
            throw new ArgumentException(
                "Only letters, digits, '.', and '-' are allowed.",
                paramName);
        }
    }

    private async Task RefreshDevCacheEntryAsync(string name, CancellationToken cancellationToken)
    {
        var matches = (await ListAsync(AgentEnvironments.Dev, cancellationToken).ConfigureAwait(false))
            .Where(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            _memCache.Unregister(name);
            return;
        }

        _memCache.Register(ResolveLatest(matches));
    }

    private async Task<AgentDefinition?> LoadAndVerifyAsync(
        string filePath, CancellationToken ct)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

            var dir = Path.GetDirectoryName(filePath)!;
            var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath));
            var checksumPath = Path.Combine(dir, fileName + ".sha256");
            if (!File.Exists(checksumPath))
                return null;

            var storedChecksum = await File.ReadAllTextAsync(checksumPath, ct).ConfigureAwait(false);
            var actualChecksum = ComputeChecksum(yaml);
            if (!storedChecksum.Equals(actualChecksum, StringComparison.Ordinal))
                return null; // checksum mismatch — skip tampered file

            var definition = _deserializer.Deserialize<AgentDefinition>(yaml);
            ValidateKeyPart(definition.Name, nameof(definition.Name));
            AgentVersions.Normalize(definition.Version, nameof(definition.Version));
            if (!string.IsNullOrWhiteSpace(definition.Environment))
                definition.Environment = AgentEnvironments.Normalize(definition.Environment);

            return definition;
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Returns the definition with the highest semantic version.</summary>
    private static AgentDefinition ResolveLatest(List<AgentDefinition> candidates)
    {
        return candidates
            .OrderByDescending(d => ParseSemVer(d.Version))
            .ThenByDescending(d => d.Version.Count(c => c == '.'))
            .ThenByDescending(d => d.Version, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static Version ParseSemVer(string version)
    {
        return AgentVersions.ParseOrZero(version);
    }
}
