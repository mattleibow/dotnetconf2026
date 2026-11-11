---
description: Bring semantic search to the other platforms with ONNX
---

Bring semantic search to Android and Windows with the small ONNX model in `models/` (see `models/README.md`; `models/DownloadModels.cs` shows the embedding code).

- Write an `IEmbeddingGenerator<string, Embedding<float>>` using `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`, and bundle the model files as raw assets.
- Use it everywhere except Apple, and add a constant in `MauiProgram` to switch Apple to it too, so I can compare the two.
- Nothing else in search should need to change.

Use DevFlow on Mac Catalyst with the constant switched both ways to try "saving money on AI" and "hardware projects" in Durban, and "making games" in Cape Town. Compare the two models' top results in a small table, and tell me which generator actually ran. Build Android too.
