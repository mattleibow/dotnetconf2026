# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

This repository is the companion to the .NET Conf 2026 South Africa talk and a hands-on workshop.

`main` starts as a complete, working .NET MAUI 11 wireframe conference app (`ConferenceApp.slnx`, [`src/ConferenceApp/`](src/ConferenceApp/))—with the Event (sponsors + map), Agenda (literal `Contains` search + All/Favourites filter), Speakers, and Assistant pages, plus the design system ([`DESIGN.md`](DESIGN.md), [`PRODUCT.md`](PRODUCT.md), [`.impeccable/`](.impeccable/)) and [`Microsoft.Maui.DevFlow.Agent`](src/ConferenceApp/MauiProgram.cs) already wired up.

From `main`, two independent pull requests add visual styling and on-device AI with **zero overlapping files**, so they can be explored or merged in either order:

- **Visual Design & Styles ([#30](https://github.com/mattleibow/dotnetconf2026/pull/30)):** Uses the `impeccable` skill, [`DESIGN.md`](DESIGN.md), and [`PRODUCT.md`](PRODUCT.md) to style `Colors.xaml`, `Styles.xaml`, `AppShell.xaml`, and `Views/*.xaml` for phone and desktop in light and dark themes (9 `.xaml` files only).
- **On-Device AI ([#31](https://github.com/mattleibow/dotnetconf2026/pull/31)):** Adds `src/ConferenceApp/AI/` (`builder.AddConferenceAI()`) with local on-device chat (`AppleIntelligenceChatClient` / `GeminiNanoChatClient` with `OrderedFailoverChatClient`), ONNX MiniLM semantic search (`VectorStoreCollection<string, Session>`), background data enrichment (`SummaryEnricher` + `KeywordEnricher`), source-generated tools (`[AIToolSource]` with `[FromServices]`), and multi-agent workflows (`Microsoft.Agents.AI.Workflows`) (`.cs` and project files only).

## What's here

| Path | Contents |
|---|---|
| [`src/ConferenceApp/`](src/ConferenceApp/) | The .NET MAUI 11 wireframe conference app: Event (with sponsors and map), Agenda (with literal `Contains` search and favourites filter), Speakers, Assistant, and DevFlow |
| [`src/ConferenceApp/Data/`](src/ConferenceApp/Data/) | Read-only conference database (`ConferenceDbProvider`, `ConferenceDb`, `DatabasePreloadService`, `Session`, `City`, `Speaker`, `Venue`, `Sponsor`) |
| [`.github/prompts/`](.github/prompts/) | Two live demo prompts (`01-inspect-and-drive` and `02-make-it-beautiful`) |
| [`docs/`](docs/README.md) | Speaker runbook split into [`01-whats-new-net11-maui11.md`](docs/01-whats-new-net11-maui11.md), [`02-agents-building-apps.md`](docs/02-agents-building-apps.md), and [`03-giving-the-app-ai.md`](docs/03-giving-the-app-ai.md) |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Shared guidance the agent reads automatically: .NET 11 and MAUI 11 notes, architecture, skills, and DevFlow workflow |
| [`DESIGN.md`](DESIGN.md) & [`PRODUCT.md`](PRODUCT.md) | The Impeccable design system and product brief: brand colours, type tokens, spacing, components, and layouts |
| [`data/`](data/README.md) | An offline snapshot of the conference: a JSON catalogue, Markdown descriptions, and images |

## Feature Pull Requests (mergeable in either order)

| Pull Request | Branch | Files touched | Summary |
|---|---|---|---|
| [#30 — Visual design and styles](https://github.com/mattleibow/dotnetconf2026/pull/30) | `mattleibow-layer-2-visual-design-and-styles` | 9 `.xaml` files (`Colors.xaml`, `Styles.xaml`, `AppShell.xaml`, `Views/*.xaml`) | Styles all four tabs and detail pages following `DESIGN.md` and `PRODUCT.md` |
| [#31 — On-device AI stack](https://github.com/mattleibow/dotnetconf2026/pull/31) | `mattleibow-layer-3-on-device-ai` | `.cs`, `.csproj`, `Directory.Packages.props`, `models/`, `packages/`, `NuGet.config` | Adds `src/ConferenceApp/AI/` and upgrades `SearchService`, `ChatService`, and `Session` |

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
