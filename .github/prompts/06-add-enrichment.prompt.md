---
description: Add topics and summaries during ingestion
---

Add two enrichment steps to the ingestion pipeline, so each session gets a topic and a one-line summary:

- **Topic:** a small custom chunk processor that compares each chunk's embedding with short descriptions of a few topics (AI, Mobile, Web, Data, Cloud, Other) and keeps the closest. No LLM; just the embedding generator we already have.
- **Summary:** `SummaryEnricher` with the on-device chat client from `Microsoft.Maui.Essentials.AI`, where a local model is available.
- One topic and one summary per session, not per chunk. `SummaryEnricher` works on chunks, so give it each whole session as a single chunk, one at a time. The model may decline some requests; show those sessions without a summary.
- Show topic chips and the summary on session cards, and let me filter by topic.
- Summaries are slow, so run them in the background and never block the app.

Use DevFlow to check the topics look sensible and summaries appear.
