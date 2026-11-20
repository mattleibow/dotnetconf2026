# Models

On-device models and preparation scripts grouped by capability under `models/embedding/` and `models/chat/`.

```text
models/
  embedding/
    PrepareModel.cs
    all-MiniLM-L6-v2/
      model.onnx
      vocab.txt
  chat/
    PrepareModel.py
    requirements.txt
    CheckModel.cs
    Qwen3-0.6B/          # generated locally by PrepareModel.py (gitignored)
      model.onnx
      model.onnx.data
      ...
```

## Embedding: `all-MiniLM-L6-v2`

A small sentence-embedding model used for on-device semantic search.

```text
models/embedding/all-MiniLM-L6-v2/
  model.onnx    # int8-quantized ONNX model, ~22 MB, any CPU
  vocab.txt     # WordPiece vocabulary for Microsoft.ML.Tokenizers' BertTokenizer
```

- **Source:** [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2), pinned to revision `1110a243`.
- **License:** Apache 2.0.
- **Output:** 384-dimension vectors. Run the model, average the token vectors (mean pooling), then normalize. Inputs are `input_ids`, `attention_mask` and `token_type_ids`; the output is `last_hidden_state`.
- **Packages:** `Microsoft.ML.OnnxRuntime` (`1.32.0`) and `Microsoft.ML.Tokenizers` (`3.0.0`).

### Refreshing and smoke-testing

From the repository root, with the .NET 11 SDK:

```sh
dotnet run models/embedding/PrepareModel.cs
```

It downloads the files, checks their SHA-256 hashes, and runs an embedding similarity smoke test.

### Using the embedding model in a .NET MAUI app

- Bundle the folder as raw assets, for example `<MauiAsset Include="..\..\models\embedding\all-MiniLM-L6-v2\**" LogicalName="models/embedding/all-MiniLM-L6-v2/%(Filename)%(Extension)" />`.
- On iOS, ONNX Runtime is linked statically and needs two extra linker settings:

  ```xml
  <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
    <ReferenceNativeSymbol Include="RegisterCustomOps" SymbolType="Function" SymbolMode="Ignore" />
    <LinkerArgument Include="-lsqlite3" />
  </ItemGroup>
  ```

## Chat & Tools: `Qwen3-0.6B` (Windows)

On Windows, requests with a nonempty `ChatOptions.Tools` list use Qwen3 through
`Microsoft.ML.OnnxRuntimeGenAI` (`0.20.0`). Other requests, including JSON-schema
enrichment and workflow analysts, use Windows AI. The ONNX branch has its own
`UseFunctionInvocation` middleware, so tool results and the final answer stay on
ONNX even when that middleware disables tools on its final iteration.

iOS, Mac Catalyst, and Android use their existing platform clients for all
requests; they do not bundle or require the Qwen model.

The CPU INT4 bundle is about 396 MiB, Apache-2.0, exported from
[Qwen/Qwen3-0.6B](https://huggingface.co/Qwen/Qwen3-0.6B) at revision
`c1899de289a04d12100db370d81485cdf75e47ca`. It is intentionally not committed.

### Exporting the model

Before building the Windows app or running the chat model smoke test, export it
once from the repository root with Python 3.13:

```sh
python3 -m venv models/chat/.tool-model-env
models/chat/.tool-model-env/bin/python -m pip install -r models/chat/requirements.txt
models/chat/.tool-model-env/bin/python models/chat/PrepareModel.py
```

On Windows, use `models\chat\.tool-model-env\Scripts\python.exe` instead.
No Hugging Face login or runtime network access is required. The app copies the
bundle to app data on first tool use; allow another approximately 400 MiB for
that copy. The native weights are loaded lazily and shared across instances;
inference is serialized and runs off the UI thread. Tool responses are buffered
to avoid streaming partial JSON or raw call markup into the chat UI.

### Smoke-testing the chat model

Run `dotnet run models/chat/CheckModel.cs` from the repository root for a
desktop smoke test of tool calling (`<tool_call>`) and schema-guided JSON output
(`SetGuidance("json_schema", ...)`). If a macOS host loads an older ONNX Runtime
from another app on the path, run the check with:

```sh
DYLD_LIBRARY_PATH="$HOME/.nuget/packages/microsoft.ml.onnxruntime/1.32.0/runtimes/osx-arm64/native" dotnet run models/chat/CheckModel.cs
```

**Experimental:** Small models can invent answers, miss actions, or take an
unrequested action. The app's `QwenChatClient` rejects unknown tools,
incomplete/malformed calls, missing arguments, and non-string arguments before
executing any calls from that response, and rejects JSON response formats so
tool-free structured requests stay on Windows AI.

