using Microsoft.Data.Sqlite;

namespace JD.AI.Core.Memory;

/// <summary>
/// SQLite-based vector store using manual cosine similarity.
/// For production, consider sqlite-vec extension or a dedicated vector DB.
/// </summary>
public sealed class SqliteVectorStore : IVectorStore, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteVectorStore(string? dbPath = null)
    {
        dbPath ??= Config.DataDirectories.VectorsDb;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS memory_entries (
                id TEXT PRIMARY KEY,
                content TEXT NOT NULL,
                source TEXT,
                category TEXT,
                embedding BLOB,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                metadata TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_memory_category ON memory_entries(category);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task UpsertAsync(IReadOnlyList<MemoryEntry> entries, CancellationToken ct = default)
    {
        using var tx = _connection.BeginTransaction();
        foreach (var entry in entries)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO memory_entries (id, content, source, category, embedding, created_at, metadata)
                VALUES ($id, $content, $source, $category, $embedding, $created, $metadata)
                """;
            cmd.Parameters.AddWithValue("$id", entry.Id);
            cmd.Parameters.AddWithValue("$content", entry.Content);
            cmd.Parameters.AddWithValue("$source", (object?)entry.Source ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$category", (object?)entry.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$embedding",
                entry.Embedding is not null ? SerializeEmbedding(entry.Embedding) : DBNull.Value);
            cmd.Parameters.AddWithValue("$created", entry.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$metadata",
                System.Text.Json.JsonSerializer.Serialize(entry.Metadata));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private const string SearchAllSql =
        "SELECT id, content, source, category, embedding, created_at FROM memory_entries WHERE embedding IS NOT NULL";

    private const string SearchByCategorySql =
        "SELECT id, content, source, category, embedding, created_at FROM memory_entries WHERE category = $cat AND embedding IS NOT NULL";

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        float[] queryEmbedding, int topK = 5, string? categoryFilter = null,
        CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        if (categoryFilter is not null)
        {
            cmd.CommandText = SearchByCategorySql;
            cmd.Parameters.AddWithValue("$cat", categoryFilter);
        }
        else
        {
            cmd.CommandText = SearchAllSql;
        }

        var results = new List<(MemoryEntry Entry, double Score)>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var embedding = DeserializeEmbedding((byte[])reader["embedding"]);
            var score = CosineSimilarity(queryEmbedding, embedding);

            results.Add((new MemoryEntry
            {
                Id = reader.GetString(0),
                Content = reader.GetString(1),
                Source = reader.IsDBNull(2) ? null : reader.GetString(2),
                Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                Embedding = embedding,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(5))
            }, score));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select(r => new MemorySearchResult(r.Entry, r.Score))
            .ToList();
    }

    public async Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        foreach (var id in ids)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM memory_entries WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM memory_entries";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : 0;
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }

    public void Dispose() => _connection.Dispose();
}
