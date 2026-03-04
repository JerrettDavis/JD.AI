namespace JD.AI.Core.Agents;

public sealed class ForkPoint
{
    public int Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public int TurnIndex { get; init; }
    public int MessageCount { get; init; }
}
