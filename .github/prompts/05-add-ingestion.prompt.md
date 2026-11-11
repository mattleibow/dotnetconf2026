---
description: Replace the indexing code with a data ingestion pipeline
---

Replace our hand-written indexing loop with a `Microsoft.Extensions.DataIngestion` pipeline:

- `MarkdownReader` (from `Microsoft.Extensions.DataIngestion.Markdig`) reads each session's Markdown.
- Turn our two-sentence chunking into a small custom `IngestionChunker<string>`.
- `VectorStoreWriter` writes the chunks into the in-memory vector store, using the same embedding generator.
- Update search to query the writer's collection. Results should stay about the same; only the indexing changes. Small differences from how Markdown is parsed are fine; don't work around the reader to keep them identical.

The pipeline reads files rather than app-package streams, so copy the session Markdown to app data first. These packages are preview, so check the docs first: [Data ingestion in .NET](https://learn.microsoft.com/dotnet/ai/conceptual/data-ingestion).

Use DevFlow to check that search still finds the same talks.
