---
description: Add on-device semantic session search with a keyword fallback
---

Add search to the sessions list so people can find talks by what they mean, not just exact words: "making my app faster" should find performance sessions. Keep the existing design and features, and keep the code small enough to explain during a live demo.

Put search behind one small app service, such as `SearchSessionsAsync(query)`, returning ranked sessions with a short matching excerpt. The UI and, later, an in-app assistant should both use it. Everything runs on the device. No cloud service, API key, or backend.

Before implementing, check the current packages and docs linked below. These APIs are experimental or platform-specific, so verify availability at runtime and keep search working everywhere.

## Content ingestion

Index the session Markdown files that step 01 already bundles, using the paths in `conference.json`. Don't scrape the website or convert the content again; `conference.json` stays the source for times, cities, and links.

- `MarkdownReader` comes from **`Microsoft.Extensions.DataIngestion.Markdig`**, not the base package alone. Open each file with `FileSystem.OpenAppPackageFileAsync` and call `new MarkdownReader().ReadAsync(stream, sessionId, "text/markdown", cancellationToken)`. The session ID becomes `document.Identifier`.
- The file's `# H1` title is parsed as a header element, so a header-aware chunker can carry the title as each chunk's context. Use `IngestionChunker<string>.ProcessAsync(document, cancellationToken)` to produce `IngestionChunk<string>` values, then the embedding generator's `GenerateAsync` to embed them. A small custom chunker is fine.
- The full `IngestionPipeline<T>` accepts `FileInfo`/`DirectoryInfo`, not MAUI asset streams. Use the reader and chunker directly; a vector database and `VectorStoreWriter` aren't required for a small agenda.
- These ingestion packages are preview. Validate the selected versions on the target device, including release/trimming behavior where relevant. If the dependency isn't viable on mobile, retain the already parsed Markdown body and the same small chunking/indexing service, explain the limitation, and don't fight the pipeline on stage.

## Implementation guidance

- **Keyword search everywhere.** Start with simple case-insensitive matching over title, abstract, speakers, track, and tags. This is the fallback, and it stays useful for exact speaker names, API names, and acronyms that semantic search handles poorly.
- **Apple: semantic search.** Use `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI` through `IEmbeddingGenerator<string, Embedding<float>>`, with the English sentence model for both index and queries. Apple Intelligence is not required, but the language's native sentence model must be available; handle unsupported models explicitly.
  - Index the session bodies on the device using the ingestion flow above. Compare whole-abstract embeddings with small sentence-based chunks; two sentences without overlap, prefixed with the session title, is a candidate to evaluate, not an Apple requirement or a proven optimum.
  - Rank a session by its best chunk's cosine similarity, return the top results, and show the best excerpt. Don't invent a similarity threshold.
  - Build the index off the UI thread after sessions load and cache it to disk. Fingerprint content, chunking, language, and model identity/revision/dimension as available; a generic provider name alone isn't a model version. Reject empty, mismatched, non-finite, or zero-length vectors before cosine comparison.
  - Show exact keyword matches first, then semantic results. Don't add raw keyword and cosine scores together.
  - Evaluate about five realistic queries from this agenda with expected session IDs before choosing the chunking. In Debug builds only, log or show each result's matched chunk and score so the ranking can be inspected; don't ship a separate playground page.
- **Windows: optional App Content Search.** Only consider it if Windows is already a target and the required SDK/access is available; otherwise keep keyword search.
  - The new `Microsoft.Windows.Search.AppContentIndex.AppContentIndexer` supports both lexical and semantic indexing. It requires package identity; Learn explicitly requires `<systemai:Capability Name="systemAIModels"/>` for semantic indexing and OCR, not lexical indexing generally. Follow the documented manifest namespace and current Limited Access Feature requirements.
  - Check `GetIndexCapabilitiesOfCurrentSystem()` for `IndexCapability.TextSemantic` and the content language, and inspect index-creation results. Don't assume every Windows machine supports semantic indexing or impose a blanket NPU requirement without evidence for the selected SDK.
  - This indexer manages its own chunks. Index per-session text with stable content IDs, retain that exact text to resolve returned offsets, and handle asynchronous indexing. Don't feed it Apple vectors or label lexical results as semantic.
  - The older `Windows.Storage.Search.ContentIndexer` is a separate Windows Search API, not this semantic indexer.
- **Android and other platforms:** use keyword search unless a supported on-device embedding provider is verified. Don't download a model or call a cloud service just to fill the gap.
- Keep results scoped to the selected city and deduplicate by scheduled-session ID. Empty queries restore the normal agenda.
- Debounce typing, cancel stale searches, and show whether results are semantic or keyword-only, including while indexing or after a provider failure. Log failures and explain the fallback; search must never block or break the sessions list.

Use the `maui-essentials-ai` skill when available, but verify its guidance against the current upstream docs.

Use DevFlow to exercise the real app: an interest-based query, an exact speaker name, an empty query, and a no-results query. Report which search mode actually ran on each platform you tested; don't claim semantic search worked where the model was unavailable.

## Current references

- [`Microsoft.Maui.Essentials.AI` and `NLEmbeddingGenerator`](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Apple: finding similarities between pieces of text](https://developer.apple.com/documentation/naturallanguage/finding-similarities-between-pieces-of-text)
- [Windows App Content Search](https://learn.microsoft.com/windows/ai/apis/app-content-search-tutorial)
- [Windows index creation options: lexical and semantic requirements](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.windows.search.appcontentindex.getorcreateindexoptions?view=windows-app-sdk-2.0)
- [Older Windows Search ContentIndexer](https://learn.microsoft.com/uwp/api/windows.storage.search.contentindexer)
- [Microsoft.Extensions.DataIngestion overview](https://learn.microsoft.com/dotnet/ai/conceptual/medi-library)
- [Data ingestion in .NET](https://learn.microsoft.com/dotnet/ai/conceptual/data-ingestion)
- [MarkdownReader source and stream API](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.DataIngestion.Markdig/MarkdownReader.cs)
- [Microsoft.Extensions.AI embeddings](https://learn.microsoft.com/dotnet/ai/ai-extensions)
