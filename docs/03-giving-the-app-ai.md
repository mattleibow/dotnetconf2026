# Part 3: Giving the App That Same AI — Code Walkthrough & Live Demo (20 mins)

Start at [`src/ConferenceApp/AI/ConferenceAIExtensions.cs`](../src/ConferenceApp/AI/ConferenceAIExtensions.cs). It organizes the entire in-app AI architecture into five self-contained folders that you can walk through in order:

```csharp
public static class ConferenceAIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        builder.AddChatClients();
        builder.AddEmbeddings();
        builder.AddDataEnrichment();
        builder.AddAITools();
        builder.AddAgents();

        return builder;
    }
}
```

Keep `maui devflow logs --follow --no-json` visible in a terminal (or open the **Logs** tab in the DevFlow Web Inspector). Here is what the real running app logs look like during the demo:

```text
[AI] Loading ONNX embedding model 'all-MiniLM-L6-v2' (384 dimensions)...
LoggingEmbeddingGenerator: GenerateAsync generated 51 embedding(s).
[AI] Indexed 51 session abstracts into VectorStoreCollection<string, Session>.
[AI] Enriching session durban-agents-building-agentic-apps-ai-goes-mobile-with-net-maui with SummaryEnricher + KeywordEnricher...
[AI Search] Query 'football forecasts' in durban -> Top match: 'Developing A .NET Sports Predictor' (5 results)
[AI RAG] Injected 3 matching sessions in Durban for 'When is Matthew's talk?'
[AI Tool] get_session('durban-agents-building-agentic-apps-ai-goes-mobile-with-net-maui')
[AI Chat] Completed reply (563 chars, 1 session cards).
```

---

## 1. Local On-Device Chat Clients (`AI/ChatClients/` — ~3 mins)

Open [`src/ConferenceApp/AI/ChatClients/ChatExtensions.cs`](../src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) and [`UnavailableChatClient.cs`](../src/ConferenceApp/AI/ChatClients/UnavailableChatClient.cs).

`Microsoft.Maui.Essentials.AI` (`11.0.0-preview.8.26555.2`) provides local on-device `IChatClient` implementations for each platform: `AppleIntelligenceChatClient` on iOS / Mac Catalyst 26+, `GeminiNanoChatClient` on Android API 26+, and `WindowsAIChatClient` on Windows 10.0.26100+.

```csharp
#if IOS || MACCATALYST
if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
{
    return new AppleIntelligenceChatClient(loggerFactory: loggerFactory, functionInvocationServices: services)
        .AsBuilder()
        .UseLogging(loggerFactory)
        .Build();
}
#elif ANDROID
if (OperatingSystem.IsAndroidVersionAtLeast(26))
{
    return new OrderedFailoverChatClient(
        [
            .. AndroidCandidateModels.Select(modelId =>
                new GeminiNanoChatClient()
                    .AsBuilder()
                    .ConfigureOptions(options => options.ModelId = modelId)
                    .Build())
        ])
        .AsBuilder()
        .UseFunctionInvocation(loggerFactory)
        .UseLogging(loggerFactory)
        .Build(services);
}
#endif
return new UnavailableChatClient();
```

Point out three important details here:

First, notice how `AppleIntelligenceChatClient` takes `functionInvocationServices: services` directly in its constructor (without `.UseFunctionInvocation()`), because Apple's native Foundation Models runtime executes tools inside its native bridge and uses `functionInvocationServices` to resolve `[FromServices]` parameters. On Android and Windows, `.UseFunctionInvocation(loggerFactory)` + `.Build(services)` adds the managed `Microsoft.Extensions.AI` tool-calling loop.

Second, on Android, **`OrderedFailoverChatClient`** (`Microsoft.Extensions.AI` 11.0.0) chains the Gemini Nano profiles (`gemini-nano-preview-fast`, `gemini-nano-preview-full`, `gemini-nano-full`, `gemini-nano-fast`) in order. If a profile isn't downloaded or doesn't support a capability (such as tool calling), `OrderedFailoverChatClient` automatically advances to the next profile.

Third, if the current OS or device doesn't support local AI (for example an Android emulator where Google AICore isn't installed), `CreateLocalChatClient` falls back to [`UnavailableChatClient`](../src/ConferenceApp/AI/ChatClients/UnavailableChatClient.cs) and `GetModelAvailabilityAsync()` catches platform status exceptions to return `ChatModelAvailability.NotSupported`. That means `IChatClient`, enrichers, and agents **always resolve cleanly from DI**, while `ChatPage` displays the `chat-unavailable` banner (`"On-device chat isn't available on this device right now. You can still browse the agenda and favourites. No cloud service is used."`) and disables chat inputs.

