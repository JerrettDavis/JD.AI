namespace JD.AI.Dashboard.Wasm.Models;

public record MemorySearchRequest
{
    public string Query { get; init; } = "";
    public int TopK { get; init; } = 10;
}

public record MemorySearchResult
{
    public string Id { get; init; } = "";
    public string Content { get; init; } = "";
    public double Score { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public record MemoryIndexRequest
{
    public string Content { get; init; } = "";
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public record MemoryIndexResponse
{
    public string Id { get; init; } = "";
    public bool Success { get; init; }
}

public record MemoryStats
{
    public int DocumentCount { get; init; }
}
