using System.Runtime.InteropServices;

namespace JD.AI.Core.LocalModels;

/// <summary>
/// Detects available GPU backends for LLamaSharp inference at runtime.
/// </summary>
public static class GpuDetector
{
    private static GpuBackend? _cached;

    /// <summary>
    /// Detects the best available GPU backend.
    /// Order: CUDA → Metal (macOS) → CPU fallback.
    /// </summary>
    public static GpuBackend Detect()
    {
        if (_cached.HasValue) return _cached.Value;

        _cached = DetectInternal();
        return _cached.Value;
    }

    /// <summary>
    /// Resets the cached detection result (for testing).
    /// </summary>
    public static void Reset() => _cached = null;

    /// <summary>
    /// Returns a recommended GPU layer count based on the backend and model size.
    /// Returns -1 for "all layers" when GPU is available.
    /// </summary>
    public static int RecommendGpuLayers(GpuBackend backend, long modelSizeBytes)
    {
        if (backend == GpuBackend.Cpu) return 0;

        // For GPU backends, offload all layers by default.
        // Users can override via LocalModelOptions.GpuLayers.
        return -1;
    }

    private static GpuBackend DetectInternal()
    {
        // Check for CUDA (NVIDIA)
        if (TryLoadNativeLib("nvcuda") || TryLoadNativeLib("cudart64_12"))
        {
            return GpuBackend.Cuda;
        }

        // On macOS, Metal is always available
        if (OperatingSystem.IsMacOS())
        {
            return GpuBackend.Metal;
        }

        return GpuBackend.Cpu;
    }

    private static bool TryLoadNativeLib(string name)
    {
        try
        {
            return NativeLibrary.TryLoad(name, out _);
        }
        catch
        {
            return false;
        }
    }
}
