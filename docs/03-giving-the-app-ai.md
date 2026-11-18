# Part 3: Giving the App That Same AI — Code Walkthrough & Live Demo (20 mins)

**This is the separate native AI stack, not the implementation on `main`.** `main` has literal search and an unavailable Assistant UI. Keep this guide on `main` for the whole talk, but switch to the [ordered stack #34 → #35 → #42 → #39 → #36 → #37 → #41](../README.md#demo-branches) to run these features.

Source links below point to the [combined top-of-stack branch](https://github.com/mattleibow/dotnetconf2026/tree/mattleibow-ai-6-windows-tool-calling). That branch can temporarily lag revisions to lower PRs; the enrichment section links to the revised #39 source.

Start at [`AI/ConferenceAIExtensions.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/ConferenceAIExtensions.cs). One entry point organizes five areas:

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

Look for model/index startup, `LoggingEmbeddingGenerator`, `[AI Search]`, `[AI RAG]`, `[AI Tool]`, and `[AI Chat]`. These are stack diagnostics, not logs to expect from `main`. Exact output, counts, timings, and enrichment messages depend on the checked-out revision and device.

## 1. Local on-device chat clients (`AI/ChatClients/` — ~3 mins)

Open [`ChatExtensions.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) and [`UnavailableChatClient.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/ChatClients/UnavailableChatClient.cs).

MAUI Essentials AI supplies `IChatClient` implementations. The registration checks OS support, and `GetModelAvailabilityAsync()` checks model readiness before chat or enrichment. An OS-version check alone does not establish hardware support, permissions, model provisioning, or availability.

| Platform | Client and invocation |
|---|---|
| iOS / Mac Catalyst 26+ | `AppleIntelligenceChatClient`, with `loggerFactory` and `functionInvocationServices: services`; Apple's native Foundation Models bridge invokes tools |
| Supported Android devices (API check 26+) | `GeminiNanoChatClient` profiles behind `OrderedFailoverChatClient`, then `.UseFunctionInvocation(loggerFactory)` and `.Build(services)` |
| Windows 11 build 26100+ with an available model | Native `WindowsAIChatClient`; layer 6 adds an experimental tool adapter inside the managed invocation loop |
| Unsupported / unavailable | Availability message and disabled chat controls; no cloud fallback |

Apple passes DI services into the native client rather than adding the managed function-invocation loop:

```csharp
new AppleIntelligenceChatClient(
        loggerFactory: loggerFactory,
        functionInvocationServices: services)
    .AsBuilder()
    .UseLogging(loggerFactory)
    .Build();
```

Android tries `gemini-nano-preview-fast`, `gemini-nano-preview-full`, `gemini-nano-full`, and `gemini-nano-fast` in order. `OrderedFailoverChatClient` can move to another profile when a requested capability is unsupported. Availability failures are logged while checking the next profile. An Android emulator that runs semantic search need not support Gemini Nano chat.

The `chat-unavailable` banner and input gating are intentional fallbacks. There is no automatic model download. Check the stack's central package versions rather than assuming experimental package versions are stable.

### Windows layer 6: experimental, not feature parity

The native Windows client generates text but has no native function-calling API. [#41](https://github.com/mattleibow/dotnetconf2026/pull/41) routes requests **with tools** through `WindowsAIToolCallingClient` inside `UseFunctionInvocation()`. Enrichment and tool-free analyst requests use the plain client. The adapter exposes the same existing conference lookup, favourites, and navigation tools; it does not add a separate Windows tool set.

Windows needs:

- Windows 11 build 26100 or newer and compatible Copilot+ hardware / on-device model.
- Packaged MSIX identity with the `systemAIModels` manifest capability.
- A compatible Windows App SDK runtime and any Microsoft-required Phi Silica access approval.
- The stack's pinned experimental Windows package. Its [provenance and reproduction guide](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/packages/README.md) records the source commit, license, checksum, and build inputs. Other platforms keep their published package.

Use the packaged Windows launch profile. `-p:WindowsPackageType=None` deliberately exercises the unavailable fallback, not working unpackaged Windows AI.

The adapter permits **one call per user turn**, a **30-second deadline per native phase**, and no retry. Every argument must be supported by an exact quote from the user's message or a tool result. Existing tools stay within its string-only / empty-object schema subset. Native requests share a one-at-a-time gate so enrichment cannot starve a tool phase; logical background services can still be independent.

A failed adapter is logged and replaced. A failed lookup (`get_*`, `search_*`) falls back to supplied context. A failed favourite/navigation action gets a fixed “nothing changed” response instead of a model-written answer that might falsely claim success. These constraints do not change Apple or Android behavior.

**Observed limitations from the stack's packaged Copilot+ PC tests:** it navigated to Speakers twice, but often chose the right tool with the wrong quotation, chose another tool, or timed out. Adding favourites by chat did not succeed. Tool turns took roughly 60–120 seconds and a deep dive about three minutes; replies could contain typos or wrong details. These are observations, not performance promises. Use Agenda and Speakers for reliable actions and verify saved state in Storage.

Earlier Windows layers use context-only answers without tools. Layer 6 restores the tool-backed deep-dive researcher, with context fallback on lookup failure; don't describe the top layer as permanently lacking researcher tools. Recommendations remain tool-free. Every generative workflow still needs a local chat model—ONNX embeddings alone cannot produce replies.

## 2. Semantic embeddings and vector search (`AI/Embeddings/` — ~4 mins)

Open the stack versions of [`Data/Session.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/Data/Session.cs), [`EmbeddingExtensions.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs), [`SessionIndexingService.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs), and [`SearchService.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/Services/SearchService.cs).

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

The inspected implementation requests up to 10 semantic matches and applies a minimum score of `0.25`; literal results remain available if the collection is not ready or vector search fails.

### Why bundled ONNX?

[`MiniLmEmbeddingGenerator.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Embeddings/MiniLmEmbeddingGenerator.cs) uses ONNX Runtime and the BERT tokenizer over the bundled approximately 22 MB `all-MiniLM-L6-v2` model. It runs locally on CPU without requiring the platform chat model.

The demo moved from Apple's OS embedding table to MiniLM for domain-synonym retrieval. Earlier Durban demo measurements ranked *Developing A .NET Sports Predictor* first for `football forecasts` and *Wait, what does Optimization really entail?* first for `gradient descent`, versus fifth/fourth with the tested Apple embedding setup. Treat these as historical demo observations, not a universal model benchmark.

**Live:** select Durban, wait for indexing, and try those queries in `agenda-search`. Watch `[AI Search]` and embedding logs. On `main` these are only literal queries, so do not expect semantic matches.

## 3. Typed app data & background enrichment (`AI/Data/` — ~3 mins)

Open [PR #39](https://github.com/mattleibow/dotnetconf2026/pull/39) and its [`AI/Data/` source at 220a32a](https://github.com/mattleibow/dotnetconf2026/tree/220a32a433c6589e0bccdda11a591f8108038fc9/src/ConferenceApp/AI/Data). It uses `Microsoft.Extensions.AI` typed `GetResponseAsync<T>` outputs, **not `Microsoft.Extensions.DataIngestion`, `SummaryEnricher`, `KeywordEnricher`, or Windows-specific ingestion wrappers**.

### Two independent background services

[`SessionEnrichmentService`](https://github.com/mattleibow/dotnetconf2026/blob/220a32a433c6589e0bccdda11a591f8108038fc9/src/ConferenceApp/AI/Data/SessionEnrichmentService.cs) and [`SpeakerEnrichmentService`](https://github.com/mattleibow/dotnetconf2026/blob/220a32a433c6589e0bccdda11a591f8108038fc9/src/ConferenceApp/AI/Data/SpeakerEnrichmentService.cs) run independently in parallel, each with a nested output model (`SessionInsights` and `SpeakerProfile`, respectively). Each service processes its own items; this is not one service that finishes all sessions before beginning speakers.

**Revision boundary:** until the higher layers incorporate #39's revision, the top-stack branch may still contain the earlier sequential implementation.

- **Session role:** one typed request per title and abstract produces a useful concise `Summary` and one to three `Topics`. Allowed topics are `AI`, `Agents`, `Cloud`, `Data`, `Mobile`, `Web`, `IoT`, `Architecture`, `Security`, `DevOps`, and `Gaming`. Match exact allowed values ignoring case/outer whitespace, discard unknown values, and deduplicate.
- **Partial session output:** preserve independently valid fields. At most one title-only request fills missing summary/topics without overwriting good output; a failed or unusable request can also lead to this fallback. A timeout instead skips the item without retrying.
- **Speaker role:** use the biography and talk descriptions to populate the existing `Summary`, `Expertise`, and `AskMeAbout` fields. Keep the structured output flat and non-nullable; compact native models do not need nested metadata wrappers, ingestion chunks, or batch arrays.
- **UI updates:** apply observable session/speaker fields on the main thread. Original abstracts and biographies stay unchanged; cards and profiles update in place rather than resetting their lists.

[`EnrichmentRequest`](https://github.com/mattleibow/dotnetconf2026/blob/220a32a433c6589e0bccdda11a591f8108038fc9/src/ConferenceApp/AI/Data/EnrichmentRequest.cs) uses a fresh **120-second deadline per attempt**, linked to app shutdown. A timeout is logged, discards late output, and advances to the next item without retrying or halting the entire job. Native cancellation, especially Windows producer drainage, is not guaranteed to finish immediately; late output must not update UI. Keep this caveat distinct from layer 6's serialized Windows request gate. Two logical services do not promise simultaneous native inference, nor do they gate all chat usage across platforms.

Use the concise service/error logs and actual card updates to demonstrate enrichment, not a fixed transcript or per-item telemetry contract. Startup/shutdown behavior is described in [Part 1](01-whats-new-net11-maui11.md#cooperative-shutdown-not-a-process-exit-promise).

## 4. Source-generated tool contexts (`AI/Tools/` — ~3 mins)

Open [`ToolExtensions.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Tools/ToolExtensions.cs), [`ConferenceDataTools.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Tools/ConferenceDataTools.cs), [`FavoritesTools.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Tools/FavoritesTools.cs), and [`NavigationTools.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Tools/NavigationTools.cs).

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

Apple supplies `functionInvocationServices` to the native bridge. Android and the Windows experimental path use managed function invocation with `.Build(services)`.

## 5. Context, workflows, and semantic routing (`AI/Agents/` — ~4 mins)

Open [`AgentExtensions.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Agents/AgentExtensions.cs) and the [agents folder](https://github.com/mattleibow/dotnetconf2026/tree/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Agents).

### Keyed context providers

| Key / provider | Context |
|---|---|
| `"user"` / `UserContextProvider` | Active city, event date, demo conference time, venue/address, saved favourites and topics |
| `"schedule"` / `ScheduleContextProvider` | Full city schedule: times, tracks, titles, speakers, topics |
| `"available-sessions"` / `AvailableSessionsContextProvider` | Candidate talks excluding already-saved favourites |
| `"rag"` / `RagContextProvider` | `TextSearchProvider` running before inference; searches from the latest ordinary user message and injects up to three talk titles, IDs, and enriched summaries |

The first three are keyed `AIContextProvider` registrations; `"rag"` is registered through `RagContextProvider.Create`. `"assistant"` is a keyed `IChatClient`.

The pipeline is compact: background session enrichment → summaries in RAG context → `get_session` / `get_speaker` only for full source content. Agents use `UseProvidedChatClientAsIs=true` and `Temperature=0`; output budgets vary by client/workflow rather than being a guarantee on every agent.

### Default assistant

[`ConferenceAssistantAgent.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Agents/ConferenceAssistantAgent.cs) combines user, schedule, and RAG context with all three tool contexts. It handles times, rooms, bios, clash questions, saved talks, and requested navigation.

### Session deep dive: sequential → concurrent

[`SessionDeepDiveWorkflow.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Agents/SessionDeepDiveWorkflow.cs):

1. `SessionResearcher` reads the target abstract and speaker bio with data tools and builds a factual brief.
2. `TakeawaysAgent` produces three technical takeaway bullets.
3. In parallel with takeaways, `QuestionsCoachAgent` produces two things to think about and two Q&A questions.

```csharp
var parallelAnalysis = AgentWorkflowBuilder
    .BuildConcurrent("DeepDiveParallelAnalysis", [takeawaysAgent, questionsCoachAgent])
    .AsAIAgent(name: "DeepDiveParallelAnalysis");

return AgentWorkflowBuilder.BuildSequential(
    "SessionDeepDiveWorkflow",
    [sessionResearcher, parallelAnalysis]);
```

On earlier Windows layers the researcher is limited to supplied schedule context, not full source retrieval. Layer 6 attempts data-tool research and falls back to context on failure. Neither fallback nor generated takeaways substitute for reading the actual abstract/bio.

### Recommendations: multi-turn group chat

[`TalkRecommendationWorkflow.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/AI/Agents/TalkRecommendationWorkflow.cs) uses `CreateGroupChatBuilderWith` and `RoundRobinGroupChatManager` with **no tools**, relying on user and available-session context:

```text
Turn 1: Concierge → pause for attendee interests
Turn 2: Concierge → DepthAdvocate → BreadthAdvocate → PracticalAdvocate → Moderator
```

The custom termination function pauses after Concierge's first iteration (`IterationCount >= 1`). On the next turn, Concierge locks in requested/avoided topics; each advocate pitches 2–3 distinct talks; Moderator targets a chronological shortlist of 4–6. Already-saved talks are excluded from candidates. These are model instructions, not guaranteed exact counts.

### Stateful routing plus embedding similarity

Workflows adapt with `.AsAIAgent(...).AsIChatClient(allowNonChatClientAgents: true)`. The keyed assistant wraps two routers:

1. `RoutingChatClient.Create` checks message history. If the last named assistant message has `AuthorName == "Concierge"`, route the next user response back to recommendations.
2. Otherwise, `SemanticRoutingChatClient` compares the prompt to deep-dive/recommendation utterances using the same ONNX embedding generator (`scoreThreshold: 0.58f`, `topK: 1`), falling back to the default assistant.

This routes using embeddings, not an extra model-written tool plan.

## 6. Making multi-agent streaming readable (~3 mins)

Open the stack's [`Services/ChatService.cs`](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/src/ConferenceApp/Services/ChatService.cs), not the stub on `main`.

- **Progress, not internal chatter:** research and Concierge handoff turns update `reply.Progress`, shown by `chat-progress`, instead of filling the visible bubble with intermediate state.
- **De-interleave parallel streams:** show `TakeawaysAgent` tokens under **Key Takeaways**, buffer `QuestionsCoachAgent`, then append **Things to Think About & Ask** so concurrent output remains readable.
- **Preserve routing history:** store `updates.ToChatResponse().Messages` in `reply.ResponseMessages`, retaining `AuthorName` across turns for the Concierge follow-up route.
- **Tappable evidence:** `AddReferencedSessionsAsync` finds current-city talk titles/IDs mentioned in the reply and attaches `Session` cards. `chat-session-card-{Id}` opens session details.

## Assistant live demo

Select **Durban** and verify model availability. The empty chat shows four starter suggestions:

1. **`When is Matthew's talk?`** or **`Do my favourites clash?`** — show schedule/RAG context and any resulting session card. A schedule answer need not require a tool call.
2. **New chat**, then **`Prepare me for Matthew's talk: takeaways and questions`** — show researcher progress, data tools where supported, and the two parallel analysts.
3. **New chat**, then **`Can you suggest some talks for me?`** — when Concierge asks, reply **`AI and Cloud, no Web`**. Show the advocates and Moderator; this workflow is tool-free.

On Windows, allow for the documented delays and failed tool calls. Do not equate fluent text with a completed action: inspect favourites/storage or use manual controls. Without an available model, show the availability message and continue the non-AI app demo.
