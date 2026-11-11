---
description: Make search understand meaning on Apple
---

Make search understand meaning, not just words: "making my app faster" should find the performance talks. Start with Apple.

- Use `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI`, through `IEmbeddingGenerator<string, Embedding<float>>`.
- At startup, split each abstract into two-sentence chunks that start with the session title, and add them to an in-memory vector store from `CommunityToolkit.VectorData.InMemory`. Give the store the embedding generator so it does the embedding for us.
- Search the store, keep each session's best chunk as the excerpt, and show exact keyword matches first.
- Other platforms keep keyword search for now. Show whether results are semantic or keyword.

These packages are new or preview, so check the docs first:

- [Microsoft.Maui.Essentials.AI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [CommunityToolkit AI](https://github.com/CommunityToolkit/AI)

Use DevFlow on Mac Catalyst or iOS to try "making my app faster", a speaker's name, and a query with no results.
