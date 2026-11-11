---
description: Swap in the bundled ONNX embedding model for semantic search
---

Apple's built-in sentence embeddings aren't strong enough for retrieval. Swap the `IEmbeddingGenerator<string, Embedding<float>>` to use the small ONNX model in `models/` (see `models/README.md`; `models/DownloadModels.cs` shows the tokenization, mean pooling, and normalization).

- Implement `IEmbeddingGenerator<string, Embedding<float>>` with `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`, linking the files in `models/all-MiniLM-L6-v2/` as raw `MauiAsset`s.
- Register it as the `IEmbeddingGenerator<string, Embedding<float>>` on every platform.
- Nothing in the ingestion pipeline, vector store writer, or search UI should change apart from the vector dimension (`384` for MiniLM instead of `512`).

Use DevFlow on Mac Catalyst in Durban to try the exact same searches ("football forecasts" and "gradient descent") and verify that the sports predictor and mathematical optimization talks now rank first.
