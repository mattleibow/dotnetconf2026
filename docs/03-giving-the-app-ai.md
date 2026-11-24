# Part 3: Giving the App That Same AI — Code Walkthrough & Live Demo (20 mins)

The AI layers are merged, so walk through the code on `main`. The [PR stack](../README.md#demo-branches) shows how it was built up.

Start at [`AI/ConferenceAIExtensions.cs`](../src/ConferenceApp/AI/ConferenceAIExtensions.cs). One entry point organizes five areas:

```csharp
public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
{
    builder.AddChatClients();
    builder.AddEmbeddings();
    builder.AddDataEnrichment();
    builder.AddAITools();
    builder.AddAgents();
    return builder;
}
```

Keep the DevFlow Inspector's **Logs** tab or a terminal visible:

```sh
maui devflow logs --follow --no-json
```

Look for model/index startup, `LoggingEmbeddingGenerator`, `[AI Search]`, `[AI RAG]`, `[AI Tool]`, and `[AI Chat]`.

## 1. Local on-device chat clients (`AI/ChatClients/` — ~3 mins)

Open [`ChatExtensions.cs`](../src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) and [`UnavailableChatClient.cs`](../src/ConferenceApp/AI/ChatClients/UnavailableChatClient.cs).

MAUI Essentials AI supplies `IChatClient` implementations. The registration checks OS support, and `GetModelAvailabilityAsync()` checks model readiness before background enrichment. Chat has no up-front check: when no model can answer, the failover reaches `UnavailableChatClient`, which replies with a friendly message.

| Platform | Client and invocation |
|---|---|
| iOS / Mac Catalyst 26+ | `AppleIntelligenceChatClient`, with `loggerFactory` and `functionInvocationServices: services`; Apple's native Foundation Models bridge invokes tools |
| Supported Android devices (API check 26+) | `GeminiNanoChatClient` profiles behind `OrderedFailoverChatClient`, then `.UseFunctionInvocation(loggerFactory)` and `.Build(services)` |
| Windows | `WindowsAIChatClient` for plain requests; requests with tools route to the bundled [`QwenChatClient`](../models/chat/README.md) with managed function invocation |
| Unsupported / unavailable | `UnavailableChatClient` replies with a friendly message on unsupported platforms; if every client fails, the reply says the assistant can't answer right now and the error is logged |

Apple passes DI services into the native client rather than adding the managed function-invocation loop:

```csharp
new AppleIntelligenceChatClient(
        loggerFactory: loggerFactory,
        functionInvocationServices: services)
    .AsBuilder()
    .UseLogging(loggerFactory)
    .Build();
```

Android tries `gemini-nano-preview-fast`, `gemini-nano-preview-full`, `gemini-nano-full`, and `gemini-nano-fast` in order. `OrderedFailoverChatClient` can move to another profile when a requested capability is unsupported.

Check [device requirements](../README.md#on-device-ai) before the demo. The [gateway runbook](04-ai-gateway.md) adds server-first chat in front of these clients.

## 2. Semantic embeddings and vector search (`AI/Embeddings/` — ~4 mins)

Open [`Data/Session.cs`](../src/ConferenceApp/Data/Session.cs), [`EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs), [`SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs), and [`SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs).

### Declarative mapping

The stack annotates `Session` directly; there is no parallel vector DTO:

```csharp
[VectorStoreKey]
public string Id { get; set; } = "";

[VectorStoreData(IsIndexed = true)]
public string CityId { get; set; } = "";

[JsonIgnore]
[VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
public string EmbeddingText => $"{Title}\n\n{Description}";
```

A string-valued vector property lets `VectorStoreCollection<string, Session>` call the registered `IEmbeddingGenerator<string, Embedding<float>>` on upsert/search. Indexed `CityId` enables a typed city filter.

### Registration and indexing

```csharp
builder.Services
    .AddEmbeddingGenerator(sp =>
        ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp))
    .UseLogging();

builder.Services.AddInMemoryVectorStore();
builder.Services.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions");
builder.Services.AddSingleton<BaseBackgroundService, SessionIndexingService>();
```

Indexing selects talk sessions with nonempty descriptions, independently of background enrichment; the count depends on the packaged snapshot:

```csharp
await sessionVectors.EnsureCollectionExistsAsync();
await sessionVectors.UpsertAsync(sessions);
```

The upgraded search keeps literal matches first, then appends deduplicated vector matches scoped to the current city:

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

Search requests up to 10 semantic matches and applies a minimum score of `0.25`; literal results remain available while indexing.

### Why bundled ONNX?

[`MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/Embeddings/MiniLmEmbeddingGenerator.cs) uses ONNX Runtime and the BERT tokenizer over the bundled approximately 22 MB `all-MiniLM-L6-v2` model. It runs locally on CPU without requiring the platform chat model.

**Live:** select Durban, wait for indexing, and try `football forecasts` or `gradient descent` in `agenda-search`. Watch `[AI Search]` and embedding logs.

## 3. Typed app data & background enrichment (`AI/Data/` — ~3 mins)

Open [`AI/Data/`](../src/ConferenceApp/AI/Data). It uses `Microsoft.Extensions.AI` typed `GetResponseAsync<T>` outputs.

### Two independent background services

[`SessionEnrichmentService`](../src/ConferenceApp/AI/Data/SessionEnrichmentService.cs) and [`SpeakerEnrichmentService`](../src/ConferenceApp/AI/Data/SpeakerEnrichmentService.cs) run independently in parallel, each with a nested output model (`SessionInsights` and `SpeakerProfile`, respectively).

- **Session role:** one typed request per title and abstract produces a useful concise `Summary` and one to three `Topics`. Allowed topics are `AI`, `Agents`, `Cloud`, `Data`, `Mobile`, `Web`, `IoT`, `Architecture`, `Security`, `DevOps`, and `Gaming`. Match exact allowed values ignoring case/outer whitespace, discard unknown values, and deduplicate.
- **Partial session output:** preserve independently valid fields. At most one title-only request fills missing summary/topics without overwriting good output; a failed or unusable request can also lead to this fallback. A timeout instead skips the item without retrying.
- **Speaker role:** use the biography and talk descriptions to populate `Summary`, `Expertise`, and `AskMeAbout`. Keep the structured output flat and non-nullable. A profile counts as complete when all three fields are populated; useful partial fields still appear.
- **UI updates:** the workers set whole properties (strings and new arrays) on the observable session/speaker models rather than mutating collections in place. MAUI marshals binding property-change notifications to the UI thread, but not `ObservableCollection` changes, so whole-value replacement is safe from a background thread ([data binding](https://learn.microsoft.com/dotnet/maui/fundamentals/data-binding/?view=net-maui-11.0)). Original abstracts and biographies stay unchanged; cards and profiles update in place rather than resetting their lists.

[`EnrichmentService<TItem>`](../src/ConferenceApp/AI/Data/EnrichmentService.cs) owns the model-availability check, item selection and iteration, request deadlines, and final counts; each worker chooses its items, asks, and applies results. Each attempt has a **120-second deadline**. A timeout is logged, discards late output, and advances to the next item without retrying.

Watch the cards update and the summary log: `[AI] {Name} enrichment finished: {Complete} of {Total} complete, {TimedOut} timed out.`

## 4. Source-generated tool contexts (`AI/Tools/` — ~3 mins)

Open [`ToolExtensions.cs`](../src/ConferenceApp/AI/Tools/ToolExtensions.cs), [`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs), [`FavoritesTools.cs`](../src/ConferenceApp/AI/Tools/FavoritesTools.cs), and [`NavigationTools.cs`](../src/ConferenceApp/AI/Tools/NavigationTools.cs).

`Microsoft.Maui.AI.Attributes` generates AOT-friendly tools without runtime reflection:

```csharp
[AIToolSource(typeof(ConferenceDataTools))]
public partial class ConferenceDataToolContext : AIToolContext;
```

| Context | Tools | Scope |
|---|---|---|
| `ConferenceDataToolContext` | `search_sessions`, `get_session`, `get_speaker` | Read-only schedule, abstract, and bio lookup |
| `FavoritesToolContext` | `get_favorites`, `add_favorite`, `remove_favorite` | Local saved talks; verify `favorites` in DevFlow Storage |
| `NavigationToolContext` | `navigate_to_session`, `navigate_to_speaker`, `navigate_to_page` | Open details, switch tab, or switch city |

Read-only research receives only data tools; the general assistant also receives favourites and navigation.

### Method-level DI

`[FromServices]` resolves the parameter from the invocation service provider; the model does not supply a `SearchService`:

```csharp
[ExportAIFunction("search_sessions")]
[Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
public async Task<string> SearchSessionsAsync(
    [Description("Topic, title, or speaker to search for.")] string query,
    [FromServices] SearchService search,
    CancellationToken cancellationToken = default)
```

Apple supplies `functionInvocationServices` to the native bridge. Android and Windows use managed function invocation with `.Build(services)`.

## 5. Context, workflows, and semantic routing (`AI/Agents/` — ~4 mins)

Open [`AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs) and the [agents folder](../src/ConferenceApp/AI/Agents).

### Keyed context providers

| Key / provider | Context |
|---|---|
| `"user"` / `UserContextProvider` | Active city, event date, demo conference time, venue/address, saved favourites and topics |
| `"schedule"` / `ScheduleContextProvider` | Full city schedule: times, tracks, titles, speakers, topics |
| `"available-sessions"` / `AvailableSessionsContextProvider` | Candidate talks excluding already-saved favourites |
| `"rag"` / `RagContextProvider` | `TextSearchProvider` running before inference; searches from the latest ordinary user message and injects up to three talk titles, IDs, and enriched summaries |

The first three are keyed `AIContextProvider` registrations; `"rag"` is registered through `RagContextProvider.Create`. `"assistant"` is a keyed `IChatClient`.

The pipeline is compact: background session enrichment → summaries in RAG context → `get_session` / `get_speaker` for full source content. Agents use `UseProvidedChatClientAsIs=true` and `Temperature=0`.

### Default assistant

[`ConferenceAssistantAgent.cs`](../src/ConferenceApp/AI/Agents/ConferenceAssistantAgent.cs) combines user, schedule, and RAG context with all three tool contexts. It handles times, rooms, bios, clash questions, saved talks, and requested navigation.

### Session deep dive: sequential → concurrent

[`SessionDeepDiveWorkflow.cs`](../src/ConferenceApp/AI/Agents/SessionDeepDiveWorkflow.cs):

1. `session-researcher` reads the target abstract and speaker bio with data tools and builds a factual brief.
2. `takeaways` produces three technical takeaway bullets.
3. In parallel with takeaways, `questions-coach` produces two things to think about and two Q&A questions.

```csharp
var parallelAnalysis = AgentWorkflowBuilder
    .BuildConcurrent("deep-dive-parallel-analysis", [takeawaysAgent, questionsCoachAgent])
    .AsAIAgent(name: "deep-dive-parallel-analysis");

return AgentWorkflowBuilder.BuildSequential(
    "SessionDeepDiveWorkflow",
    [sessionResearcher, parallelAnalysis]);
```

### Recommendations: multi-turn group chat

[`TalkRecommendationWorkflow.cs`](../src/ConferenceApp/AI/Agents/TalkRecommendationWorkflow.cs) uses `CreateGroupChatBuilderWith` and a small custom `GroupChatManager` with **no tools**, relying on user and available-session context:

```text
Turn 1: concierge → pause for attendee interests
Turn 2: concierge → depth-advocate ⇄ alternative-advocate (2 rounds) → moderator
```

`PanelGroupChatManager` overrides `SelectNextAgentAsync` and `ShouldTerminateAsync`: Turn 1 stops after concierge asks. On the next turn, concierge locks in requested/avoided topics, then the advocates take turns over two rounds, each pitching **one** talk not yet mentioned (alternative-advocate reacts and offers a different option). `moderator` lists the four picks chronologically. Already-saved talks are excluded from candidates.

### Stateful routing plus embedding similarity

Workflows adapt with `.AsAIAgent(...).AsIChatClient(allowNonChatClientAgents: true)`. The keyed assistant wraps two routers:

1. `RoutingChatClient.Create` checks message history. If the last named assistant message has `AuthorName == "concierge"`, route the next user response back to recommendations.
2. Otherwise, `SemanticRoutingChatClient` compares the prompt to deep-dive/recommendation utterances using the same ONNX embedding generator (`scoreThreshold: 0.58f`, `topK: 1`), falling back to the default assistant.

This routes using embeddings, not an extra model-written tool plan.

## 6. Making multi-agent streaming readable (~3 mins)

Open [`Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs).

- **Progress first:** the reply shows a `chat-progress` pill until text arrives.
- **One bubble per agent turn:** streamed updates are grouped by `MessageId` (falling back to `AuthorName`), so each workflow agent (researcher, takeaways, questions coach, advocates, moderator) gets its own bubble. The bubble's author shows the agent and the model that answered, e.g. `conference-assistant (apple-intelligence)`.
- **Visible tool calls:** each `FunctionCallContent` adds a small tool chip (`chat-tool-{name}`) above the reply.
- **Preserve routing history:** `updates.ToChatResponse().Messages` is appended to the conversation history, keeping `AuthorName` for the concierge follow-up route.
- **Tappable evidence:** `UpdateReferencedSessionsAsync` finds current-city talk titles/IDs mentioned in the reply and attaches `Session` cards. `chat-session-card-{Id}` opens session details.

## Assistant live demo

Select **Durban** and verify model availability. The empty chat shows four starter suggestions:

1. **`When is Matthew's talk?`** or **`Do my favourites clash?`** — show schedule/RAG context and any resulting session card. A schedule answer need not require a tool call.
2. **New chat**, then **`Prepare me for Matthew's talk: takeaways and questions`** — show researcher progress, data tools where supported, and the two parallel analysts.
3. **New chat**, then **`Can you suggest some talks for me?`** — when concierge asks, reply **`AI and Cloud, no Web`**. Show the advocates and moderator; this workflow is tool-free.
