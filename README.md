# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

This repository starts almost empty. A coding agent such as GitHub Copilot builds a .NET MAUI 11 conference app from it, one prompt at a time. Then the agent adds AI to the app itself: semantic search, an on-device assistant, tools and actions, an Agent Framework workflow, and optional cloud routing.

It's the companion to the .NET Conf 2026 South Africa talk, and works as a hands-on workshop.

## What's here

| Folder | Contents |
|---|---|
| [`.github/prompts/`](.github/prompts/) | Twelve prompts, one per step. Copy each into your agent in order |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Shared guidance the agent reads automatically: .NET 11 and MAUI 11 notes, architecture, the style guide, and how to test with DevFlow |
| [`data/`](data/README.md) | An offline snapshot of the conference: a JSON catalogue, Markdown descriptions and images |
| [`models/`](models/README.md) | A small ONNX embedding model for semantic search on platforms without a built-in one |

There's no app code yet. You create it.

## Before you start

- **.NET 11 SDK** with the MAUI workload (`dotnet workload install maui`), and the platform tooling for your target: Xcode for iOS and Mac Catalyst, the Android SDK for Android.
- **MAUI CLI** from [MAUI Labs](https://github.com/dotnet/maui-labs): `dotnet tool install -g Microsoft.Maui.Cli --prerelease`, then run `maui doctor`.
- **A coding agent** that reads `.github/copilot-instructions.md`, such as GitHub Copilot, with the [.NET agent skills](https://github.com/dotnet/skills) and the MAUI DevFlow skills enabled.
- **For on-device AI** (steps 03 and 06 onwards): a Mac with Apple Intelligence works best (run the app as Mac Catalyst). Android and Windows use the ONNX model for search; local chat there needs supported hardware (Gemini Nano or Windows AI).

## The steps

Run the prompts in order, one at a time, and check the app after each. Each builds on the last.

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

Tips:

- Commit after each step, so you can go back if a step goes wrong.
- Start each step in a fresh agent session; the prompts are written to stand alone.
- The AI packages are experimental. If a step fails because an API changed, tell the agent to check the linked docs.

## Refreshing the data and model

Both are already included. To refresh them (with the .NET 11 SDK, from the repository root):

```sh
dotnet run data/ImportConference.cs
dotnet run models/DownloadModels.cs
```
