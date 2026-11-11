---
description: Bring semantic search to the other platforms with ONNX
---

Bring semantic search to Android and Windows with the small ONNX model in `models/` (see `models/README.md`; `models/DownloadModels.cs` shows the embedding code).

- Write an `IEmbeddingGenerator<string, Embedding<float>>` using `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`, and bundle the model files as raw assets.
- Use it everywhere except Apple, and add a constant in `MauiProgram` to switch Apple to it too, so I can compare the two.
- Nothing else in search should need to change.

Use DevFlow on Android, and on Apple with the constant switched, to try "saving money on AI" and "hardware projects". Tell me which generator actually ran.