---

## 2. Semantic Embeddings & Declarative Vector Store (`AI/Embeddings/` — ~4 mins)

Open [`Data/Session.cs`](../src/ConferenceApp/Data/Session.cs), [`AI/Embeddings/EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs), [`AI/Embeddings/SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs), and [`Services/SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs).

### Declarative Vector Mapping on `Session`

Instead of manually generating float arrays or maintaining a separate vector DTO, `Session` is annotated directly with `Microsoft.Extensions.VectorData` attributes:

```csharp
public class Session
{
    [VectorStoreKey]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string CityId { get; set; } = "";
    ...
    [JsonIgnore]
    [VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string EmbeddingText => $"{Title}\n\n{Description}";
}
```

Because `[VectorStoreVector(384)]` is placed on the `string EmbeddingText` property, `VectorStoreCollection<string, Session>` automatically invokes the DI-registered `IEmbeddingGenerator<string, Embedding<float>>` whenever records are upserted or searched, and `[VectorStoreData(IsIndexed = true)]` on `CityId` enables strongly-typed LINQ filtering (`Filter = s => s.CityId == cityId`).

### DI Registration & Background Indexing

[`EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs) registers the ONNX `MiniLmEmbeddingGenerator` with `.UseLogging()`, the in-memory vector store, and the strongly-typed `Session` collection:

```csharp
builder.Services.AddSingleton<MiniLmEmbeddingGenerator>();
builder.Services
    .AddEmbeddingGenerator(sp => sp.GetRequiredService<MiniLmEmbeddingGenerator>())
    .UseLogging();

builder.Services.AddInMemoryVectorStore();
builder.Services.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions");
builder.Services.AddSingleton<BaseBackgroundService, SessionIndexingService>();
```

At startup, [`SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs) loads the 51 talk sessions from `ConferenceDb` (`s.IsTalk && !string.IsNullOrWhiteSpace(s.Description)`) and indexes them in two lines:

```csharp
await sessionVectors.EnsureCollectionExistsAsync();
await sessionVectors.UpsertAsync(sessions);
```

And [`SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs) performs hybrid search—matching literal title/description/speaker substrings first, then appending semantic vector matches from `VectorStoreCollection<string, Session>` filtered by `CityId`:

```csharp
await foreach (var match in sessionVectors.SearchAsync(
    trimmed,
    top: SemanticResultLimit,
    new() { Filter = s => s.CityId == cityId }))
{
    if (match.Score >= MinimumSemanticScore && !results.Contains(match.Record))
        results.Add(match.Record);
}
```

### Why ONNX `all-MiniLM-L6-v2` instead of Apple `NLEmbeddingGenerator`?

When we first tested Apple's built-in `NLEmbeddingGenerator` (a 512-dim OS word/sentence table rather than a transformer retrieval model), semantic search missed domain synonyms. Because `VectorStoreCollection<string, Session>` depends only on `IEmbeddingGenerator<string, Embedding<float>>`, swapping one DI line to [`MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/Embeddings/MiniLmEmbeddingGenerator.cs) (`Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` `BertTokenizer` over the 22 MB `all-MiniLM-L6-v2` model in `models/`) fixed retrieval across iOS, Mac Catalyst, Android, and Windows—even on an Android emulator without Gemini Nano!

| Durban Search Query | Target Session | Apple `NLEmbeddingGenerator` | ONNX `MiniLmEmbeddingGenerator` |
|---|---|---|---|
| `"football forecasts"` | *Developing A .NET Sports Predictor* | Ranked **#5** | Ranked **#1** |
| `"gradient descent"` | *Wait, what does Optimization really entail?* | Ranked **#4** | Ranked **#1** |

### Live Demo on the Agenda Tab (Durban)

Switch the city to **Durban**, type **`football forecasts`** and **`gradient descent`** into `agenda-search`, and point out `[AI Search]` and `LoggingEmbeddingGenerator` in `maui devflow logs`.

---

## 3. Data Ingestion & Background Enrichment (`AI/Data/` — ~3 mins)

Open [`src/ConferenceApp/AI/Data/DataExtensions.cs`](../src/ConferenceApp/AI/Data/DataExtensions.cs), [`SessionSummaryEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionSummaryEnrichmentService.cs), and [`SessionTopicEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionTopicEnrichmentService.cs).

Putting all 51 full Markdown abstracts into a compact on-device model's context window overflows or degrades accuracy. Using `Microsoft.Extensions.DataIngestion` (`11.0.0-preview.1.26560.3`), `DataExtensions.cs` registers shared `EnricherOptions` (backed by the local `IChatClient`) along with two `BaseBackgroundService` implementations that `App.xaml.cs` starts automatically via `IEnumerable<BaseBackgroundService>`:

- **[`SessionSummaryEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionSummaryEnrichmentService.cs)** uses `SummaryEnricher(enricherOptions, maxWordCount: 24)` to populate `session.Summary`.
- **[`SessionTopicEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionTopicEnrichmentService.cs)** uses `KeywordEnricher(enricherOptions, predefinedKeywords: Topics, maxKeywords: 3, confidenceThreshold: 0.7)` across the 11 conference topics (`AI`, `Agents`, `Cloud`, `Data`, `Mobile`, `Web`, `IoT`, `Architecture`, `Security`, `DevOps`, `Gaming`) to populate `session.Topics`.

Because `Session` inherits `ObservableObject` and `Summary` and `Topics` (`IReadOnlyList<string>`) are `[ObservableProperty]`s, XAML bindings (`BindableLayout.ItemsSource="{Binding Topics}"`) update each card in place automatically without rebuilding or resetting the `CollectionView`.

---

## 4. Source-Generated AI Tool Contexts (`AI/Tools/` — ~3 mins)

Open [`src/ConferenceApp/AI/Tools/ToolExtensions.cs`](../src/ConferenceApp/AI/Tools/ToolExtensions.cs), [`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs), and [`FavoritesTools.cs`](../src/ConferenceApp/AI/Tools/FavoritesTools.cs).

Using `Microsoft.Maui.AI.Attributes`, a one-line `[AIToolSource]` partial class source-generates an AOT-friendly `AIToolContext` (`Default.Tools`) with zero reflection. Tools are split into two stateless contexts so read-only research agents never receive state-mutating tools:

```csharp
[AIToolSource(typeof(ConferenceDataTools))]
public partial class ConferenceDataToolContext : AIToolContext;
```

| Tool Context | Tools | Purpose |
|---|---|---|
| **`ConferenceDataToolContext`** | `search_sessions`, `get_session`, `get_speaker` | Read-only schedule, abstract, and speaker bio lookup. |
| **`FavoritesToolContext`** | `get_favorites`, `add_favorite`, `remove_favorite` | Reads and mutates saved favourites in `UserPreferencesService` (visible live in DevFlow's **Storage** tab). |

Point out Method-Level DI with `[FromServices]` in [`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs):

### Method-Level DI with `[FromServices]`

`ConferenceDataTools` doesn't inject `SearchService` in its constructor just for one method. Instead, `SearchSessionsAsync` uses `[FromServices] SearchService search` right on the tool parameter, resolved automatically from the `IServiceProvider` configured on the chat client:

```csharp
[ExportAIFunction("search_sessions")]
[Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
public async Task<string> SearchSessionsAsync(
    [Description("Topic, title, or speaker to search for.")] string query,
    [FromServices] SearchService search,
    CancellationToken cancellationToken = default)
```

---

## 5. Context Providers, Multi-Agent Workflows & Semantic Routing (`AI/Agents/` — ~4 mins)

Open [`src/ConferenceApp/AI/Agents/AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs) and the files in `AI/Agents/`.

### Keyed `AIContextProvider` Singletons

In `AgentExtensions.AddAgents()`, four composable context providers are registered as keyed `AIContextProvider` singletons so each agent or workflow can request only the context slices it needs:

```csharp
builder.Services.AddKeyedSingleton<AIContextProvider, UserContextProvider>("user");
builder.Services.AddKeyedSingleton<AIContextProvider, ScheduleContextProvider>("schedule");
builder.Services.AddKeyedSingleton<AIContextProvider, AvailableSessionsContextProvider>("available-sessions");
builder.Services.AddKeyedSingleton<AIContextProvider>("rag", (services, _) => RagContextProvider.Create(services));
builder.Services.AddKeyedChatClient("assistant", CreateAssistantRouter);
```

| Key | File | What it injects into `AIContext` |
|---|---|---|
| `"user"` | [`UserContextProvider.cs`](../src/ConferenceApp/AI/Agents/UserContextProvider.cs) | Active city, event date, current conference time (`08:30`), venue name/address, and saved favourites with `[Topics]` tags. |
| `"schedule"` | [`ScheduleContextProvider.cs`](../src/ConferenceApp/AI/Agents/ScheduleContextProvider.cs) | Full day schedule (`Time \| Track [Topics] \| Title (Speakers)`) for the active city so agents answer schedule and clash questions without extra tool calls. |
| `"available-sessions"` | [`AvailableSessionsContextProvider.cs`](../src/ConferenceApp/AI/Agents/AvailableSessionsContextProvider.cs) | Candidate talks in the active city excluding sessions already saved in the attendee's favourites, used by the recommendation panel. |
| `"rag"` | [`RagContextProvider.cs`](../src/ConferenceApp/AI/Agents/RagContextProvider.cs) | `TextSearchProvider` (`BeforeAIInvoke`) that filters to the latest user message, finds the top 3 talks via `SearchService`, and injects their IDs, titles, and AI-enriched summaries (`session.Summary`). |

Notice how the whole pipeline connects: `SessionEnrichmentService` pre-computes compact `session.Summary` strings $\rightarrow$ `RagContextProvider` injects the top 3 summaries into context $\rightarrow$ the agent calls `get_session` / `get_speaker` only when it needs a full abstract or bio. Every agent is also configured with `UseProvidedChatClientAsIs = true`, `Temperature = 0`, and tight `MaxOutputTokens` budgets tailored for an on-device model.

### Three Non-Overlapping Agent & Workflow Architectures

#### Default Assistant ([`ConferenceAssistantAgent.cs`](../src/ConferenceApp/AI/Agents/ConferenceAssistantAgent.cs))
A single `AIAgent` configured with `[userContext, scheduleContext, ragContext]` and both tool contexts (`ConferenceDataToolContext` + `FavoritesToolContext`) for fast schedule lookups, speaker bios, clash checks, and adding/removing favourites.

#### Session Deep-Dive Workflow ([`SessionDeepDiveWorkflow.cs`](../src/ConferenceApp/AI/Agents/SessionDeepDiveWorkflow.cs) — `BuildSequential` + `BuildConcurrent`)
Combines sequential and concurrent orchestration using `Microsoft.Agents.AI.Workflows`. First, `SessionResearcher` calls `get_session` and `get_speaker` to gather the target talk's full abstract and speaker biography into a factual brief. Next, `AgentWorkflowBuilder.BuildConcurrent` fans out in parallel to **`TakeawaysAgent`** (what to expect & 3 key technical takeaways) and **`QuestionsCoachAgent`** (2 things to think about & 2 smart Q&A questions for the speaker):

```csharp
var parallelAnalysis = AgentWorkflowBuilder
    .BuildConcurrent("DeepDiveParallelAnalysis", [takeawaysAgent, questionsCoachAgent])
    .AsAIAgent(name: "DeepDiveParallelAnalysis");

return AgentWorkflowBuilder.BuildSequential(
    "SessionDeepDiveWorkflow",
    [sessionResearcher, parallelAnalysis]);
```

#### Talk Recommendation & Debate Workflow ([`TalkRecommendationWorkflow.cs`](../src/ConferenceApp/AI/Agents/TalkRecommendationWorkflow.cs) — `CreateGroupChatBuilderWith`)
A multi-turn 5-agent group chat managed by `RoundRobinGroupChatManager` with zero tool calls—relying purely on `UserContextProvider` and `AvailableSessionsContextProvider`:

```text
Turn 1 (Pause):  Concierge ──► (pauses at IterationCount >= 1 for user confirmation)
Turn 2 (Debate): Concierge ──► DepthAdvocate ──► BreadthAdvocate ──► PracticalAdvocate ──► Moderator
```

On **Turn 1**, `Concierge` checks the attendee's `Saved Favourites` (as a starting point) and asks them to confirm or pick their topics; the custom `shouldTerminateFunc` pauses the group chat after `Concierge` (`IterationCount >= 1`) so the user can reply.

On **Turn 2**, `Concierge` locks in the confirmed and avoided topics, `DepthAdvocate`, `BreadthAdvocate`, and `PracticalAdvocate` each pitch 2–3 distinct talks from `AvailableSessionsContextProvider` (which automatically excludes already-favourited sessions), and `Moderator` curates a chronological shortlist of 4–6 recommended talks for the attendee to choose from.

### Two-Layer Routing: `RoutingChatClient.Create` + `SemanticRoutingChatClient` ([`AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs))

