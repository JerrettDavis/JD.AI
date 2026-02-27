using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a single AI backend and builds kernels for its models.
/// </summary>
public interface IProviderDetector
{
    string ProviderName { get; }

    Task<ProviderInfo> DetectAsync(CancellationToken ct = default);

    Kernel BuildKernel(ProviderModelInfo model);
}
