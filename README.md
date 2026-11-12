# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

This repository is the companion to the .NET Conf 2026 South Africa talk and a hands-on workshop.

`src/ConferenceApp/` starts as a complete, working .NET MAUI 11 conference app—with unstyled wireframe XAML and the full on-device AI stack already wired up in [`src/ConferenceApp/AI/`](src/ConferenceApp/AI/) through `builder.AddChatClients()`, `builder.AddEmbeddings()`, and `builder.AddAgents()`.

- **Part 1 — Agents Building Apps:** Use a coding agent (such as GitHub Copilot), the Mobile Device canvas, the MAUI DevFlow Web Inspector, and the `impeccable` design skill to inspect the wireframe, add an inline favourite toggle on Agenda cards, style the entire app against [`DESIGN.md`](DESIGN.md), and verify layout diagnostics and `Preferences` live.
- **Part 2 — Giving the App That Same AI:** Walk through [`src/ConferenceApp/AI/`](src/ConferenceApp/AI/) and demo on-device chat (`IChatClient`), data ingestion and enrichment (`Microsoft.Extensions.DataIngestion`), ONNX MiniLM semantic search (`IEmbeddingGenerator`), source-generated tools with user approval (`[AIToolSource]`), and the **Plan my day** workflow (`Microsoft.Agents.AI.Workflows`) while streaming live `[AI]` logs in DevFlow.

## What's here

| Path | Contents |
|---|---|
| [`src/ConferenceApp/`](src/ConferenceApp/) | The .NET MAUI 11 conference app: Event (with sponsors and map), Agenda (with semantic search and favourites filter), Speakers, Assistant, and DevFlow |
| [`src/ConferenceApp/AI/`](src/ConferenceApp/AI/) | Clean, isolated on-device AI layer registered via `AddChatClients()`, `AddEmbeddings()`, and `AddAgents()` |
| [`.github/prompts/`](.github/prompts/) | Two live demo prompts (`01-inspect-and-drive` and `02-make-it-beautiful`) |
| [`docs/demo.md`](docs/demo.md) | Speaker runbook & file-by-file talking points (.NET 11, MAUI 11, DevFlow, and in-app AI walkthrough) |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Shared guidance the agent reads automatically: .NET 11 and MAUI 11 notes, architecture, skills, and DevFlow workflow |
| [`DESIGN.md`](DESIGN.md) & [`PRODUCT.md`](PRODUCT.md) | The Impeccable design system and product brief: brand colours, type tokens, spacing, components, and layouts |
| [`data/`](data/README.md) | An offline snapshot of the conference: a JSON catalogue, Markdown descriptions, and images |
| [`models/`](models/README.md) | Bundled ONNX embedding model (`all-MiniLM-L6-v2`) for cross-platform semantic search |

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
- **For on-device AI:** a Mac with Apple Intelligence works best (run the app as Mac Catalyst or iOS Simulator). Android and Windows use the bundled ONNX model for semantic search; local chat there uses Gemini Nano or Windows AI on supported hardware.

## Live Demo Flow

### Part 1: Agents Building Apps (Prompts `01`–`02`)

| # | Prompt | What happens |
|---|---|---|
| 01 | [Inspect and drive](.github/prompts/01-inspect-and-drive.prompt.md) | Opens the Mobile Device canvas + DevFlow Web Inspector (`http://localhost:19223/inspector/<agent-id>/`), checks `ui tree` and `ui diagnostics`, drives the wireframe UI, inspects sandboxed `Preferences`, and shows live `[AI]` logs |
| 02 | [Make it beautiful](.github/prompts/02-make-it-beautiful.prompt.md) | Uses `impeccable` and `DESIGN.md` to style all 4 tabs and detail pages, adds an inline heart toggle (`session-favorite-<id>`) on Agenda talk cards, and verifies light/dark themes and `ui diagnostics` |

### Part 2: Giving the App That Same AI (`src/ConferenceApp/AI/`)

Watch what the on-device model and tools are doing in real time from a terminal (or in the **Logs** tab of the DevFlow Web Inspector):

```sh
maui devflow logs --follow --no-json
```

| File | Feature |
|---|---|
| [`AI/ChatExtensions.cs`](src/ConferenceApp/AI/ChatExtensions.cs) & [`Services/ChatService.cs`](src/ConferenceApp/Services/ChatService.cs) | `builder.AddChatClients()` registering `IChatClient` (`AppleIntelligenceChatClient`, `GeminiNanoChatClient`, `WindowsAIChatClient`) with `.UseLogging()` |
| [`AI/EmbeddingExtensions.cs`](src/ConferenceApp/AI/EmbeddingExtensions.cs) & [`AI/MiniLmEmbeddingGenerator.cs`](src/ConferenceApp/AI/MiniLmEmbeddingGenerator.cs) | `builder.AddEmbeddings()` registering the 384-dim ONNX `all-MiniLM-L6-v2` `IEmbeddingGenerator<string, Embedding<float>>`, `SessionEnrichmentService`, and `SearchService` |
| [`AI/SessionEnrichmentService.cs`](src/ConferenceApp/AI/SessionEnrichmentService.cs) & [`Services/SearchService.cs`](src/ConferenceApp/Services/SearchService.cs) | `Microsoft.Extensions.DataIngestion` pipeline (`MarkdownReader`, `SummaryEnricher`, `ClassificationEnricher`, `VectorStoreWriter`) + hybrid vector search |
| [`AI/ConferenceAssistantTools.cs`](src/ConferenceApp/AI/ConferenceAssistantTools.cs) | Source-generated `[AIToolSource]` tools (`search_sessions`, `get_session`, `get_speaker`, `open_*`, and approval-gated `add_favorite` / `remove_favorite`) |
| [`AI/AgentExtensions.cs`](src/ConferenceApp/AI/AgentExtensions.cs) | `builder.AddAgents()` wiring `TextSearchProvider` RAG, `AsAIAgent`, `AgentWorkflowBuilder.BuildSequential`, and `SemanticRoutingChatClient` |

## Refreshing the data and model

Both are already included. To refresh them (with the .NET 11 SDK, from the repository root):

```sh
dotnet run data/ImportConference.cs
dotnet run models/DownloadModels.cs
```
