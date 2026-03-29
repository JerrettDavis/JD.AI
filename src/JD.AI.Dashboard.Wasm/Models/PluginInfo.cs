namespace JD.AI.Dashboard.Wasm.Models;

public record PluginInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Author { get; init; } = "";
    public bool Enabled { get; init; }
}
