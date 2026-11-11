# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

An almost-empty starter for a live demo: GitHub Copilot builds a polished .NET MAUI 11 conference app, then adds an on-device assistant that can use the app's services and take real actions.

Reused across four cities. Simple code, great design, no finished app checked in.

## Demo data

[`data/`](data/README.md) holds an offline snapshot of the event: a JSON catalogue, Markdown descriptions and images. The app is built from it, so the demo doesn't depend on the website. To refresh it before an event:

```sh
dotnet run data/ImportConference.cs
```

[`models/`](models/README.md) holds the small ONNX embedding model used for semantic search where there's no built-in one. To refresh it: `dotnet run models/DownloadModels.cs`.

## Copy the prompts

Run these in order, one at a time. Each builds on the last:

| # | Prompt | Adds |
|---|---|---|
| 01 | [Create the app](.github/prompts/01-create-app.prompt.md) | `dotnet new maui`, then the conference app from the offline data |
| 02 | [Keyword search](.github/prompts/02-add-keyword-search.prompt.md) | A search box and one small search service |
| 03 | [Apple embeddings](.github/prompts/03-add-apple-embeddings.prompt.md) | Semantic search on Apple, with an in-memory vector store |
| 04 | [ONNX embeddings](.github/prompts/04-add-onnx-embeddings.prompt.md) | The same on Android and Windows with a small ONNX model |
| 05 | [Ingestion](.github/prompts/05-add-ingestion.prompt.md) | A DataIngestion pipeline instead of the indexing loop |
| 06 | [Enrichment](.github/prompts/06-add-enrichment.prompt.md) | Topics from embeddings and summaries from the local model |
| 07 | [Local chat](.github/prompts/07-add-local-chat.prompt.md) | An on-device assistant primed with the agenda |
| 08 | [Chat RAG](.github/prompts/08-add-chat-rag.prompt.md) | Full abstracts via simple RAG middleware |
| 09 | [Chat tools](.github/prompts/09-add-chat-tools.prompt.md) | Read-only tools the model can call |
| 10 | [Chat actions](.github/prompts/10-add-chat-actions.prompt.md) | Navigation and favourites, with approval |
| 11 | [Plan my day](.github/prompts/11-add-plan-my-day.prompt.md) | An Agent Framework workflow |
| 12 | [Cloud routing](.github/prompts/12-add-cloud-routing.prompt.md) | Optional: send complex questions to a cloud model |

The [Copilot instructions](.github/copilot-instructions.md) provide shared .NET 11, MAUI 11, design, and DevFlow guidance.

## Before starting

Have a compatible .NET 11 SDK, MAUI workload, platform tooling, and simulator or device ready; check the workload's required Xcode version on Apple. Install the [MAUI Labs CLI](https://github.com/dotnet/maui-labs) (`dotnet tool install -g Microsoft.Maui.Cli --prerelease`) and run `maui doctor`. Enable the relevant [.NET agent skills](https://github.com/dotnet/skills) and MAUI DevFlow skills in your coding agent.

The AI prompts use experimental [MAUI Labs local clients](https://github.com/dotnet/maui-labs/tree/main/src/AI/Microsoft.Maui.Essentials.AI) and [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions). Local models require supported hardware and model availability; a simulator alone may not be enough.
