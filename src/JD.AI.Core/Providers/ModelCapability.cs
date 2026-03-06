namespace JD.AI.Core.Providers;

/// <summary>
/// Capability flags for model-level routing decisions.
/// </summary>
[Flags]
public enum ModelCapability
{
    None = 0,
    ChatCompletion = 1 << 0,
    Streaming = 1 << 1,
    ToolCalling = 1 << 2,
    JsonMode = 1 << 3,
    Vision = 1 << 4,
    Embeddings = 1 << 5,
    AudioInput = 1 << 6,
    AudioOutput = 1 << 7,
}
