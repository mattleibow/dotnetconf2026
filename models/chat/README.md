# Chat & Tool Model (`Qwen3-0.6B`)

A small INT4 ONNX GenAI model used on Windows for tool-calling chat requests via `Microsoft.ML.OnnxRuntimeGenAI` (`0.20.0`).

```text
models/chat/
  PrepareModel.py
  requirements.txt
  CheckModel.cs
  Qwen3-0.6B/          # generated locally by PrepareModel.py (gitignored)
    genai_config.json
    model.onnx
    model.onnx.data
    tokenizer.json
    tokenizer_config.json
```

- **Source:** [Qwen/Qwen3-0.6B](https://huggingface.co/Qwen/Qwen3-0.6B), pinned to revision `c1899de289a04d12100db370d81485cdf75e47ca`.
- **License:** Apache 2.0.
- **Size:** ~396 MiB (CPU INT4 RTN, block size 32), intentionally gitignored and exported locally.
- **Package:** `Microsoft.ML.OnnxRuntimeGenAI` (`0.20.0`).

## Exporting the model

Before building the Windows app or running the smoke test, export the model once from the repository root with Python 3.13:

```sh
python3 -m venv models/chat/.tool-model-env
models/chat/.tool-model-env/bin/python -m pip install -r models/chat/requirements.txt
models/chat/.tool-model-env/bin/python models/chat/PrepareModel.py
```

On Windows, use `models\chat\.tool-model-env\Scripts\python.exe` instead. No Hugging Face login or runtime network access is required.

## Smoke-testing the model

Run `dotnet run models/chat/CheckModel.cs` from the repository root to verify tool calling (`<tool_call>`) and schema-guided JSON output (`SetGuidance("json_schema", ...)`).

If a macOS host loads an older ONNX Runtime from another app on `PATH`, run with:

```sh
DYLD_LIBRARY_PATH="$HOME/.nuget/packages/microsoft.ml.onnxruntime/1.32.0/runtimes/osx-arm64/native" dotnet run models/chat/CheckModel.cs
```

## Using the model in the Windows app

On Windows, requests with a nonempty `ChatOptions.Tools` list route to `QwenChatClient` (wrapped with its own `UseFunctionInvocation` middleware so tool follow-up turns stay on Qwen), while tool-free requests such as plain chat, JSON-schema enrichment, and workflow analysts use `WindowsAIChatClient`. iOS, Mac Catalyst, and Android use their platform chat clients for all requests and do not bundle `Qwen3-0.6B`.
