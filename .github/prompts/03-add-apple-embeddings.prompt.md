---
description: Make search understand meaning on Apple
---

Make search understand meaning, not just words: in Durban, "saving money on AI" should bring up the talk on the hidden cost of calling LLMs, and "hardware projects" the Arduino and solar car talks. Start with Apple's built-in sentence model. It's small, so results will be rough; that's fine, the next step brings a better model.

- Use `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI`, through `IEmbeddingGenerator<string, Embedding<float>>`.
- At startup, split each abstract into two-sentence chunks that start with the session title, and add them to an in-memory vector store from `CommunityToolkit.VectorData.InMemory`. Give the store the embedding generator so it does the embedding for us.
- Search the store, rank each session by its best chunk, and show the top 5 after any exact keyword matches, with the best chunk as the excerpt. Small on-device models give modest similarity scores, so don't use a fixed cut-off.
- Other platforms keep keyword search for now. Show whether results are semantic or keyword.

These packages are new or preview, so check the docs first:

- [Microsoft.Maui.Essentials.AI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Vector stores in .NET](https://learn.microsoft.com/dotnet/ai/vector-stores/overview) (the toolkit's in-memory store implements these APIs)

Use DevFlow on Mac Catalyst or iOS to try "saving money on AI", "hardware projects", a speaker's name, and a query with no results.
