# Speaker Runbook & Talking Points (`docs/demo.md`)

**Session:** *Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI*  
**Duration:** 45 minutes  
**Premise:** Start with a functional, zero-styled wireframe .NET MAUI 11 app that already has its on-device AI plumbed in (`src/ConferenceApp/AI/`).
1. **Part 1 (Agents Building Apps):** Let GitHub Copilot + Mobile Canvas + MAUI DevFlow + `impeccable` inspect the running app, add an inline UI feature, style all 4 tabs, and verify layout health and `Preferences`.
2. **Part 2 (Giving the App That Same AI):** While or after the agent styles the app, walk through the clean `.NET 11`, `MAUI 11`, and `AI/` files and demo semantic search, tool calling, approvals, and workflows live with real-time logs.

---

## Pre-Flight Checklist (5 mins before going on stage)

1. **Boot the iOS Simulator** (so `01` doesn't wait for cold boot):
   ```sh
   xcrun simctl boot "iPhone 17 Pro" || true
   open -a Simulator
   ```
2. **Start the DevFlow Broker** (if not already running):
   ```sh
   maui devflow list
   ```
3. **Keep a terminal tab ready for live AI logs** (for Part 2):
   ```sh
   maui devflow logs --follow --no-json
   ```

---

## Part 1: What's New in .NET 11, C# 15 & .NET MAUI 11 (5–8 mins)

Open these files briefly before kicking off the first prompt to show why the baseline code is so clean:

| File | What to point out | Why it matters in .NET 11 / MAUI 11 |
|---|---|---|
| [`src/ConferenceApp/ConferenceApp.csproj`](../src/ConferenceApp/ConferenceApp.csproj) | `<TargetFrameworks>net11.0-android;net11.0-ios;net11.0-maccatalyst</TargetFrameworks>` | **CoreCLR is now the default runtime** for .NET MAUI on iOS, Mac Catalyst, and Android—no old Mono/interpreter workarounds needed. **XAML Source Generation** (`MauiXamlInflator=SourceGen`), **XAML Incremental Hot Reload**, and **Lazy `ResourceDictionary`** are all on by default. |
| [`src/ConferenceApp/GlobalXmlns.cs`](../src/ConferenceApp/GlobalXmlns.cs) | `[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", ...)]` | **Implicit Global XAML Namespaces** in .NET 11. Register your namespaces once in C#, and every `.xaml` file in the project has zero `xmlns:` clutter at the top—even `<Map />` and custom converters work without prefixes. |
| [`src/ConferenceApp/Views/AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) & [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml) | `<ContentPage x:Class="ConferenceApp.Views.AgendaPage" x:DataType="AgendaViewModel">`, `IsVisible="{!string.IsNullOrWhiteSpace(Topic)}"`, `Text="{ShowFavoritesOnly ? 'Showing: Favourites' : 'Showing: All Sessions'}"`, `IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"` | Three huge .NET 11 XAML features in one file: **1) Zero `xmlns:` declarations**, **2) C# Expressions in XAML** (`{!string.IsNullOrWhiteSpace(Topic)}`, ternaries, `AND`, string interpolation `{$'chat-session-card-{Id}'}`—eliminating all `HasXyz` ViewModel properties and `InverseBooleanConverter`s!), and **3) AOT-safe compiled `RelativeSource` bindings** (`AncestorType={x:Type AgendaViewModel}, x:DataType=AgendaViewModel`). |
| [`src/ConferenceApp/ViewModels/AgendaViewModel.cs`](../src/ConferenceApp/ViewModels/AgendaViewModel.cs) | `[ObservableProperty] public partial string SearchQuery { get; set; } = "";` | **C# partial properties + `CommunityToolkit.Mvvm` 8.4.2**: no backing fields, no `_searchQuery` naming rules, and full IDE/source-gen visibility. |
| [`data/ImportConference.cs`](../data/ImportConference.cs) & [`models/DownloadModels.cs`](../models/DownloadModels.cs) | `#:package HtmlAgilityPack@1.12.4` and `dotnet run data/ImportConference.cs` | **.NET single-file C# scripts (`dotnet run file.cs`)**: no `.csproj` needed for repo utilities—used here to snapshot the Sessionize/web data and download/verify the ONNX model. |
| [`src/ConferenceApp/MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) & [`src/ConferenceApp/AI/ConferenceAIExtensions.cs`](../src/ConferenceApp/AI/ConferenceAIExtensions.cs) | `builder.AddConferenceAI();` -> `AddChatClients()`, `AddEmbeddings()`, `AddAITools()`, `AddAgents()`, plus `builder.AddMauiDevFlowAgent(...)` | Clean DI registration (`AddTransient<Page>().AddTransient<ViewModel>()`), a single `builder.AddConferenceAI()` entrypoint that steps through the 4 AI subfolders (`ChatClients/`, `Embeddings/`, `Tools/`, `Agents/`), plus **MAUI DevFlow Agent** registered in `#if DEBUG` with `EnableLayoutDiagnostics = true` and `EnableProfiler = true`. |
| [`src/ConferenceApp/Data/`](../src/ConferenceApp/Data/) | `ConferenceDbProvider.cs`, `ConferenceDb.cs`, `Session.cs`, `City.cs`, `Speaker.cs`, `Venue.cs`, `Sponsor.cs` | Read-only in-memory conference database (`dbProvider.GetDbAsync()`) that loads `conference.json` and caches Markdown descriptions once, while keeping user favourites cleanly separated in `UserPreferencesService`. |
| [`.github/copilot-instructions.md`](../.github/copilot-instructions.md), [`DESIGN.md`](../DESIGN.md), [`PRODUCT.md`](../PRODUCT.md) | Repo instructions + Impeccable design spec | How we keep coding agents aligned: `.github/copilot-instructions.md` teaches the agent .NET 11 APIs (like `ScaleToAsync`, `BoxView.Fill` brushes, and `GlobalXmlns`), while `DESIGN.md` defines the tokens and rules for the `impeccable` design skill. |

---

## Part 2: Agents Building Apps — Live Prompts `01` & `02` (12–15 mins)

### Step 01: Inspect and Drive ([`.github/prompts/01-inspect-and-drive.prompt.md`](../.github/prompts/01-inspect-and-drive.prompt.md))

Paste `01-inspect-and-drive.prompt.md` into the chat. While the agent works (~2–3 mins), call out:
- **Mobile Device Canvas (`mobile-device`):** Live iOS Simulator embedded right inside the side panel.
- **DevFlow Web Inspector (`browser` canvas at `http://localhost:19223/inspector/<id>/`):**
  - **UI Tree tab:** Live visual hierarchy, bounds, visibility, and `AutomationId`s.
  - **Logs tab:** Real-time `ILogger` stream from inside the app.
  - **Storage tab:** Inspecting and mutating sandboxed `Preferences` (`selected_city`, `favorites`) without writing debug UI.
- **Layout Diagnostics (`maui devflow ui diagnostics --profile agent`):** Automated detection of clipped text, off-screen elements, or zero-sized containers so the agent doesn't have to guess from screenshots alone.

### Step 02: Make It Beautiful + Add Inline Favourite Hearts ([`.github/prompts/02-make-it-beautiful.prompt.md`](../.github/prompts/02-make-it-beautiful.prompt.md))

Paste `02-make-it-beautiful.prompt.md` into the chat. While the agent works (~5–6 mins), switch to your editor and walk through **Part 3 (In-App AI Architecture)** below so there is zero dead air!

When Step 02 finishes, show the result on screen:
- **MAUI 11 UI touches added by the agent:**
  - `BoxView.Fill` accepting a `LinearGradientBrush` directly for the hero header on `EventPage.xaml`.
  - `ScaleToAsync` (new async animation API with `CancellationToken` in MAUI 11) on the new inline heart button (`session-favorite-<id>`) on Agenda cards.
  - Native `Microsoft.Maui.Controls.Maps` `<Map />` on the Event tab.
  - Adaptive phone vs. wide desktop layouts (`AdaptiveTrigger`) and light/dark themes (`AppThemeBinding`).

---

## Part 3: Giving the App That Same AI — Code Walkthrough & Live Demo (20 mins)

All in-app AI lives in **`src/ConferenceApp/AI/`** organized into 5 clean folders behind [`ConferenceAIExtensions.cs`](../src/ConferenceApp/AI/ConferenceAIExtensions.cs) (`builder.AddConferenceAI()`):
1. **`ChatClients/`** (`builder.AddChatClients()`)
2. **`Embeddings/`** (`builder.AddEmbeddings()`)
3. **`Data/`** (`builder.AddDataEnrichment()`)
4. **`Tools/`** (`builder.AddAITools()`)
5. **`Agents/`** (`builder.AddAgents()`)

Keep `maui devflow logs --follow --no-json` running in a terminal (or open the **Logs** tab in the DevFlow Web Inspector) so the audience sees every model call, embedding query, RAG context injection, semantic route selection, tool invocation, and workflow step in real time.

### 1. Local Chat Clients: [`src/ConferenceApp/AI/ChatClients/`](../src/ConferenceApp/AI/ChatClients/)

- **[`ChatExtensions.cs`](../src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) (`builder.AddChatClients()`):**
  - `Microsoft.Maui.Essentials.AI` (`11.0.0-preview.8.26555.2`) provides local on-device `IChatClient` implementations for every platform:
    - **iOS / Mac Catalyst (`OperatingSystem.IsIOSVersionAtLeast(26)`):** `AppleIntelligenceChatClient(loggerFactory, functionInvocationServices: services)`
    - **Android (`OperatingSystem.IsAndroidVersionAtLeast(26)`):** `GeminiNanoChatClient().AsBuilder().UseFunctionInvocation(loggerFactory)`
    - **Windows (`OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100)`):** `WindowsAIChatClient().AsBuilder().UseFunctionInvocation(loggerFactory)`
  - Wraps each in a `ChatClientBuilder` pipeline with `.UseLogging(loggerFactory)` so every prompt, streaming response, and function call shows up automatically in `maui devflow logs`!
  - Only registers `IChatClient` when the OS supports local AI (never throws `PlatformNotSupportedException` at startup).

### 2. Data Ingestion & Background Enrichment: [`src/ConferenceApp/AI/Data/`](../src/ConferenceApp/AI/Data/)

- **[`DataExtensions.cs`](../src/ConferenceApp/AI/Data/DataExtensions.cs) (`builder.AddDataEnrichment()`) & [`SessionEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionEnrichmentService.cs):**
  - Why: Putting all 54 full Markdown abstracts into a small on-device model's context window overflows or degrades accuracy.
  - How: `DataExtensions.cs` registers `EnricherOptions`, `SummaryEnricher` (`maxWordCount: 24`), and `ClassificationEnricher` (`AI`, `Mobile`, `Web`, `Cloud`, `Data`, `IoT`, fallback `None`) from `Microsoft.Extensions.DataIngestion` when `IChatClient` is available.
  - `SessionEnrichmentService` passes each talk's `session.EmbeddingText` (`Title + Description`) as an `IngestionChunk<string>` through `SummaryEnricher` + `ClassificationEnricher`, populates `session.Summary` and `session.Topic` in memory, and broadcasts `SessionsEnrichedMessage` via `WeakReferenceMessenger` so Agenda and Speaker cards update live.

### 3. Semantic Search & Vector Store: [`src/ConferenceApp/AI/Embeddings/`](../src/ConferenceApp/AI/Embeddings/) & [`src/ConferenceApp/Services/SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs)

- **Declarative Vector Model ([`Data/Session.cs`](../src/ConferenceApp/Data/Session.cs)):**
  - Uses `Microsoft.Extensions.VectorData` attributes directly on `Session`:
    - `[VectorStoreKey]` on `Id`
    - `[VectorStoreData(IsIndexed = true)]` on `CityId` (for LINQ filtering `Filter = s => s.CityId == cityId`)
    - `[VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]` on `string EmbeddingText => $"{Title}\n\n{Description}"` — because this attribute is on a `string` property, `VectorStoreCollection<string, Session>` automatically invokes the registered `IEmbeddingGenerator<string, Embedding<float>>` on both `UpsertAsync` and `SearchAsync`!
- **DI Registration ([`EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs)) & Indexing ([`SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs)):**
  - Registers `.AddEmbeddingGenerator(...).UseLogging()`, `.AddInMemoryVectorStore()`, and `.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions")`.
  - `SessionIndexingService` runs in the background on startup and calls `await sessionVectors.UpsertAsync(sessions)` — storing the exact `Session` instances in `VectorStoreCollection<string, Session>`.
- **The Apple `NLEmbeddingGenerator` vs. ONNX `all-MiniLM-L6-v2` story ([`MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/Embeddings/MiniLmEmbeddingGenerator.cs)):**
  - When we first wired `NLEmbeddingGenerator` (Apple's built-in 512-dim sentence embeddings), searching Durban for `"football forecasts"` ranked *Developing A .NET Sports Predictor* **#5**, and `"gradient descent"` ranked *Wait, what does Optimization really entail?* **#4** (because `NLEmbedding` is a lightweight OS word/sentence table, not a transformer retrieval model).
  - Because `VectorStoreCollection<string, Session>` depends only on `IEmbeddingGenerator<string, Embedding<float>>`, we swapped one DI line in `EmbeddingExtensions.cs` to `MiniLmEmbeddingGenerator` (`Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` `BertTokenizer`, mean pooling, L2 normalization, 384 dimensions over the 22 MB `all-MiniLM-L6-v2` model in `models/`), which works identically on **iOS, Mac Catalyst, Android, and Windows**.
- **Live Demo on Agenda tab (switch city to Durban):**
  - Type **`football forecasts`** into `agenda-search` → *Developing A .NET Sports Predictor* ranks **#1**.
  - Type **`gradient descent`** into `agenda-search` → *Wait, what does Optimization really entail?* ranks **#1**.
  - Point to the terminal running `maui devflow logs --follow`:
    `[AI Search] Query 'football forecasts' in durban -> Top match: 'Developing A .NET Sports Predictor'`

### 3. Focused Source-Generated Tool Contexts: [`src/ConferenceApp/AI/Tools/`](../src/ConferenceApp/AI/Tools/)

- **Entry point:** [`ToolExtensions.cs`](../src/ConferenceApp/AI/Tools/ToolExtensions.cs) (`builder.AddAITools()`).
- **Declarative Tool Bindings (`Microsoft.Maui.AI.Attributes`):**
  - Instead of one giant tool class with every service in its constructor, tools are split into two focused, stateless `[AIToolSource]` classes:
    1. **[`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs) (`ConferenceDataToolContext.Default.Tools`):** Read-only conference tools (`search_sessions`, `get_session`, `get_speaker`, `list_sessions(TimeOnly start, TimeOnly end)`).
    2. **[`FavoritesTools.cs`](../src/ConferenceApp/AI/Tools/FavoritesTools.cs) (`FavoritesToolContext.Default.Tools`):** User favourites tools (`get_favorites`, `add_favorite`, and `remove_favorite`).
  - **`[FromServices]` on tool parameters:** Services only needed by a single tool (such as `[FromServices] SearchService search` on `SearchSessionsAsync`) are injected directly into the method parameter via `Microsoft.Extensions.DependencyInjection` + `functionInvocationServices: services` on `AppleIntelligenceChatClient`!
  - **Strongly-typed `TimeOnly` parameters:** `list_sessions(TimeOnly start, TimeOnly end)` uses `TimeOnly` directly (`AIJsonUtilities` generates `"format": "time"` and deserializes `"08:00"` / `"12:00"` with zero manual string parsing).

### 4. Context Providers, Agents, Workflows & Semantic Routing: [`src/ConferenceApp/AI/Agents/`](../src/ConferenceApp/AI/Agents/)

- **Entry point:** [`AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs) (`builder.AddAgents()`), which registers `builder.Services.AddKeyedChatClient("assistant", CreateAssistantRouter)`.
- **Three Composable `AIContextProvider`s (each in its own file):**
  - **[`UserContextProvider.cs`](../src/ConferenceApp/AI/Agents/UserContextProvider.cs):** Injects the selected city, event date, current conference time, venue name/address, and the user's saved favourites (with `[Topic]` tags).
  - **[`ScheduleContextProvider.cs`](../src/ConferenceApp/AI/Agents/ScheduleContextProvider.cs):** Injects the full day schedule of times, rooms, `[Topic]` tags, titles, and speakers for the current city so `ConferenceAssistant` and `SessionResearcher` can answer schedule/speaker questions immediately without redundant search calls.
  - **[`RagContextProvider.cs`](../src/ConferenceApp/AI/Agents/RagContextProvider.cs) (`Microsoft.Agents.AI.TextSearchProvider`):** Finds the top matching sessions by semantic similarity and injects each session's ID (`SourceLink`), title (`SourceName`), and 1-line summary (`Text`).
- **Multi-Turn Routing (`RoutingChatClient.Create` + `SemanticRoutingChatClient`) across 3 Non-Overlapping Routes:**
  - `AgentExtensions.CreateAssistantRouter` combines `SemanticRoutingChatClient` (embedding similarity router) with `RoutingChatClient.Create` (which inspects the full `RoutingContext.Messages` history, including `AuthorName`):
    - **[`ConferenceAssistantAgent.cs`](../src/ConferenceApp/AI/Agents/ConferenceAssistantAgent.cs) (Default Single `AIAgent`):** Uses `[userContext, scheduleContext, ragContext]` and `[.. ConferenceDataToolContext.Default.Tools, .. FavoritesToolContext.Default.Tools]` for fast factual Q&A, speaker bios, time-clash checks, and managing favourites.
    - **[`SessionDeepDiveWorkflow.cs`](../src/ConferenceApp/AI/Agents/SessionDeepDiveWorkflow.cs) (`BuildSequential` + `BuildConcurrent` Fan-Out):**
      - `SessionResearcher` calls `get_session` and `get_speaker` to gather the target talk's full abstract and speaker biography.
      - `AgentWorkflowBuilder.BuildConcurrent` then fans out in parallel to **`TakeawaysAgent`** (what to expect & 3 key takeaways) and **`QuestionsCoachAgent`** (things to think about & 2 Q&A questions to ask the speaker).
    - **[`TalkRecommendationWorkflow.cs`](../src/ConferenceApp/AI/Agents/TalkRecommendationWorkflow.cs) (`CreateGroupChatBuilderWith` + `RoundRobinGroupChatManager`):**
      - 6-agent panel (`Concierge` $\rightarrow$ `Timekeeper` $\rightarrow$ `DepthAdvocate` $\rightarrow$ `BreadthAdvocate` $\rightarrow$ `PracticalAdvocate` $\rightarrow$ `Moderator`):
        - **First turn:** `Concierge` checks the user's `Saved Favourites` (as a starting point) and asks the user to confirm or choose topics and any arrival/departure time constraints; `shouldTerminateFunc` pauses after `Concierge` (`IterationCount >= 1`).
        - **Follow-up turn:** When the user replies (e.g. `"AI and Cloud, and I have to leave before noon"`), `RoutingChatClient.Create` sees `lastAssistant?.AuthorName == "Concierge"` in `context.Messages` and routes back to `TalkRecommendationWorkflow`, where **`Timekeeper`** calls `list_sessions(start: 08:00, end: 12:00)` *first* so `DepthAdvocate`, `BreadthAdvocate`, `PracticalAdvocate`, and `Moderator` only debate talks within the attendee's time window!
- **Live Demo on Assistant tab:**
  - **Single Agent + Schedule Context + Tools:** Tap **`When is Matthew's talk?`** or **`Do my favourites clash?`**.
  - **Sequential + Concurrent Fan-Out Workflow (`BuildSequential` + `BuildConcurrent`):** Tap **`Prepare me for Matthew's talk: takeaways and questions`** and watch `SessionResearcher` gather the abstract & bio before `TakeawaysAgent` and `QuestionsCoachAgent` run concurrently!
  - **Multi-Turn Group Chat Workflow (`CreateGroupChatBuilderWith`):** Tap **`Can you suggest some talks for me?`**, reply to `Concierge` with **`AI and Cloud, leaving before noon`**, and watch `Timekeeper` filter the schedule via `list_sessions(08:00, 12:00)` before `DepthAdvocate`, `BreadthAdvocate`, `PracticalAdvocate`, and `Moderator` pitch and synthesize your morning lineup!
