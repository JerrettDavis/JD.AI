namespace JD.AI.Gateway.Endpoints;

/// <summary>
/// Standard paginated response envelope for list endpoints.
/// Uses cursor-based pagination with opaque <see cref="Cursor"/> tokens.
/// </summary>
public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    string? Cursor,
    bool HasMore);

/// <summary>
/// Helpers for building cursor-based paginated responses.
/// Cursors are base64-encoded offsets for simplicity and forward compatibility.
/// </summary>
public static class PaginationHelper
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static int ClampLimit(int? requested) =>
        Math.Clamp(requested ?? DefaultLimit, 1, MaxLimit);

    public static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return 0;
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            return int.TryParse(text, out var offset) ? Math.Max(0, offset) : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static string? EncodeCursor(int offset) =>
        offset > 0
            ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()))
            : null;

    public static PaginatedResult<T> Paginate<T>(
        IReadOnlyList<T> allItems,
        int? limit,
        string? cursor)
    {
        var offset = DecodeCursor(cursor);
        var take = ClampLimit(limit);
        var page = allItems.Skip(offset).Take(take).ToList();
        var hasMore = offset + take < allItems.Count;
        var nextCursor = hasMore ? EncodeCursor(offset + take) : null;

        return new PaginatedResult<T>(page, allItems.Count, nextCursor, hasMore);
    }
}
