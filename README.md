# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

This repository starts with a working wireframe .NET MAUI 11 conference app in `src/ConferenceApp/`. A coding agent such as GitHub Copilot styles it into a polished conference companion app, then adds on-device AI step by step: local chat, data ingestion and enrichment, semantic search (comparing Apple's built-in embeddings with a bundled ONNX model), tool calling and approved actions, and an Agent Framework workflow.

It's the companion to the .NET Conf 2026 South Africa talk, and works as a hands-on workshop.

## What's here

| Path | Contents |
|---|---|
| [`src/ConferenceApp/`](src/ConferenceApp/) | The starting wireframe app: Event (with sponsors and map), Agenda (with search and favourites filter), Speakers, Assistant, and DevFlow |
| [`.github/prompts/`](.github/prompts/) | Eight prompts (`01`–`08`), plus optional [extras](.github/prompts/extras/). Copy each into your agent in order |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Shared guidance the agent reads automatically: .NET 11 and MAUI 11 notes, architecture, skills, and how to test with DevFlow |
| [`DESIGN.md`](DESIGN.md) & [`PRODUCT.md`](PRODUCT.md) | The Impeccable design system and product brief: brand colours, type tokens, spacing, components, and layouts |
| [`data/`](data/README.md) | An offline snapshot of the conference: a JSON catalogue, Markdown descriptions and images |
| [`models/`](models/README.md) | A small ONNX embedding model (`all-MiniLM-L6-v2`) for cross-platform semantic search |

## Before you start

- **.NET 11 SDK** with the MAUI workload (`dotnet workload install maui`), and the platform tooling for your target: Xcode for iOS and Mac Catalyst, the Android SDK for Android.
- **MAUI CLI** from [MAUI Labs](https://github.com/dotnet/maui-labs): `dotnet tool install -g Microsoft.Maui.Cli --prerelease`, then run `maui doctor`.
- **A coding agent** that reads `.github/copilot-instructions.md`, such as GitHub Copilot, with the `impeccable` design skill and the MAUI Labs skills. With the Copilot CLI:

  ```sh
  npx skills add google-labs-code/impeccable
  copilot plugin marketplace add dotnet/maui-labs
  copilot plugin install dotnet-maui@dotnet-maui-labs
  copilot plugin install dotnet-maui-tooling@dotnet-maui-labs
  copilot plugin install dotnet/skills:plugins/dotnet   # optional: C# and MSBuild
  ```
- **For on-device AI** (steps 03 onwards): a Mac with Apple Intelligence works best (run the app as Mac Catalyst). Android and Windows use the ONNX model for search; local chat there needs supported hardware (Gemini Nano or Windows AI).

## The steps

Run the prompts in order, one at a time, and check the app after each. Each builds on the last.

| # | Prompt | Adds |
|---|---|---|
| 01 | [Inspect and drive](.github/prompts/01-inspect-and-drive.prompt.md) | Launches the wireframe in the Mobile Canvas + DevFlow Web Inspector, drives the UI, and inspects `Preferences` |
| 02 | [Make it beautiful](.github/prompts/02-make-it-beautiful.prompt.md) | Styles the wireframe app with `impeccable` and `DESIGN.md`, checking light/dark themes and `ui diagnostics` |
| 03 | [On-device chat](.github/prompts/03-on-device-chat.prompt.md) | Connects the Assistant tab to the local chat model (`IChatClient`) with titles and times |
| 04 | [Ingestion and enrichment](.github/prompts/04-ingestion-and-enrichment.prompt.md) | `Microsoft.Extensions.DataIngestion` pipeline generating cached summaries and topics |
| 05 | [Apple semantic search](.github/prompts/05-apple-semantic-search.prompt.md) | `VectorStoreWriter` and semantic search with Apple's built-in sentence embeddings |
| 06 | [ONNX semantic search](.github/prompts/06-onnx-semantic-search.prompt.md) | Swaps `IEmbeddingGenerator` to the bundled ONNX MiniLM model for accurate retrieval |
| 07 | [Tools and actions](.github/prompts/07-tools-and-actions.prompt.md) | Read-only tools (including semantic search RAG), navigation, and approval-gated favourites |
| 08 | [Plan my day](.github/prompts/08-plan-my-day.prompt.md) | An Agent Framework workflow that builds a conflict-free schedule |

### Workshop extras

- [Create the app from scratch](.github/prompts/extras/create-app-from-scratch.prompt.md)
- [Chat RAG middleware](.github/prompts/extras/chat-rag-middleware.prompt.md) (for local models without tool calling)
- [Cloud routing](.github/prompts/extras/cloud-routing.prompt.md) (`SemanticRoutingChatClient` failover to a cloud model)

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
