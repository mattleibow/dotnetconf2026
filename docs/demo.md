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
| [`src/ConferenceApp/Views/AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) | `<ContentPage x:Class="ConferenceApp.Views.AgendaPage" x:DataType="AgendaViewModel">` and `AncestorType={x:Type AgendaViewModel}, x:DataType=AgendaViewModel` | Look at line 2: **no `xmlns` or `xmlns:x` declarations at all!** And inside `DataTemplate`, .NET 11 supports **AOT-safe compiled `RelativeSource` bindings** directly to the ViewModel (`AncestorType={x:Type AgendaViewModel}`) instead of untyped `BindingContext.` paths. |
| [`src/ConferenceApp/ViewModels/AgendaViewModel.cs`](../src/ConferenceApp/ViewModels/AgendaViewModel.cs) | `[ObservableProperty] public partial string SearchQuery { get; set; } = "";` | **C# partial properties + `CommunityToolkit.Mvvm` 8.4.2**: no backing fields, no `_searchQuery` naming rules, and full IDE/source-gen visibility. |
| [`data/ImportConference.cs`](../data/ImportConference.cs) & [`models/DownloadModels.cs`](../models/DownloadModels.cs) | `#:package HtmlAgilityPack@1.12.4` and `dotnet run data/ImportConference.cs` | **.NET single-file C# scripts (`dotnet run file.cs`)**: no `.csproj` needed for repo utilities—used here to snapshot the Sessionize/web data and download/verify the ONNX model. |
| [`src/ConferenceApp/MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) | `builder.AddConferenceAI();` and `builder.AddMauiDevFlowAgent(...)` | Clean DI registration (`AddTransient<Page>().AddTransient<ViewModel>()`), plus **MAUI DevFlow Agent** registered in `#if DEBUG` with `EnableLayoutDiagnostics = true` and `EnableProfiler = true`. |
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

All in-app AI lives in **`src/ConferenceApp/AI/`** (5 focused files) plus `SearchService.cs` and `ChatService.cs`. Keep `maui devflow logs --follow --no-json` running in a terminal (or open the **Logs** tab in the DevFlow Web Inspector) so the audience sees every model call, embedding query, tool invocation, and workflow step in real time.

### 1. Wiring & Logging: [`src/ConferenceApp/AI/AIExtensions.cs`](../src/ConferenceApp/AI/AIExtensions.cs)

- **Single entry point:** `builder.AddConferenceAI()` called from `MauiProgram.cs`.
- **`Microsoft.Extensions.AI` abstraction (`IChatClient` & `IEmbeddingGenerator`):**
  - On iOS / Mac Catalyst 26+, registers `AppleIntelligenceChatClient` from `Microsoft.Maui.Essentials.AI` (`MAUIAI0001`).
  - Wraps it in a `ChatClientBuilder` pipeline:
    ```csharp
    new AppleIntelligenceChatClient(sp)
        .AsBuilder()
        .UseLogging(sp.GetRequiredService<ILoggerFactory>())
        .Build()
    ```
  - Because `UseLogging` is attached and DevFlow captures `ILogger`, every prompt, streaming response, and function call shows up automatically in `maui devflow logs`!
  - Notice `new AppleIntelligenceChatClient(sp)` passes `IServiceProvider`: on Apple platforms, native Foundation Models execute tools inside the client using `functionInvocationServices`.

### 2. Data Ingestion & Background Enrichment: [`src/ConferenceApp/AI/SessionEnrichmentService.cs`](../src/ConferenceApp/AI/SessionEnrichmentService.cs)

- **Why:** Session abstracts live as raw Markdown files (`data/conference/sessions/*.md`). Putting all 54 full abstracts into a small on-device model's context window overflows or degrades accuracy.
- **How (`Microsoft.Extensions.DataIngestion`):**
  - Reads each session Markdown file with `MarkdownReader` (`Microsoft.Extensions.DataIngestion.Markdig`) into an `IngestionDocument` and `IngestionChunk<string>`.
  - Runs two built-in enrichers backed by the local `IChatClient`:
    1. **`SummaryEnricher`** (`maxWordCount: 25`) — produces a 1-sentence summary for Agenda cards.
    2. **`ClassificationEnricher`** (`AI`, `Mobile`, `Web`, `Cloud`, `Data`, `IoT`, fallback `None`) — produces the topic chip on Agenda cards.
  - Persists to `session-enrichment.json` (and seeds from `Resources/Raw/session-enrichment.json` on first launch so cold start is instant, while still enriching any new/uncached talks in the background).
  - Publishes `SessionEnrichedMessage` via `WeakReferenceMessenger` so Agenda and Speaker cards update live.

### 3. Semantic Search (Apple NLEmbedding vs. ONNX MiniLM): [`src/ConferenceApp/AI/MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/MiniLmEmbeddingGenerator.cs) & [`src/ConferenceApp/Services/SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs)

- **Ingestion into Vector Store (`SessionEnrichmentService.cs` lines 48–60):**
  - Uses `VectorStoreWriter<string>` to write all 54 chunks (title + full abstract + `cityid` metadata) into `InMemoryVectorStore` (`CommunityToolkit.VectorData.InMemory`) using `DistanceFunction.CosineSimilarity`.
- **The Apple `NLEmbeddingGenerator` vs. ONNX `all-MiniLM-L6-v2` story:**
  - When we first wired `NLEmbeddingGenerator` (Apple's built-in 512-dim sentence embeddings), searching Durban for:
    - `"football forecasts"` ranked *Developing A .NET Sports Predictor* **#5**.
    - `"gradient descent"` ranked *Wait, what does Optimization really entail?* **#4**.
  - Why? `NLEmbedding` is a lightweight OS word/sentence table, not a transformer retrieval model.
  - Because `SearchService` and `VectorStoreWriter` depend only on `IEmbeddingGenerator<string, Embedding<float>>`, we swapped one DI line in `AIExtensions.cs` to `MiniLmEmbeddingGenerator`:
    - Uses `Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` (`BertTokenizer`, mean pooling, L2 normalization, 384 dimensions) over the 22 MB `all-MiniLM-L6-v2` model bundled in `models/`.
    - Works identically on **iOS, Mac Catalyst, Android, and Windows**.
- **Live Demo on Agenda tab (switch city to Durban):**
  - Type **`football forecasts`** into `agenda-search` → *Developing A .NET Sports Predictor* ranks **#1** (even though the title only says "Sports Predictor" and the abstract talks about predicting sports odds!).
  - Type **`gradient descent`** into `agenda-search` → *Wait, what does Optimization really entail?* ranks **#1**.
  - Point to the terminal running `maui devflow logs --follow`:
    `[AI Search] Query 'football forecasts' in durban -> Top match: 'Developing A .NET Sports Predictor'`

### 4. Source-Generated Tools & Human-in-the-Loop Approvals: [`src/ConferenceApp/AI/ConferenceAssistantTools.cs`](../src/ConferenceApp/AI/ConferenceAssistantTools.cs) & [`src/ConferenceApp/Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs)

- **Compact Context + On-Demand Retrieval (`ChatService.GetSystemInstructionsAsync`):**
  - The system prompt only gives the model 1 line per session (`time | title | speakers | room`). It doesn't bloat the context with 54 abstracts or 38 speaker bios.
- **Declarative Tool Bindings (`Microsoft.Maui.AI.Attributes`):**
  - `[AIToolSource]` on `ConferenceAssistantTools` + `[AITool]` on methods + `[AIToolContext]` on `ConferenceToolContext` source-generates `AIFunction` definitions (`ConferenceToolContext.Default.Tools`) with zero reflection.
  - **Read-only tools:** `search_sessions` (calls `SearchService` for RAG!), `get_session` (loads full abstract), `get_speaker` (loads speaker bio), `list_sessions` (time range filter), `get_favorites`.
  - **Navigation tools:** `open_session`, `open_speaker`, `open_event` (drives `Shell.Current.GoToAsync`).
  - **Approval-gated mutation tools (`[AITool(..., ApprovalRequired = true)]`):** `add_favorite` and `remove_favorite`.
- **How Human-in-the-Loop Approval Works (`ChangeFavoriteAsync` & `Models/ChatApproval.cs`):**
  - Before mutating `UserPreferencesService`, `ChangeFavoriteAsync` pushes a `ChatApproval` object (`TaskCompletionSource<bool>`) into `reply.Approvals` on the main thread and `await approval.WaitAsync(cancellationToken)`.
  - The tool call pauses inside the chat bubble while the user sees **Approve** / **Deny** buttons!
  - Only when the user taps **Approve** does `preferences.SetFavorite(...)` run and return `"Added '...' to favourites."` to the model.
- **Live Demo on Assistant tab (in Durban):**
  1. Ask: **`What's Niels' background?`**
     - Watch `reply.Progress` show `"Running tool: get_speaker..."` and the terminal log `[AI Tool] get_speaker(speaker: 'Niels')`.
  2. Ask: **`Find me a talk about sports odds and favourite it`**
     - Watch the model chain `search_sessions` → `get_session` → `add_favorite` (`"Waiting for your approval to add..."`).
     - Show that only the recommended talk card appears under the reply (because `search_sessions` passes `addCards: false` while `get_session` adds the card).
     - Click **Approve** (`chat-approve-action`), then switch to **Agenda → Favourites** to show it saved.

### 5. Multi-Step Agent Workflow ("Plan my day"): [`src/ConferenceApp/AI/DayPlanService.cs`](../src/ConferenceApp/AI/DayPlanService.cs)

- **Why a Workflow (`Microsoft.Agents.AI.Workflows`) instead of a single Giant Prompt?**
  - Small on-device models are great at focused extraction and short writing, but bad at combinatorial time-slot constraint solving across 20 sessions.
  - `DayPlanService.BuildWorkflow` uses `InProcessExecution.RunAsync` with a 4-stage pipeline mixing **AI agents** and **deterministic C# executors**:
    1. **`PreferencesExecutor` (`ChatClientAgent` with structured output `DayPreferences`):** Extracts `Interests`, `Track`, `ArriveAfter`, `LeaveBy` from natural language (plus a deterministic C# regex fallback in `ApplyExplicitTimes` so the model never invents times).
    2. **`CandidatesExecutor` (deterministic C# + `SearchService`):** Runs semantic search per interest and scores talks by keyword, semantic match, track, and existing favourites.
    3. **`ScheduleExecutor` (deterministic C# dynamic programming):** Weighted interval scheduling (`BuildNonOverlappingSchedule`) guarantees zero overlapping time slots within the user's arrival/departure window.
    4. **`ExplanationsExecutor` (`Task.WhenAll` concurrent `ChatClientAgent`s):** Runs one lightweight local agent per chosen talk to write a 12-word personalized "Why it fits you" reason.
- **Live Demo on Assistant tab:**
  - Type **`I'm into mobile and AI, leaving at 3`** in the chat input and tap **Plan my day** (`plan-my-day`).
  - Watch the live progress steps in the chat bubble (`Workflow 1/4` → `4/4`) and the concurrent `TalkFit` agent invocations in `maui devflow logs`.
  - Review the conflict-free schedule cards and single bulk approval card (`Add all N talks to favourites?`), tap **Approve**, and show the updated schedule in **Agenda → Favourites**.
