# Models

On-device models the app bundles, so the demo doesn't download anything at runtime.

## all-MiniLM-L6-v2

A small sentence-embedding model used for semantic search on platforms without a built-in embedding model (Apple uses its own `NLEmbedding` instead).

```text
all-MiniLM-L6-v2/
  model.onnx    # int8-quantized ONNX model, ~22 MB, any CPU
  vocab.txt     # WordPiece vocabulary for Microsoft.ML.Tokenizers' BertTokenizer
```

- **Source:** [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2), pinned to revision `1110a243`.
- **License:** Apache 2.0.
- **Output:** 384-dimension vectors. Run the model, average the token vectors (mean pooling), then normalize. Inputs are `input_ids`, `attention_mask` and `token_type_ids`; the output is `last_hidden_state`.
- **Packages:** `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`.

## Refreshing

From the repository root, with the .NET 11 SDK:

```sh
dotnet run models/DownloadModels.cs
```

It downloads the files, checks their SHA-256 hashes, and runs a quick test: "saving money on AI" must score closer to a talk on cost-efficient GenAI than to lunch. `DownloadModels.cs` is also a short working example of the embedding code the app needs.

## Using the model in a .NET MAUI app

- Bundle the folder as raw assets, for example `<MauiAsset Include="..\..\models\all-MiniLM-L6-v2\*" LogicalName="models/all-MiniLM-L6-v2/%(Filename)%(Extension)" />`.
- On iOS, ONNX Runtime is linked statically and needs two extra linker settings:

  ```xml
  <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
    <ReferenceNativeSymbol Include="RegisterCustomOps" SymbolType="Function" SymbolMode="Ignore" />
    <LinkerArgument Include="-lsqlite3" />
  </ItemGroup>
  ```
