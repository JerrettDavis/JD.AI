using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Agents;

/// <summary>
/// Loads <see cref="AgentDefinition"/> instances from <c>*.agent.yaml</c> files
/// in one or more search directories and registers them into an
/// <see cref="IAgentDefinitionRegistry"/>.
/// </summary>
public sealed class FileAgentDefinitionLoader
{
    private static readonly IDeserializer Deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    private readonly IAgentDefinitionRegistry _registry;
    private readonly ILogger<FileAgentDefinitionLoader> _logger;

    public FileAgentDefinitionLoader(
        IAgentDefinitionRegistry registry,
        ILogger<FileAgentDefinitionLoader> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Scans <paramref name="searchPaths"/> for <c>*.agent.yaml</c> files and loads
    /// each into the registry. Malformed files are skipped with a warning.
    /// </summary>
    public void LoadAll(IEnumerable<string> searchPaths)
    {
        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir))
                continue;

            var files = Directory.EnumerateFiles(dir, "*.agent.yaml", SearchOption.AllDirectories);
            foreach (var file in files)
                LoadFile(file);
        }
    }

    /// <summary>Loads and registers a single <c>.agent.yaml</c> file.</summary>
    public void LoadFile(string path)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            var definition = Deserializer.Deserialize<AgentDefinition>(yaml);

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                _logger.LogWarning("Skipping agent definition file {File}: missing 'name' field", path);
                return;
            }

            _registry.Register(definition);
            _logger.LogDebug("Loaded agent definition '{Name}' v{Version} from {File}",
                definition.Name, definition.Version, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse agent definition file {File}", path);
        }
    }
}
