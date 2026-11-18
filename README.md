# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

This repository is the companion to the .NET Conf 2026 South Africa talk and a hands-on workshop.

`main` starts as a complete, styled .NET MAUI 11 conference app (`ConferenceApp.slnx`, [`src/ConferenceApp/`](src/ConferenceApp/))—with the Event (hero banner, sponsors, and map), Agenda (literal `Contains` search, All/Favourites filter, and swipeable ticket cards), Speakers, and Assistant pages, plus [`Microsoft.Maui.DevFlow.Agent`](src/ConferenceApp/MauiProgram.cs) wired up for live UI inspection and debugging.

From `main`, two pull requests cover the two halves of the talk:

- **Demo Bugs for MAUI DevFlow Bug Hunt ([#33](https://github.com/mattleibow/dotnetconf2026/pull/33), Draft):** Optional branch introducing silent bugs so you can demo [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md) using DevFlow **Logs**, **Storage**, **UI Diagnostics**, and **Screenshots**.
- **On-Device AI ([#31](https://github.com/mattleibow/dotnetconf2026/pull/31)):** Adds `src/ConferenceApp/AI/` (`builder.AddConferenceAI()`) with local on-device chat (`AppleIntelligenceChatClient` / `GeminiNanoChatClient` with `OrderedFailoverChatClient`), ONNX MiniLM semantic search (`VectorStoreCollection<string, Session>`), background data enrichment (`SummaryEnricher` + `KeywordEnricher`), source-generated tools (`[AIToolSource]` with `[FromServices]`), and multi-agent workflows (`Microsoft.Agents.AI.Workflows`).

## What's here

| Path | Contents |
|---|---|
| [`src/ConferenceApp/`](src/ConferenceApp/) | The .NET MAUI 11 conference app: Event (with sponsors and map), Agenda (with literal `Contains` search, swipeable cards, and favourites filter), Speakers, Assistant, and DevFlow |
| [`src/ConferenceApp/Data/`](src/ConferenceApp/Data/) | Read-only conference database (`ConferenceDbProvider`, `ConferenceDb`, `DatabasePreloadService`, `Session`, `City`, `Speaker`, `Venue`, `Sponsor`) |
| [`.github/prompts/`](.github/prompts/) | Two live demo prompts (`01-explore-app` and `02-bug-hunt`) |
| [`docs/`](docs/README.md) | Speaker runbook split into [`01-whats-new-net11-maui11.md`](docs/01-whats-new-net11-maui11.md), [`02-agents-building-apps.md`](docs/02-agents-building-apps.md), and [`03-giving-the-app-ai.md`](docs/03-giving-the-app-ai.md) |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Shared guidance the agent reads automatically: .NET 11 and MAUI 11 notes, architecture, skills, and DevFlow workflow |
| [`DESIGN.md`](DESIGN.md) & [`PRODUCT.md`](PRODUCT.md) | The Impeccable design system and product brief: brand colours, type tokens, spacing, components, and layouts |
| [`data/`](data/README.md) | An offline snapshot of the conference: a JSON catalogue, Markdown descriptions, and images |

## Pull Requests

| Pull Request | Branch | Status | Summary |
|---|---|---|---|
| [#31 — Add on-device AI stack](https://github.com/mattleibow/dotnetconf2026/pull/31) | `mattleibow-layer-3-on-device-ai` | Ready | Adds `src/ConferenceApp/AI/`, `models/`, `packages/`, `NuGet.config`, and upgrades `SearchService`, `ChatService`, and `Session` |
| [#33 — Demo bugs for MAUI DevFlow bug hunt](https://github.com/mattleibow/dotnetconf2026/pull/33) | `mattleibow-demo-bugs-for-devflow-bug-hunt` | Draft | Optional branch introducing silent bugs for [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md) |

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
- **Windows:** use the packaged Windows launch profile on Windows 11 24H2 (build 26100) or newer. Local chat requires a compatible Copilot+ PC and its on-device model; semantic search runs on the CPU without it. Windows tool calling is experimental in [AI layer 6 (#41)](https://github.com/mattleibow/dotnetconf2026/pull/41) and can be slow or fail; use the app's pages for reliable navigation and favourites.

## Live Demo Flow

### Part 1: Agents Building Apps (Prompts `01`–`02`)

| # | Prompt | What happens |
|---|---|---|
| 01 | [Explore app](.github/prompts/01-explore-app.prompt.md) | Launches the app with DevFlow, finds the .NET MAUI session on the Agenda, adds it to Favourites (verifying `Preferences` in DevFlow Storage), and opens the speaker's profile |
| 02 | [Bug hunt](.github/prompts/02-bug-hunt.prompt.md) | Uses DevFlow **Logs** (logged `InvalidOperationException` in `SpeakerViewModel`), **Storage** (`favorite_sessions` vs `favorite-sessions` key mismatch), and **UI Diagnostics / Screenshots** to diagnose and fix silent bugs |

### Part 2: Giving the App That Same AI (`src/ConferenceApp/AI/`)

Watch what the on-device model and tools are doing in real time from a terminal (or in the **Logs** tab of the DevFlow Web Inspector):

```sh
maui devflow logs --follow --no-json
```

| Path | Feature |
|---|---|
| [`AI/ConferenceAIExtensions.cs`](src/ConferenceApp/AI/ConferenceAIExtensions.cs) | `builder.AddConferenceAI()` calling `AddChatClients()`, `AddEmbeddings()`, `AddDataEnrichment()`, `AddAITools()`, and `AddAgents()` |
| [`AI/ChatClients/`](src/ConferenceApp/AI/ChatClients/) | `ChatExtensions.cs` (`AppleIntelligenceChatClient`, `GeminiNanoChatClient` with `OrderedFailoverChatClient`, `WindowsAIChatClient` with `.UseLogging()`) |
| [`AI/Data/`](src/ConferenceApp/AI/Data/) | `DataExtensions.cs` (`EnricherOptions`), `SessionSummaryEnrichmentService.cs` (`SummaryEnricher`), and `SessionTopicEnrichmentService.cs` (`KeywordEnricher`) |
| [`AI/Embeddings/`](src/ConferenceApp/AI/Embeddings/) & [`Services/SearchService.cs`](src/ConferenceApp/Services/SearchService.cs) | `EmbeddingExtensions.cs` (`AddEmbeddingGenerator`, `AddInMemoryVectorStore`, `AddInMemoryVectorStoreRecordCollection<string, Session>("sessions")`), `MiniLmEmbeddingGenerator.cs`, and `SessionIndexingService.cs` |
| [`AI/Tools/`](src/ConferenceApp/AI/Tools/) | `ToolExtensions.cs`, `ConferenceDataTools.cs` (`ConferenceDataToolContext`), and `FavoritesTools.cs` (`FavoritesToolContext`) using `[FromServices]` |
| [`AI/Agents/`](src/ConferenceApp/AI/Agents/) | `AgentExtensions.cs` (`RoutingChatClient.Create` + `SemanticRoutingChatClient`), `ConferenceAssistantAgent.cs`, `SessionDeepDiveWorkflow.cs`, `TalkRecommendationWorkflow.cs`, and `UserContextProvider.cs`, `ScheduleContextProvider.cs`, `AvailableSessionsContextProvider.cs`, `RagContextProvider.cs` |

## Refreshing the data and model

Both are already included. To refresh them (with the .NET 11 SDK, from the repository root):

```sh
dotnet run data/ImportConference.cs
dotnet run models/DownloadModels.cs
```
