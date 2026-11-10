---
description: Add on-device semantic search, with keyword search as the fallback
---

Add search to the agenda so people can find talks by meaning, not just exact words: "making my app faster" should find the performance talks.

Put it behind one small service, like `SearchSessionsAsync(query)`, that the UI and, later, the assistant can both use. Everything runs on the device.

- Keyword search works everywhere. It's the fallback, and it's better for exact speaker names.
- On Apple, use `NLEmbeddingGenerator` from `Microsoft.Maui.Essentials.AI`. Index the session Markdown with `Microsoft.Extensions.DataIngestion` (`MarkdownReader` from the `.Markdig` package, plus a chunker) and cache the index on disk.
- Try a couple of chunk sizes against a few real queries from this agenda before picking one. In Debug builds, show the matched chunk and score so I can see why something ranked.
- Show exact keyword matches first, then semantic ones, and only for the selected city.
- Windows and Android can stay on keyword search unless you find something on-device that's verified to work.
- Show whether results are semantic or keyword, and never let search break the agenda.

These packages are preview, so check the current docs first:

- [Microsoft.Maui.Essentials.AI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Data ingestion in .NET](https://learn.microsoft.com/dotnet/ai/conceptual/data-ingestion)
- [Windows App Content Search](https://learn.microsoft.com/windows/ai/apis/app-content-search-tutorial)

Use DevFlow to try an interest-based query, a speaker's name, and a query with no results. Tell me which search mode actually ran.
