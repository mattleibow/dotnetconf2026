---
description: Upgrade SearchService to semantic search with Apple's built-in embeddings
---

`SearchService` currently only checks `Contains` on session titles and speaker names. Upgrade it to understand meaning, starting with Apple's built-in sentence embedding model.

- Add `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI` behind `IEmbeddingGenerator<string, Embedding<float>>` (suppress `MAUIAI0001`).
- Add a `VectorStoreWriter` to our existing data ingestion pipeline so each session chunk (title plus abstract) is written into an in-memory vector store from `CommunityToolkit.VectorData.InMemory` using the registered `IEmbeddingGenerator`.
- Update `SearchService.SearchSessionsAsync(string cityId, string query)` so it puts exact title/speaker matches first, followed by the top vector matches for that city, and logs `[AI Search]` top match results via `ILogger`.
- Apple's built-in sentence model is small, so expect rough semantic rankings on queries that don't share words with the title; the next step swaps in a stronger model to compare.

Docs:
- [Microsoft.Maui.Essentials.AI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Vector stores in .NET](https://learn.microsoft.com/dotnet/ai/vector-stores/overview)

Use DevFlow on Mac Catalyst in Durban to search for "football forecasts" and "gradient descent", inspect `maui devflow logs --limit 20 --no-json`, and note where the sports predictor and mathematical optimization talks rank.