Each workflow is adapted via `.AsAIAgent(...).AsIChatClient(allowNonChatClientAgents: true)` so workflows are plain `IChatClient`s to the rest of the app. `CreateAssistantRouter` combines two routers from `Microsoft.Extensions.AI` behind the keyed `"assistant"` `IChatClient`:

1. **Stateful multi-turn check (`RoutingChatClient.Create`):** Inspects `context.Messages` first—if the previous assistant message came from `AuthorName == "Concierge"`, it routes the user's reply straight back into `TalkRecommendationWorkflow`.
2. **Stateless utterance similarity (`SemanticRoutingChatClient`):** Reuses the same on-device ONNX `MiniLmEmbeddingGenerator` to compare the prompt against `SessionDeepDiveWorkflow.Utterances` and `TalkRecommendationWorkflow.Utterances` (`scoreThreshold: 0.58f`), falling back to `assistantClient`:

```csharp
var semanticRouter = new SemanticRoutingChatClient(
    services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
    new Dictionary<IChatClient, IReadOnlyList<string>>
    {
        [deepDiveClient] = SessionDeepDiveWorkflow.Utterances,
        [recommendationClient] = TalkRecommendationWorkflow.Utterances
    },
    defaultClient: assistantClient,
    scoreThreshold: 0.58f,
    topK: 1,
    leaveOpen: true);

return RoutingChatClient.Create((context, _) =>
{
    var lastAssistant = context.Messages.LastOrDefault(m =>
        m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.AuthorName));
    if (lastAssistant?.AuthorName == "Concierge")
        return ValueTask.FromResult(recommendationClient);

    return ValueTask.FromResult<IChatClient>(semanticRouter);
});
```

