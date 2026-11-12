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
| [`src/ConferenceApp/Views/AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) & [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml) | `<ContentPage x:Class="ConferenceApp.Views.AgendaPage" x:DataType="AgendaViewModel">`, `IsVisible="{!string.IsNullOrWhiteSpace(Topic)}"`, `Text="{ShowFavoritesOnly ? 'Showing: Favourites' : 'Showing: All Sessions'}"`, `IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"` | Three huge .NET 11 XAML features in one file: **1) Zero `xmlns:` declarations**, **2) C# Expressions in XAML** (`{!string.IsNullOrWhiteSpace(Topic)}`, ternaries, `AND`, string interpolation `{$'chat-approval-{Sessions[0].Id}'}`—eliminating all `HasXyz` ViewModel properties and `InverseBooleanConverter`s!), and **3) AOT-safe compiled `RelativeSource` bindings** (`AncestorType={x:Type AgendaViewModel}, x:DataType=AgendaViewModel`). |
| [`src/ConferenceApp/ViewModels/AgendaViewModel.cs`](../src/ConferenceApp/ViewModels/AgendaViewModel.cs) | `[ObservableProperty] public partial string SearchQuery { get; set; } = "";` | **C# partial properties + `CommunityToolkit.Mvvm` 8.4.2**: no backing fields, no `_searchQuery` naming rules, and full IDE/source-gen visibility. |
| [`data/ImportConference.cs`](../data/ImportConference.cs) & [`models/DownloadModels.cs`](../models/DownloadModels.cs) | `#:package HtmlAgilityPack@1.12.4` and `dotnet run data/ImportConference.cs` | **.NET single-file C# scripts (`dotnet run file.cs`)**: no `.csproj` needed for repo utilities—used here to snapshot the Sessionize/web data and download/verify the ONNX model. |
| [`src/ConferenceApp/MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) | `builder.AddChatClients(); builder.AddEmbeddings(); builder.AddAgents();` and `builder.AddMauiDevFlowAgent(...)` | Clean DI registration (`AddTransient<Page>().AddTransient<ViewModel>()`), three discoverable AI extension methods you can F12 into, plus **MAUI DevFlow Agent** registered in `#if DEBUG` with `EnableLayoutDiagnostics = true` and `EnableProfiler = true`. |
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

All in-app AI lives in **`src/ConferenceApp/AI/`** (5 focused files) plus `SearchService.cs` and `ChatService.cs`, discoverable directly from `MauiProgram.cs` (`builder.AddChatClients()`, `builder.AddEmbeddings()`, `builder.AddAgents()`). Keep `maui devflow logs --follow --no-json` running in a terminal (or open the **Logs** tab in the DevFlow Web Inspector) so the audience sees every model call, embedding query, RAG context injection, semantic route selection, tool invocation, and workflow step in real time.

### 1. Local Chat Clients: [`src/ConferenceApp/AI/ChatExtensions.cs`](../src/ConferenceApp/AI/ChatExtensions.cs)

- **Entry point:** `builder.AddChatClients()` in `MauiProgram.cs`.
- **`Microsoft.Extensions.AI` (`IChatClient`) across all platforms:**
  - `Microsoft.Maui.Essentials.AI` (`11.0.0-preview.8.26555.2`) provides local on-device `IChatClient` implementations for every platform:
    - **iOS / Mac Catalyst (`OperatingSystem.IsIOSVersionAtLeast(26)`):** `AppleIntelligenceChatClient(loggerFactory, functionInvocationServices: services)`
    - **Android (`OperatingSystem.IsAndroidVersionAtLeast(26)`):** `GeminiNanoChatClient().AsBuilder().UseFunctionInvocation(loggerFactory)`
    - **Windows (`OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100)`):** `WindowsAIChatClient().AsBuilder().UseFunctionInvocation(loggerFactory)`
  - Wraps each in a `ChatClientBuilder` pipeline with `.UseLogging(loggerFactory)` so every prompt, streaming response, and function call shows up automatically in `maui devflow logs`!
  - And `ChatExtensions.GetModelAvailabilityAsync()` checks `ChatModelStatus.Availability` (`Available`, `Disabled`, `Downloading`, `NotReady`) uniformly across all platforms.

### 2. Data Ingestion & Background Enrichment: [`src/ConferenceApp/AI/SessionEnrichmentService.cs`](../src/ConferenceApp/AI/SessionEnrichmentService.cs)

- **Why:** Session abstracts live as raw Markdown files (`data/conference/sessions/*.md`). Putting all 54 full abstracts into a small on-device model's context window overflows or degrades accuracy.
- **How (`Microsoft.Extensions.DataIngestion`):**
  - Reads each session Markdown file with `MarkdownReader` (`Microsoft.Extensions.DataIngestion.Markdig`) into an `IngestionDocument` and `IngestionChunk<string>`.
  - Runs two built-in enrichers backed by the local `IChatClient`:
    1. **`SummaryEnricher`** (`maxWordCount: 25`) — produces a 1-sentence summary for Agenda cards.
    2. **`ClassificationEnricher`** (`AI`, `Mobile`, `Web`, `Cloud`, `Data`, `IoT`, fallback `None`) — produces the topic chip on Agenda cards.
  - Persists to `session-enrichment.json` (and seeds from `Resources/Raw/session-enrichment.json` on first launch so cold start is instant, while still enriching any new/uncached talks in the background).
  - Publishes `SessionEnrichedMessage` via `WeakReferenceMessenger` so Agenda and Speaker cards update live.

### 3. Semantic Search (Apple NLEmbedding vs. ONNX MiniLM): [`src/ConferenceApp/AI/EmbeddingExtensions.cs`](../src/ConferenceApp/AI/EmbeddingExtensions.cs), [`src/ConferenceApp/AI/MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/MiniLmEmbeddingGenerator.cs) & [`src/ConferenceApp/Services/SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs)

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
  1. Ask: **`Is there a talk about keeping AI costs down?`** then follow up with **`Who's giving it, and when?`**
     - Point out `[AI RAG] TextSearchProvider injected 3 session abstracts...` in `maui devflow logs`—and how the follow-up combines both user questions (`TakeLast(2)`) to keep *The Hidden Cost of 'Just Call the LLM'* in context!
  2. Ask: **`What's Niels' background?`**
     - Watch `reply.Progress` show `"Reading biography for Niels Berglund..."` and the terminal log `[AI Tool] get_speaker('niels-berglund')`.
  3. Ask: **`Find me a talk about sports odds and favourite it`**
     - Watch the model use the RAG context / tools and call `add_favorite` (`"Waiting for your approval to add..."`).
     - Show that only the recommended talk card appears under the reply (because `search_sessions` passes `addCards: false` while `get_session` / `ChangeFavoriteAsync` adds the card).
     - Click **Approve** (`chat-approve-action`), then switch to **Agenda → Favourites** to show it saved.

### 5. Agents, RAG Middleware, Sequential Workflow & Semantic Routing: [`src/ConferenceApp/AI/AgentExtensions.cs`](../src/ConferenceApp/AI/AgentExtensions.cs)

- **Entry point:** `builder.AddAgents()` in `MauiProgram.cs`.
- **Agent Framework RAG Context Provider (`CreateRagProvider` in `AgentExtensions.cs`):**
  - Creates a `Microsoft.Agents.AI.TextSearchProvider` configured with `TextSearchBehavior.BeforeAIInvoke` and `SearchInputMessageFilter = messages => messages.Where(m => m.Role == ChatRole.User).TakeLast(2)`.
  - Passed into `ChatClientAgentOptions.AIContextProviders = [ragProvider]`.
  - Before every model turn, it queries `SearchService` using the latest 1–2 user messages, fetches the top 3 matching talk abstracts for the active city, and injects them into the in-flight request without polluting the saved chat history (`[AI RAG] TextSearchProvider injected 3 session abstracts...`).
- **Chaining Agents with `AsAIAgent` + `AgentWorkflowBuilder.BuildSequential`:**
  - Instead of writing custom workflow state classes or manual `IChatClient` wrappers, `AgentExtensions.CreateAssistantRouter` defines two focused agents with plain string instructions and chains them in one line:
    1. **`PreferencesAgent` (`localClient.AsAIAgent(name: "PreferencesAgent", instructions: ...)`):** Extracts the attendee's topic interests, arrival time, and departure time.
    2. **`ScheduleAgent` (`localClient.AsAIAgent(new ChatClientAgentOptions { Name = "ScheduleAgent", AIContextProviders = [ragProvider], ... })`):** Automatically receives `PreferencesAgent`'s output, runs `SessionRagProvider` to inject the matching session abstracts, and picks a conflict-free schedule with a 1-sentence reason per talk.
    3. **`AgentWorkflowBuilder.BuildSequential("DayPlanWorkflow", [preferencesAgent, scheduleAgent])`:** Chains the two agents sequentially.
    4. **`dayPlanWorkflow.AsAIAgent(name: "DayPlanner").AsIChatClient(allowNonChatClientAgents: true)`:** Turns the workflow directly into an `IChatClient` so `SemanticRoutingChatClient` can route to it!
- **Live Demo on Assistant tab:**
  - Type **`I'm into mobile and AI, leaving at 3`** in the chat input and tap **Plan my day** (`plan-my-day`)—or just type **`Build me a conflict-free schedule for today around AI, leaving at 3`** and hit **Send**.
  - Watch `PreferencesAgent` and `ScheduleAgent` run in sequence in `maui devflow logs`, review the recommended session cards and bulk approval card (`Add all N talks to favourites?`), tap **Approve**, and show the updated schedule in **Agenda → Favourites**.
