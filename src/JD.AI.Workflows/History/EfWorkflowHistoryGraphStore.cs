using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace JD.AI.Workflows.History;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowHistoryGraphStore"/> that persists
/// workflow history graph nodes and edges to an SQLite database.
/// Uses <see cref="WorkflowHistoryDbContext"/> which maps to the same tables created
/// by the WorkflowFramework Dashboard migration (AddWorkflowHistoryGraph).
/// </summary>
public sealed class EfWorkflowHistoryGraphStore : IWorkflowHistoryGraphStore
{
    private readonly WorkflowHistoryDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public EfWorkflowHistoryGraphStore(WorkflowHistoryDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WorkflowHistoryGraph> LoadAsync(CancellationToken ct = default)
    {
        var graph = new WorkflowHistoryGraph();

        var nodes = await _db.HistoryNodes
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var entity in nodes)
            graph.GetOrAddNode(entity.Fingerprint, () => RowToNode(entity));

        var edges = await _db.HistoryEdges
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var entity in edges)
        {
            if (!Enum.TryParse<EdgeKind>(entity.Kind, out var kind))
                continue;
            graph.RecordTransition(
                entity.SourceFingerprint,
                entity.TargetFingerprint,
                kind,
                string.Empty,
                TimeSpan.FromTicks(entity.AverageTransitionTimeTicks));
        }

        return graph;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WorkflowHistoryGraph graph, CancellationToken ct = default)
    {
        foreach (var node in graph.Nodes)
            await UpsertNodeAsync(node, ct);
        foreach (var edge in graph.Edges)
            await UpsertEdgeAsync(edge, ct);
    }

    /// <inheritdoc/>
    public async Task UpsertNodeAsync(WorkflowHistoryNode node, CancellationToken ct = default)
    {
        var entity = await _db.HistoryNodes.FindAsync([node.Fingerprint], ct);
        if (entity is null)
        {
            entity = new HistoryNodeRow
            {
                Fingerprint = node.Fingerprint,
                Name = node.Name,
                Kind = node.Kind.ToString(),
                Target = node.Target,
                ExecutionCount = node.ExecutionCount,
                SuccessCount = node.SuccessCount,
                FailureCount = node.FailureCount,
                AverageDurationTicks = node.AverageDuration.Ticks,
                FirstSeenAt = node.FirstSeenAt,
                LastSeenAt = node.LastSeenAt,
                WorkflowNamesJson = JsonSerializer.Serialize(node.WorkflowNames, JsonOptions),
            };
            _db.HistoryNodes.Add(entity);
        }
        else
        {
            entity.ExecutionCount = node.ExecutionCount;
            entity.SuccessCount = node.SuccessCount;
            entity.FailureCount = node.FailureCount;
            entity.AverageDurationTicks = node.AverageDuration.Ticks;
            entity.LastSeenAt = node.LastSeenAt;
            entity.WorkflowNamesJson = JsonSerializer.Serialize(node.WorkflowNames, JsonOptions);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UpsertEdgeAsync(WorkflowHistoryEdge edge, CancellationToken ct = default)
    {
        var kindStr = edge.Kind.ToString();
        var entity = await _db.HistoryEdges
            .FirstOrDefaultAsync(e =>
                e.SourceFingerprint == edge.SourceFingerprint &&
                e.TargetFingerprint == edge.TargetFingerprint &&
                e.Kind == kindStr, ct);

        if (entity is null)
        {
            entity = new HistoryEdgeRow
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceFingerprint = edge.SourceFingerprint,
                TargetFingerprint = edge.TargetFingerprint,
                Kind = kindStr,
                Weight = edge.Weight,
                AverageTransitionTimeTicks = edge.AverageTransitionTime.Ticks,
                FirstSeenAt = edge.FirstSeenAt,
                LastSeenAt = edge.LastSeenAt,
                WorkflowNamesJson = JsonSerializer.Serialize(edge.WorkflowNames, JsonOptions),
            };
            _db.HistoryEdges.Add(entity);
        }
        else
        {
            entity.Weight = edge.Weight;
            entity.AverageTransitionTimeTicks = edge.AverageTransitionTime.Ticks;
            entity.LastSeenAt = edge.LastSeenAt;
            entity.WorkflowNamesJson = JsonSerializer.Serialize(edge.WorkflowNames, JsonOptions);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<WorkflowHistoryNode?> GetNodeAsync(string fingerprint, CancellationToken ct = default)
    {
        var entity = await _db.HistoryNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Fingerprint == fingerprint, ct);
        return entity is null ? null : RowToNode(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowHistoryNode>> SearchNodesAsync(
        string query, int limit = 20, CancellationToken ct = default)
    {
        var entities = await _db.HistoryNodes
            .AsNoTracking()
            .Where(n => n.Name.Contains(query))
            .OrderByDescending(n => n.LastSeenAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(RowToNode).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowHistoryEdge>> GetTopEdgesFromAsync(
        string fingerprint, int topN = 10, CancellationToken ct = default)
    {
        var entities = await _db.HistoryEdges
            .AsNoTracking()
            .Where(e => e.SourceFingerprint == fingerprint)
            .OrderByDescending(e => e.Weight)
            .Take(topN)
            .ToListAsync(ct);

        return entities.Select(RowToEdge).OfType<WorkflowHistoryEdge>().ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowHistoryEdge>> GetEdgesForWorkflowAsync(
        string workflowName, CancellationToken ct = default)
    {
        var searchTerm = $"\"{workflowName}\"";
        var entities = await _db.HistoryEdges
            .AsNoTracking()
            .Where(e => e.WorkflowNamesJson.Contains(searchTerm))
            .ToListAsync(ct);

        return entities.Select(RowToEdge).OfType<WorkflowHistoryEdge>().ToList();
    }

    private static WorkflowHistoryNode RowToNode(HistoryNodeRow entity)
    {
        var workflowNames = TryDeserializeSet(entity.WorkflowNamesJson);
        _ = Enum.TryParse<AgentStepKind>(entity.Kind, out var kind);
        return new WorkflowHistoryNode
        {
            Fingerprint = entity.Fingerprint,
            Name = entity.Name,
            Kind = kind,
            Target = entity.Target,
            ExecutionCount = entity.ExecutionCount,
            SuccessCount = entity.SuccessCount,
            FailureCount = entity.FailureCount,
            AverageDuration = TimeSpan.FromTicks(entity.AverageDurationTicks),
            FirstSeenAt = entity.FirstSeenAt,
            LastSeenAt = entity.LastSeenAt,
            WorkflowNames = workflowNames,
        };
    }

    private static WorkflowHistoryEdge? RowToEdge(HistoryEdgeRow entity)
    {
        if (!Enum.TryParse<EdgeKind>(entity.Kind, out var kind))
            return null;
        var workflowNames = TryDeserializeSet(entity.WorkflowNamesJson);
        return new WorkflowHistoryEdge
        {
            SourceFingerprint = entity.SourceFingerprint,
            TargetFingerprint = entity.TargetFingerprint,
            Kind = kind,
            Weight = entity.Weight,
            AverageTransitionTime = TimeSpan.FromTicks(entity.AverageTransitionTimeTicks),
            FirstSeenAt = entity.FirstSeenAt,
            LastSeenAt = entity.LastSeenAt,
            WorkflowNames = workflowNames,
        };
    }

    private static HashSet<string> TryDeserializeSet(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return list is not null
                ? new HashSet<string>(list, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
