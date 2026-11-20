# Models

On-device models and preparation scripts grouped by capability:

- **[`models/embedding/`](embedding/README.md)** — `all-MiniLM-L6-v2` sentence-embedding model (~22 MB INT8 ONNX) used for semantic search across all platforms (`dotnet run models/embedding/PrepareModel.cs`).
- **[`models/chat/`](chat/README.md)** — `Qwen3-0.6B` chat & tool-calling model (~396 MiB INT4 ONNX GenAI, exported locally via `models/chat/PrepareModel.py` and tested with `dotnet run models/chat/CheckModel.cs`) used for Windows tool calling.


