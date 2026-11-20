# Embedding Model (`all-MiniLM-L6-v2`)

A small sentence-embedding model used for on-device semantic search across all platforms.

```text
models/embedding/
  PrepareModel.cs
  all-MiniLM-L6-v2/
    model.onnx    # int8-quantized ONNX model, ~22 MB, any CPU
    vocab.txt     # WordPiece vocabulary for Microsoft.ML.Tokenizers' BertTokenizer
```

- **Source:** [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2), pinned to revision `1110a243`.
- **License:** Apache 2.0.
- **Output:** 384-dimension vectors. Run the model, average the token vectors (mean pooling), then L2-normalize. Inputs are `input_ids`, `attention_mask`, and `token_type_ids`; the output is `last_hidden_state`.
- **Packages:** `Microsoft.ML.OnnxRuntime` (`1.32.0`) and `Microsoft.ML.Tokenizers` (`3.0.0`).

## Refreshing and smoke-testing

From the repository root, with the .NET 11 SDK:

```sh
dotnet run models/embedding/PrepareModel.cs
```

It downloads the files, verifies their SHA-256 hashes, and runs a cosine-similarity smoke test.

## Using the model in a .NET MAUI app

- Bundle the folder as raw assets:

  ```xml
  <MauiAsset Include="..\..\models\embedding\all-MiniLM-L6-v2\**" LogicalName="models/embedding/all-MiniLM-L6-v2/%(Filename)%(Extension)" />
  ```

- On iOS, ONNX Runtime is linked statically and needs two extra linker settings:

  ```xml
  <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
    <ReferenceNativeSymbol Include="RegisterCustomOps" SymbolType="Function" SymbolMode="Ignore" />
    <LinkerArgument Include="-lsqlite3" />
  </ItemGroup>
  ```
