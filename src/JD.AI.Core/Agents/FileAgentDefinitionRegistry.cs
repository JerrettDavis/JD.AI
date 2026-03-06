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
        _ = Task.Run(async () =>
        {
            var all = await ListAsync(AgentEnvironments.Dev).ConfigureAwait(false);
            foreach (var d in all) _memCache.Register(d);
        });
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
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("AgentDefinition.Name is required.", nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Version))
            throw new ArgumentException("AgentDefinition.Version is required.", nameof(definition));

        var dir = GetEnvDir(environment);
        Directory.CreateDirectory(dir);

        var yaml = _serializer.Serialize(definition);
        var filePath = GetFilePath(dir, definition.Name, definition.Version);
        var checksumPath = GetChecksumPath(dir, definition.Name, definition.Version);

        await File.WriteAllTextAsync(filePath, yaml, cancellationToken).ConfigureAwait(false);

        var checksum = ComputeChecksum(yaml);
        await File.WriteAllTextAsync(checksumPath, checksum, cancellationToken).ConfigureAwait(false);

        _memCache.Register(definition);
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
        var dir = GetEnvDir(environment);
        var filePath = GetFilePath(dir, name, version);
        var checksumPath = GetChecksumPath(dir, name, version);

        if (File.Exists(filePath))
            File.Delete(filePath);
        if (File.Exists(checksumPath))
            File.Delete(checksumPath);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task PromoteAsync(
        string name,
        string version,
        string fromEnvironment,
        string toEnvironment,
        CancellationToken cancellationToken = default)
    {
        var definition = await ResolveAsync(name, version, fromEnvironment, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
            throw new InvalidOperationException(
                $"Agent '{name}@{version}' not found in '{fromEnvironment}' environment.");

        await RegisterAsync(definition, toEnvironment, cancellationToken).ConfigureAwait(false);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private string GetEnvDir(string environment) =>
        Path.Combine(_rootPath, environment.ToLowerInvariant());

    private static string GetFilePath(string dir, string name, string version) =>
        Path.Combine(dir, $"{SanitizeName(name)}@{SanitizeName(version)}.agent.yaml");

    private static string GetChecksumPath(string dir, string name, string version) =>
        Path.Combine(dir, $"{SanitizeName(name)}@{SanitizeName(version)}.sha256");

    private static string SanitizeName(string s) =>
        string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));

    private async Task<AgentDefinition?> LoadAndVerifyAsync(
        string filePath, CancellationToken ct)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

            // Verify checksum if a companion file exists
            var dir = Path.GetDirectoryName(filePath)!;
            var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath));
            var checksumPath = Path.Combine(dir, fileName + ".sha256");
            if (File.Exists(checksumPath))
            {
                var storedChecksum = await File.ReadAllTextAsync(checksumPath, ct).ConfigureAwait(false);
                var actualChecksum = ComputeChecksum(yaml);
                if (!storedChecksum.Equals(actualChecksum, StringComparison.Ordinal))
                    return null; // checksum mismatch — skip tampered file
            }

            return _deserializer.Deserialize<AgentDefinition>(yaml);
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
            .First();
    }

    private static Version ParseSemVer(string version)
    {
        // Normalize "1.0" → "1.0.0" for consistent comparison
        var normalized = version.Contains('.') ? version : version + ".0";
        return System.Version.TryParse(normalized, out var v) ? v : new Version(0, 0, 0);
    }
}
