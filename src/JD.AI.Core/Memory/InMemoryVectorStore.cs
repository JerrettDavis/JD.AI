using System.Collections.Concurrent;
using System.Numerics;

namespace JD.AI.Core.Memory;

/// <summary>
/// In-memory vector store for testing and lightweight use cases.
/// Uses brute-force cosine similarity (O(n) per query).
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, MemoryEntry> _entries = new();

    /// <summary>Number of stored entries.</summary>
    public int Count => _entries.Count;

    public Task UpsertAsync(IReadOnlyList<MemoryEntry> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
            _entries[entry.Id] = entry;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        float[] queryEmbedding, int topK = 5, string? categoryFilter = null,
        CancellationToken ct = default)
    {
        var results = _entries.Values
            .Where(e => e.Embedding is not null)
            .Where(e => categoryFilter is null ||
                        string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
            .Select(e => new MemorySearchResult(e, CosineSimilarity(queryEmbedding, e.Embedding!)))
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<MemorySearchResult>>(results);
    }

    public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        foreach (var id in ids)
            _entries.TryRemove(id, out _);

        return Task.CompletedTask;
    }

    public Task<long> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult((long)_entries.Count);
    }

    /// <summary>
    /// Computes cosine similarity between two vectors using SIMD-optimized operations
    /// when available.
    /// </summary>
    internal static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimensionality", nameof(b));

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        // Use SIMD for inner loop when vectors are large enough
        var i = 0;
        var simdLength = Vector<float>.Count;

        if (a.Length >= simdLength)
        {
            var dotVec = Vector<float>.Zero;
            var normAVec = Vector<float>.Zero;
            var normBVec = Vector<float>.Zero;

            for (; i <= a.Length - simdLength; i += simdLength)
            {
                var va = new Vector<float>(a, i);
                var vb = new Vector<float>(b, i);
                dotVec += va * vb;
                normAVec += va * va;
                normBVec += vb * vb;
            }

            for (var j = 0; j < simdLength; j++)
            {
                dotProduct += dotVec[j];
                normA += normAVec[j];
                normB += normBVec[j];
            }
        }

        // Scalar remainder
        for (; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dotProduct / denom;
    }
}
