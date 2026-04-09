using Microsoft.EntityFrameworkCore;

namespace JD.AI.Workflows.History;

/// <summary>
/// Minimal EF Core DbContext for persisting workflow history graph nodes and edges.
/// Mirrors the HistoryNodeEntity/HistoryEdgeEntity tables in the WorkflowFramework
/// dashboard database (SQLite) without a hard dependency on the Dashboard.Persistence project.
/// </summary>
public sealed class WorkflowHistoryDbContext : DbContext
{
    public WorkflowHistoryDbContext(DbContextOptions<WorkflowHistoryDbContext> options) : base(options) { }

    public DbSet<HistoryNodeRow> HistoryNodes => Set<HistoryNodeRow>();
    public DbSet<HistoryEdgeRow> HistoryEdges => Set<HistoryEdgeRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoryNodeRow>(e =>
        {
            e.ToTable("HistoryNodes");
            e.HasKey(n => n.Fingerprint);
            e.Property(n => n.Fingerprint).HasMaxLength(16);
            e.Property(n => n.Name).HasMaxLength(256);
            e.Property(n => n.Kind).HasMaxLength(32);
            e.Property(n => n.Target).HasMaxLength(512);
            e.HasIndex(n => n.Name);
            e.HasIndex(n => n.LastSeenAt);
        });

        modelBuilder.Entity<HistoryEdgeRow>(e =>
        {
            e.ToTable("HistoryEdges");
            e.HasKey(edge => edge.Id);
            e.HasIndex(edge => new { edge.SourceFingerprint, edge.TargetFingerprint, edge.Kind }).IsUnique();
            e.Property(edge => edge.SourceFingerprint).HasMaxLength(16);
            e.Property(edge => edge.TargetFingerprint).HasMaxLength(16);
            e.Property(edge => edge.Kind).HasMaxLength(32);
        });
    }
}

/// <summary>Row type for HistoryNodeEntity table. Mirrors WorkflowFramework.Dashboard.Persistence.Entities.HistoryNodeEntity.</summary>
public sealed class HistoryNodeRow
{
    public string Fingerprint { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Target { get; set; }
    public long ExecutionCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public long AverageDurationTicks { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string WorkflowNamesJson { get; set; } = "[]";
}

/// <summary>Row type for HistoryEdgeEntity table. Mirrors WorkflowFramework.Dashboard.Persistence.Entities.HistoryEdgeEntity.</summary>
public sealed class HistoryEdgeRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceFingerprint { get; set; } = string.Empty;
    public string TargetFingerprint { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public long Weight { get; set; }
    public long AverageTransitionTimeTicks { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string WorkflowNamesJson { get; set; } = "[]";
}
