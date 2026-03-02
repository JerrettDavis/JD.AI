using JD.AI.Core.Memory;

namespace JD.AI.Gateway.Endpoints;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/memory").WithTags("Memory");

        // POST /api/memory/index — index a document
        group.MapPost("/index", async (IndexRequest req, IVectorStore store) =>
        {
            var entry = new MemoryEntry
            {
                Id = req.Id,
                Content = req.Text,
                Embedding = req.Embedding,
                Source = req.Source,
                Category = req.Category,
                Metadata = req.Metadata is not null ? new Dictionary<string, string>(req.Metadata) : new Dictionary<string, string>()
            };
            await store.UpsertAsync([entry]);
            return Results.Ok(new { req.Id, Status = "indexed" });
        });

        // POST /api/memory/search — semantic search
        group.MapPost("/search", async (SearchRequest req, IVectorStore store) =>
        {
            var results = await store.SearchAsync(req.Embedding, req.TopK, req.CategoryFilter);
            return Results.Ok(results);
        });

        // DELETE /api/memory/{id} — delete a document
        group.MapDelete("/{id}", async (string id, IVectorStore store) =>
        {
            await store.DeleteAsync([id]);
            return Results.NoContent();
        });
    }
}

public record IndexRequest(
    string Id,
    string Text,
    float[] Embedding,
    string? Source = null,
    string? Category = null,
    IDictionary<string, string>? Metadata = null);

public record SearchRequest(
    float[] Embedding,
    int TopK = 5,
    string? CategoryFilter = null);
