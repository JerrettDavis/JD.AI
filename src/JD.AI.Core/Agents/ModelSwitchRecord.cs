namespace JD.AI.Core.Agents;

public sealed record ModelSwitchRecord(
    DateTimeOffset Timestamp,
    string ModelId,
    string ProviderName,
    string SwitchMode); // "preserve", "compact", "transform", "fresh"
