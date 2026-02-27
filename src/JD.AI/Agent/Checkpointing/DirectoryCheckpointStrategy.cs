using System.Text.Json;

namespace JD.AI.Tui.Agent.Checkpointing;

/// <summary>
/// Checkpoint strategy for non-git directories. Copies tracked files to
/// .jdai/checkpoints/{id}/ for snapshotting.
/// </summary>
public sealed class DirectoryCheckpointStrategy : ICheckpointStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _workingDir;
    private readonly string _checkpointRoot;

    public DirectoryCheckpointStrategy(string? workingDir = null)
    {
        _workingDir = workingDir ?? Directory.GetCurrentDirectory();
        _checkpointRoot = Path.Combine(_workingDir, ".jdai", "checkpoints");
    }

    public Task<string?> CreateAsync(string label, CancellationToken ct = default)
    {
        var id = $"cp-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var cpDir = Path.Combine(_checkpointRoot, id);
        Directory.CreateDirectory(cpDir);

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".ts", ".js", ".py", ".go", ".rs", ".java", ".json", ".xml",
            ".yaml", ".yml", ".toml", ".md", ".txt", ".csproj", ".sln", ".slnx",
        };

        foreach (var file in Directory.EnumerateFiles(_workingDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            if (file.Contains($"{Path.DirectorySeparatorChar}.", StringComparison.Ordinal) ||
                file.Contains(".jdai", StringComparison.Ordinal))
                continue;

            if (!extensions.Contains(Path.GetExtension(file)))
                continue;

            var relative = Path.GetRelativePath(_workingDir, file);
            var dest = Path.Combine(cpDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        var meta = new { Id = id, Label = label, CreatedAt = DateTime.UtcNow };
        File.WriteAllText(
            Path.Combine(cpDir, ".checkpoint.json"),
            JsonSerializer.Serialize(meta, JsonOptions));

        return System.Threading.Tasks.Task.FromResult<string?>(id);
    }

    public Task<IReadOnlyList<CheckpointInfo>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_checkpointRoot))
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<CheckpointInfo>>([]);

        var results = new List<CheckpointInfo>();
        foreach (var dir in Directory.GetDirectories(_checkpointRoot).Order())
        {
            ct.ThrowIfCancellationRequested();
            var metaFile = Path.Combine(dir, ".checkpoint.json");
            var id = Path.GetFileName(dir);
            var label = id;
            var createdAt = Directory.GetCreationTimeUtc(dir);

            if (File.Exists(metaFile))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaFile));
                    label = doc.RootElement.TryGetProperty("Label", out var l) ? l.GetString() ?? id : id;
                    if (doc.RootElement.TryGetProperty("CreatedAt", out var c))
                        createdAt = c.GetDateTime();
                }
#pragma warning disable CA1031
                catch { /* use defaults */ }
#pragma warning restore CA1031
            }

            results.Add(new CheckpointInfo(id, label, createdAt));
        }

        return System.Threading.Tasks.Task.FromResult<IReadOnlyList<CheckpointInfo>>(results);
    }

    public Task<bool> RestoreAsync(string checkpointId, CancellationToken ct = default)
    {
        var cpDir = Path.Combine(_checkpointRoot, checkpointId);
        if (!Directory.Exists(cpDir))
            return System.Threading.Tasks.Task.FromResult(false);

        foreach (var file in Directory.EnumerateFiles(cpDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFileName(file), ".checkpoint.json", StringComparison.Ordinal))
                continue;

            var relative = Path.GetRelativePath(cpDir, file);
            var dest = Path.Combine(_workingDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        return System.Threading.Tasks.Task.FromResult(true);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        if (Directory.Exists(_checkpointRoot))
            Directory.Delete(_checkpointRoot, recursive: true);

        return System.Threading.Tasks.Task.CompletedTask;
    }
}
