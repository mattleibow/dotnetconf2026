---
description: Add on-device semantic search, with keyword search as the fallback
---

Add search to the agenda so people can find talks by meaning, not just exact words: "making my app faster" should find the performance talks.

Put it behind one small service, like `SearchSessionsAsync(query)`, that the UI and, later, the assistant can both use. Everything runs on the device.

- Keyword search works everywhere. It's the fallback, and it's better for exact speaker names.
- Embeddings go through `IEmbeddingGenerator<string, Embedding<float>>`, so search doesn't care which one is running:
  - On Apple, use `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI`.
  - Everywhere else, write a small ONNX generator with `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers` (`BertTokenizer`), using a bundled all-MiniLM-L6-v2 model (int8 ONNX plus `vocab.txt`).
  - Add a constant in `MauiProgram` so I can switch Apple to the ONNX one for comparison.
- Index the session Markdown with `Microsoft.Extensions.DataIngestion` (`MarkdownReader` from the `.Markdig` package, plus a chunker) and cache the index on disk, separately for each model.
- Try a couple of chunk sizes against a few real queries from this agenda before picking one. In Debug builds, show the matched chunk and score so I can see why something ranked.
- Show exact keyword matches first, then semantic ones, and only for the selected city.
- Show whether results are semantic or keyword, and never let search break the agenda.

Some of these packages are preview, so check the current docs first:

- [Microsoft.Maui.Essentials.AI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [ONNX Runtime C# API](https://onnxruntime.ai/docs/get-started/with-csharp.html) and [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
- [Data ingestion in .NET](https://learn.microsoft.com/dotnet/ai/conceptual/data-ingestion)

Use DevFlow to try an interest-based query, a speaker's name, and a query with no results. Tell me which embedding generator actually ran on each platform.