---

## 6. The UI Side of Multi-Agent Streaming ([`Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs) — ~3 mins)

Open [`src/ConferenceApp/Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs) to show how a mobile UI makes multi-agent workflows feel fast and readable:

### Progress Status & Hiding Internal Handoff Steps
Internal research/handoff turns (`SessionResearcher` and `Concierge`'s 2-line handoff on Turn 2) update `reply.Progress` (bound to `chat-progress` in `ChatPage.xaml`, e.g. *"SessionResearcher: reading session abstract and speaker bio..."*) instead of dumping raw intermediate handoff state into the chat bubble.

### De-Interleaving Concurrent Streams (`BuildConcurrent`)
When `TakeawaysAgent` and `QuestionsCoachAgent` run in parallel via `BuildConcurrent`, both stream tokens at the same time. `ChatService` streams `TakeawaysAgent` live into the bubble under **`**Key Takeaways:**`** while buffering `QuestionsCoachAgent` into a `StringBuilder`, then appends **`**Things to Think About & Ask:**`** cleanly once `TakeawaysAgent` finishes.

### Preserving `AuthorName` Across Turns & Attaching Tappable `Session` Cards
At the end of the stream, `ChatService` stores `updates.ToChatResponse().Messages` on `reply.ResponseMessages` so each message's `AuthorName` (`"Concierge"`, `"Moderator"`, etc.) is preserved in `history` for `RoutingChatClient` on the next turn. Finally, `AddReferencedSessionsAsync` scans the reply for any mentioned talk titles or IDs in the current city and populates `reply.Sessions`, which `ChatPage.xaml` renders as tappable `chat-session-card-{Id}` cards that navigate straight to `SessionPage`!

---

## Live Demo Script on the Assistant Tab

When you open the **Assistant** tab with an empty chat, `<CollectionView.EmptyView>` shows the four starter suggestions right in the center of the screen:

1. **Default Assistant + Schedule Context + Tools:** Tap **`When is Matthew's talk?`** (or **`Do my favourites clash?`**). Point out `[AI RAG]` in the logs and tap the resulting `chat-session-card-*` card underneath the reply.
2. **Sequential + Concurrent Fan-Out Workflow (`BuildSequential` + `BuildConcurrent`):** Tap **`New chat`**, then tap **`Prepare me for Matthew's talk: takeaways and questions`**. Watch the progress indicator show `SessionResearcher` reading the abstract and speaker bio (`[AI Tool] get_session` + `get_speaker` in logs), followed by `TakeawaysAgent` and `QuestionsCoachAgent` running in parallel!
3. **Multi-Turn Group Chat Workflow (`CreateGroupChatBuilderWith`):** Tap **`New chat`**, then tap **`Can you suggest some talks for me?`**. When `Concierge` asks to confirm your topics, reply with **`AI and Cloud, no Web`**. Watch `DepthAdvocate`, `BreadthAdvocate`, `PracticalAdvocate`, and `Moderator` debate candidate sessions from `AvailableSessionsContextProvider` (automatically excluding already-favourited talks) and produce tappable session cards!
