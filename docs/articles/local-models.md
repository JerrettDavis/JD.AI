---
description: "Run AI models locally via LLamaSharp — fully offline, private, and standalone with no external service dependencies."
---

# Local Models

JD.AI can run GGUF language models directly in-process via [LLamaSharp](https://github.com/SciSharp/LLamaSharp), a C# binding for [llama.cpp](https://github.com/ggerganov/llama.cpp). This enables fully standalone operation with no external service dependencies — no Ollama, no cloud API keys, no internet connection.

## Overview

**What it does**: Loads GGUF model files into memory and runs inference locally using your CPU or GPU.

**When to use it**: Air-gapped environments, privacy-sensitive workloads, or when you want zero-dependency standalone operation.

**How it works**: JD.AI registers a `LocalModelDetector` that scans a configurable model directory for `.gguf` files. When you select a local model, `LlamaInferenceEngine` loads it via LLamaSharp and exposes it as a standard Semantic Kernel `IChatCompletionService` — all plugins, function calling, and streaming work transparently.

## Quick start

1. Download a GGUF model (example: TinyLlama for testing):

```bash
mkdir -p ~/.jdai/models
curl -L -o ~/.jdai/models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf \
  https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf
```

2. Launch JD.AI — the model is auto-detected:

```text
Detecting providers...
  ✓ Local: 1 model(s) [Cpu]
```

3. Select it with `/model tinyllama` or through the `/models` picker.

## Model sources

JD.AI discovers models from four sources:

| Source | Description | Example |
|---|---|---|
| **Local file** | A single `.gguf` file path | `/local add ~/my-model.gguf` |
| **Directory scan** | Recursive scan for `.gguf` files | Automatic on startup in `~/.jdai/models/` |
| **HuggingFace Hub** | Search and download from HF | `/local search llama 7b` |
| **Remote URL** | Direct HTTP(S) download with resume | `/local download <repo-id>` |

## Managing local models

Use the `/local` slash command family to manage your model library:

### List registered models

```text
/local list
```

Shows all models in the registry with their ID, display name, quantization, and file size.

### Add a model file or directory

```text
/local add /path/to/model.gguf
/local add /path/to/models-folder/
```

Registers the file (or recursively scans the directory) and persists the registry.

### Scan for models

```text
/local scan
/local scan /path/to/custom/directory
```

Scans the specified directory (or the default model directory) for `.gguf` files and merges them into the registry.

### Search HuggingFace

```text
/local search llama 7b
/local search mistral instruct
```

Queries the HuggingFace Hub API for GGUF-tagged model repositories. Results include repo ID and download count.

> [!TIP]
> Set the `HF_TOKEN` environment variable to authenticate with HuggingFace for higher rate limits and access to gated models.

### Download from HuggingFace

```text
/local download TheBloke/Mistral-7B-Instruct-v0.2-GGUF
```

Downloads the best-quality GGUF file (prefers Q4_K_M) from the specified HuggingFace repository. Downloads support resume — if interrupted, re-running the command continues where it left off.

### Remove a model

```text
/local remove <model-id>
```

Removes the model from the registry. The file on disk is not deleted.

## Model registry

JD.AI tracks all known models in a JSON manifest at `~/.jdai/models/registry.json`. The registry stores:

- Model ID and display name
- File path and size
- Quantization type (parsed from the filename)
- Parameter size (e.g., `7B`, `13B`)
- Source and origin URI
- Timestamp

On startup, the registry is loaded, stale entries pointing to missing files are pruned, the model directory is scanned for new files, and the registry is saved.

## GPU acceleration

LLamaSharp auto-detects available GPU hardware at startup:

| Backend | Detection method | Platforms |
|---|---|---|
| **CUDA** | Checks for `nvcuda` or `cudart64_12` native libraries | Windows, Linux |
| **Metal** | Always available on macOS via Apple Silicon | macOS |
| **CPU** | Fallback — always available | All |

The detected backend is shown in the provider status line:

```text
✓ Local: 3 model(s) [Cuda]
```

By default, all model layers are offloaded to the GPU when one is available. If GPU loading fails (e.g., insufficient VRAM), JD.AI falls back to CPU automatically with a warning.

> [!TIP]
> Only one local model is loaded at a time. Switching models via `/model` unloads the previous model and frees memory before loading the new one.

## Configuration

### Environment variables

| Variable | Description | Default |
|---|---|---|
| `JDAI_MODELS_DIR` | Model storage and registry directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory (for scanning HF-downloaded models) | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token for authenticated access | — |

### Inference options

The `LocalModelOptions` class controls inference parameters:

| Option | Type | Default | Description |
|---|---|---|---|
| `ContextSize` | `uint` | `4096` | Context window size in tokens |
| `GpuLayers` | `int` | `-1` | GPU layers to offload (`-1` = all, `0` = CPU only) |
| `MaxTokens` | `int` | `2048` | Maximum tokens per response |
| `Temperature` | `float` | `0.7` | Sampling temperature |
| `TopP` | `float` | `0.9` | Nucleus sampling threshold |

## Choosing a model

GGUF models vary widely in size, quality, and hardware requirements. Here is a rough guide:

| Category | Parameter size | RAM required | Use case |
|---|---|---|---|
| **Tiny** | 1–3B | 1–3 GB | Testing, quick iteration, low-resource machines |
| **Small** | 7–8B | 5–8 GB | General coding assistance, good quality/speed balance |
| **Medium** | 13–14B | 10–16 GB | Higher quality, needs decent hardware |
| **Large** | 30–70B | 24–64 GB | Best quality, requires high-end GPU or large RAM |

Quantization also affects quality and size. Common types from smallest to largest:

| Quantization | Quality | Size vs. F16 |
|---|---|---|
| Q2_K | Low | ~25% |
| Q4_K_M | Good (recommended) | ~40% |
| Q5_K_M | Very good | ~50% |
| Q6_K | Near-original | ~60% |
| Q8_0 | Excellent | ~75% |
| F16 | Original | 100% |

> [!TIP]
> **Q4_K_M** is the recommended default — it provides a good balance of quality and performance for most use cases.

## Architecture

```text
LocalModelDetector (IProviderDetector)
├── LocalModelRegistry (registry.json)
│   ├── DirectoryModelSource (scan ~/.jdai/models/)
│   ├── FileModelSource (individual files)
│   ├── HuggingFaceModelSource (HF Hub API)
│   └── RemoteUrlModelSource (HTTP download)
├── GpuDetector (CUDA → Metal → CPU)
├── LlamaInferenceEngine (IChatCompletionService)
│   ├── LLamaWeights (model loading)
│   ├── LLamaContext (inference context)
│   └── InteractiveExecutor (streaming tokens)
└── ModelDownloader (resume, retry, progress)
```

## Troubleshooting

### No local models detected

**Cause**: No `.gguf` files found in the model directory.

**Solution**: Add a model:
```text
/local add /path/to/model.gguf
/local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF
```

### Model fails to load (out of memory)

**Cause**: The model is too large for available RAM or VRAM.

**Solution**: Use a smaller model or lower quantization, or set `GpuLayers` to `0` to force CPU-only mode.

### Slow inference

**Cause**: Running on CPU without GPU offloading.

**Solution**: Install CUDA drivers for NVIDIA GPUs. Verify detection with the startup status line (`[Cuda]` vs `[Cpu]`). Use a smaller quantization (Q4_K_M instead of Q8_0).

### HuggingFace search returns no results

**Cause**: Rate-limited or network issue.

**Solution**: Set `HF_TOKEN` for authenticated access. Check network connectivity.

## See also

- [AI Providers](providers.md) — All supported providers
- [Commands Reference](commands-reference.md) — `/local` and `/models` commands
- [Configuration](configuration.md) — Environment variables and data directories
- [Extending JD.AI](extending.md) — Writing custom providers
