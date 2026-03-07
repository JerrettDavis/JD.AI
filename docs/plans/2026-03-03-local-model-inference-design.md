# Local Model Inference via LLamaSharp — Design

**Date:** 2026-03-03
**Status:** Approved
**Branch:** `feat/local-model-inference`

## Problem

JD.AI currently requires external services (Claude Code, GitHub Copilot, Codex, Ollama) for inference. Users who need fully standalone, offline-capable, or privacy-sensitive operation have no option. Adding native local model support makes JD.AI truly self-contained.

## Approach

Use **LLamaSharp** (C# bindings for llama.cpp) for in-process GGUF model inference. Models discoverable from local files, directory scans, HuggingFace Hub, and remote URLs. Configurable model cache at `~/.jdai/models/` (respects `HF_HOME` and custom paths).

## Architecture

### Components

- **`LocalModelDetector`** — `IProviderDetector` implementation. Probes the model registry on startup, returns available models under provider name "Local".

- **`LocalModelRegistry`** — Tracks all known models in `~/.jdai/models/registry.json`. Maps model IDs → file paths, metadata (size, quantization, capabilities), and source info. Deduplicates by file content hash.

- **`ModelSourceManager`** — Pluggable source system:
  - `IModelSource` — Interface: `ScanAsync()`, `DownloadAsync()`
  - `FileModelSource` — Single `.gguf` file path
  - `DirectoryModelSource` — Recursive folder scan for `.gguf` files
  - `HuggingFaceModelSource` — HF Hub API search/filter/download
  - `RemoteUrlModelSource` — HTTP(S) download with resume

- **`LlamaInferenceEngine`** — Wraps LLamaSharp to implement SK's `IChatCompletionService`. Manages model loading/unloading, GPU layer offloading, context window config. One model active at a time.

- **`GpuDetector`** — Runtime probe: CUDA → Vulkan → Metal → CPU fallback. Informs GPU layer offloading decisions.

- **`ModelDownloader`** — Shared download logic with progress reporting, resume support, cancellation.

### Data Flow

1. **Startup:** `LocalModelDetector.DetectAsync()` → loads registry → scans directories → merges → returns `ProviderInfo`
2. **Selection:** User picks model → `BuildKernel()` → `LlamaInferenceEngine` loads GGUF
3. **Inference:** SK pipeline → `IChatCompletionService` → LLamaSharp → streaming tokens
4. **Acquisition:** `/models search|download|add|list|remove` TUI commands

### File Structure

```
src/JD.AI.Core/LocalModels/
  LocalModelDetector.cs
  LocalModelRegistry.cs
  LlamaInferenceEngine.cs
  ModelMetadata.cs
  GpuDetector.cs
  ModelDownloader.cs
  Sources/
    IModelSource.cs
    FileModelSource.cs
    DirectoryModelSource.cs
    HuggingFaceModelSource.cs
    RemoteUrlModelSource.cs
src/JD.AI/Commands/
  ModelCommands.cs
```

### Dependencies

- `LLamaSharp` (v0.19+) — Core bindings
- `LLamaSharp.Backend.Cpu` — CPU fallback (always)
- `LLamaSharp.Backend.Cuda12` — NVIDIA GPU (optional)

### GPU Strategy

Auto-detect at startup. Offload all layers to GPU when available, configurable via `LocalModelOptions.GpuLayers`. Falls back gracefully on OOM.

### Error Handling

- No models: `IsAvailable: false` with guidance message
- GPU OOM: Fallback to CPU with warning
- Corrupt GGUF: Validate header magic, report specific error
- Download failure: Resume-capable, 3x retry, partial cleanup
- HF API errors: Cache results, suggest `HF_TOKEN`

### TUI Commands

- `/models search <query>` — Search HuggingFace GGUF models
- `/models download <repo-id>` — Download with progress
- `/models add <path>` — Register local file/directory
- `/models list` — Show registered models
- `/models remove <id>` — Unregister (optionally delete)

### Testing

**Unit (no model required):** Registry CRUD, metadata parsing, directory scan, HF API mocking, GPU detection, download logic, detector with mocked registry.

**Integration (requires small model):** TinyLlama-1.1B-GGUF round-trip inference, `/models` commands E2E. Marked `Category=Integration`.
