---
description: Ingest session Markdown and enrich with summaries and topics
---

Use `Microsoft.Extensions.DataIngestion` to read each session's Markdown and enrich every talk with a one-line summary and a topic:

- Read each session's Markdown with `MarkdownReader` (from `Microsoft.Extensions.DataIngestion.Markdig`), using one chunk per session (title plus abstract). The pipeline reads files rather than app-package streams, so copy the session Markdown to app data first.
- Enrich each talk chunk with `SummaryEnricher` and `ClassificationEnricher` using the on-device `IChatClient` from `Microsoft.Maui.Essentials.AI`, one session at a time.
- For `ClassificationEnricher`, use a short fixed list (`AI`, `Mobile`, `Web`, `Cloud`, `Data`, `IoT`) with a fallback to `None` when none clearly fits, and tolerate sessions where the local model declines to summarize.
- Cache the generated summaries and topics to a JSON file in app data so they are only generated once, run enrichment in the background so it never blocks the app, and log `[AI]` progress via `ILogger`.
- Show the topic chip and summary on session cards, and include each session's summary and topic in the assistant's system instructions.

Docs: [Data ingestion in .NET](https://learn.microsoft.com/dotnet/ai/conceptual/data-ingestion).

Use DevFlow on Mac Catalyst to verify that summaries and topics appear on the agenda, check `maui devflow logs --limit 20 --no-json`, and verify that the assistant can now answer "Is there a talk about predicting sports odds?" in Durban.
