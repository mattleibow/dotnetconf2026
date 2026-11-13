# Speaker Runbook & Talking Points (`docs/demo.md`)

**Session:** *Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI*  
**Duration:** 45 minutes  

We start with a functional, zero-styled wireframe .NET MAUI 11 app that already has its on-device AI stack wired up in [`src/ConferenceApp/AI/`](../src/ConferenceApp/AI/). Every feature—embeddings, semantic search, background summarization, tool calling, semantic routing, and multi-agent workflows—runs **100% locally on device and offline** with zero cloud calls.

The talk has three parts:

- **Part 1 — What's New in .NET 11, C# 15 & .NET MAUI 11 (5–8 mins):** Walk through the clean baseline project before kicking off the coding agent.
- **Part 2 — Agents Building Apps (12–15 mins):** Paste Prompts `01` and `02` so GitHub Copilot, the Mobile Device canvas, the MAUI DevFlow Web Inspector, and the `impeccable` design skill inspect the running wireframe, add an inline favourite heart button on Agenda cards, style all four tabs, and verify layout diagnostics and `Preferences` live.
- **Part 3 — Giving the App That Same AI (20 mins):** While Prompt `02` styles the app (and right after), walk through [`src/ConferenceApp/AI/`](../src/ConferenceApp/AI/) and [`Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs) and demo semantic search, data ingestion enrichment, source-generated tools, and multi-agent workflows with live logs.

---

## Pre-Flight Checklist (5 mins before going on stage)

Boot the iOS Simulator beforehand so Prompt `01` doesn't wait for a cold boot:

```sh
xcrun simctl boot "iPhone 17 Pro" || true
open -a Simulator
```

Verify the DevFlow broker is running:

```sh
maui devflow list
```

Keep a terminal tab ready for live AI logs during Part 3:

```sh
maui devflow logs --follow --no-json
```

---

## Part 1: What's New in .NET 11, C# 15 & .NET MAUI 11 (5–8 mins)

Open these files briefly before kicking off the first prompt to show why the baseline project is so small and readable.

### CoreCLR, XAML Source Gen & Offline AI Assets ([`src/ConferenceApp/ConferenceApp.csproj`](../src/ConferenceApp/ConferenceApp.csproj))

In .NET 11, **CoreCLR is the default runtime** for .NET MAUI on iOS, Mac Catalyst, and Android—no legacy Mono or interpreter configuration workarounds are needed. **XAML Source Generation** (`MauiXamlInflator=SourceGen`), **XAML Incremental Hot Reload**, and **Lazy `ResourceDictionary`** are all enabled by default without extra MSBuild properties.

Point out `<EnablePreviewFeatures>true</EnablePreviewFeatures>` (which enables C# Expressions in XAML) and how both the offline conference dataset and the 22 MB ONNX model are linked directly from the repo root as `MauiAsset`s:

```xml
<PropertyGroup>
    <TargetFrameworks>net11.0-android</TargetFrameworks>
    <TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('linux'))">$(TargetFrameworks);net11.0-ios;net11.0-maccatalyst</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net11.0-windows10.0.19041.0</TargetFrameworks>
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
</PropertyGroup>

<ItemGroup>
    <!-- Link the offline conference snapshot directly from data/conference -->
    <MauiAsset Include="..\..\data\conference\**" LogicalName="conference/%(RecursiveDir)%(Filename)%(Extension)" />
    <!-- Link the local ONNX MiniLM embedding model and vocabulary -->
    <MauiAsset Include="..\..\models\all-MiniLM-L6-v2\*" LogicalName="models/all-MiniLM-L6-v2/%(Filename)%(Extension)" />
</ItemGroup>
```

### Implicit Global XAML Namespaces ([`src/ConferenceApp/GlobalXmlns.cs`](../src/ConferenceApp/GlobalXmlns.cs))

Registering project and library namespaces once at the assembly level eliminates `xmlns:` boilerplate from every `.xaml` file in the project. Controls like `<Map />`, custom converters, ViewModels, and Data types work directly without prefixes.

```csharp
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.AI")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Converters")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Data")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Services")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.ViewModels")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Views")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "Microsoft.Maui.Controls.Maps", AssemblyName = "Microsoft.Maui.Controls.Maps")]
```

### C# Expressions in XAML & AOT-Safe `RelativeSource` ([`AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) & [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml))

With `<EnablePreviewFeatures>true</EnablePreviewFeatures>`, XAML supports inline C# expressions—negations, ternaries, boolean `AND`, and string interpolation—eliminating `HasXyz` ViewModel properties and `InverseBooleanConverter`s. Combined with .NET 11 AOT-safe compiled `RelativeSource` bindings, templates bind directly to ancestor ViewModels without reflection.

```xml
<ContentPage x:Class="ConferenceApp.Views.AgendaPage"
             x:DataType="AgendaViewModel"
             Title="Agenda">
...
<Button AutomationId="favorites-filter"
        Text="{ShowFavoritesOnly ? 'Showing: Favourites' : 'Showing: All Sessions'}"
        Command="{Binding ToggleFavoritesFilterCommand}" />
...
<VerticalStackLayout AutomationId="{$'session-{Id}'}"
                     Background="Transparent">
    <VerticalStackLayout.GestureRecognizers>
        <TapGestureRecognizer
            Command="{Binding OpenSessionCommand, Source={RelativeSource AncestorType={x:Type AgendaViewModel}}, x:DataType=AgendaViewModel}"
            CommandParameter="{Binding .}" />
    </VerticalStackLayout.GestureRecognizers>
    ...
    <Label Text="{Topic}"
           IsVisible="{!string.IsNullOrWhiteSpace(Topic)}" />
</VerticalStackLayout>
```

And in [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml):

```xml
<Button AutomationId="chat-send"
        Text="Send"
        Command="{Binding SendCommand}"
        IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}" />
```

### C# Partial Properties & `IDispatcher` ([`AgendaViewModel.cs`](../src/ConferenceApp/ViewModels/AgendaViewModel.cs))

C# partial properties pair with `CommunityToolkit.Mvvm` 8.4.2 so ViewModels have no backing fields or underscore naming conventions. ViewModels inject MAUI's `IDispatcher` (`dispatcher.Dispatch(...)`) to marshal background `WeakReferenceMessenger` updates onto the UI thread without static `MainThread` calls.

```csharp
[ObservableProperty]
public partial City? SelectedCity { get; set; }

[ObservableProperty]
public partial string SearchQuery { get; set; } = "";

[ObservableProperty]
public partial bool ShowFavoritesOnly { get; set; }

public void Receive(SessionsEnrichedMessage message) =>
    dispatcher.Dispatch(() => _ = LoadAsync());
```

### Single-File C# Scripts (`dotnet run file.cs`) ([`data/ImportConference.cs`](../data/ImportConference.cs) & [`models/DownloadModels.cs`](../models/DownloadModels.cs))

Repo utilities don't need separate `.csproj` files. Both the conference importer (`data/ImportConference.cs`) and the HuggingFace ONNX model verifier (`models/DownloadModels.cs`) are standalone `.cs` scripts targeting `.NET 11` and `C# 15` with `#:property` and `#:package` directives:

```csharp
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:package AngleSharp@1.1.2
```

### Clean Startup, DevFlow & Data Layer ([`MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs), [`App.xaml.cs`](../src/ConferenceApp/App.xaml.cs) & [`Data/`](../src/ConferenceApp/Data/))

`MauiProgram.cs` registers the data provider, app services, a single `builder.AddConferenceAI()` call, separate `AddTransient<Page>().AddTransient<ViewModel>()` pairs, and the MAUI DevFlow agent + AI debug logging in `#if DEBUG`:

```csharp
builder.Services.AddSingleton<ConferenceDbProvider>();
builder.Services.AddSingleton<UserPreferencesService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<ChatService>();

builder.AddConferenceAI();
```

In [`App.xaml.cs`](../src/ConferenceApp/App.xaml.cs), the constructor kicks off background vector indexing and AI session enrichment right after launch:

```csharp
public App(SessionIndexingService indexing, SessionEnrichmentService enrichment)
{
    InitializeComponent();
    _ = indexing.StartAsync();
    _ = enrichment.StartAsync();
}
```

In [`src/ConferenceApp/Data/`](../src/ConferenceApp/Data/), [`ConferenceDbProvider.cs`](../src/ConferenceApp/Data/ConferenceDbProvider.cs) is a 6-line on-demand provider (`GetDbAsync()`) over [`ConferenceDb.cs`](../src/ConferenceApp/Data/ConferenceDb.cs), which loads `conference.json` once, caches all Markdown descriptions (`session.Description`, `speaker.Bio`, `venue.Description`), and links navigation properties (`city.Venue`, `session.City`, `session.Speakers`). The collections on `ConferenceDb` are read-only (`IReadOnlyList<T>`), background AI enrichment populates `session.Summary` and `session.Topic` in memory, and user state (selected city and favourites) lives separately in [`UserPreferencesService.cs`](../src/ConferenceApp/Services/UserPreferencesService.cs).

---

## Part 2: Agents Building Apps — Live Prompts `01` & `02` (12–15 mins)

### Step 01: Inspect and Drive ([`.github/prompts/01-inspect-and-drive.prompt.md`](../.github/prompts/01-inspect-and-drive.prompt.md))

Paste `01-inspect-and-drive.prompt.md` into the chat. While the agent runs (~2–3 mins), highlight the two side-by-side canvases and diagnostics:

| Surface | What to show on stage |
|---|---|
| **Mobile Device Canvas** (`mobile-device`) | Live iOS Simulator embedded directly inside the Copilot side panel so the audience watches every tap and transition. |
| **DevFlow Inspector — UI Tree** (`browser` canvas) | Live visual hierarchy, element bounds, visibility, and stable `AutomationId`s (`agenda-search`, `favorites-filter`, `session-<id>`). |
| **DevFlow Inspector — Logs** (`browser` canvas) | Real-time `ILogger` stream from inside the app showing `[AI]`, `LoggingChatClient`, `LoggingEmbeddingGenerator`, and `ChatClientAgent`. |
| **DevFlow Inspector — Storage** (`browser` canvas) | Inspecting and editing sandboxed `Preferences` (`selected_city`, `favorites`) live without writing debug UI. |
| **Layout Diagnostics** (`maui devflow ui diagnostics --profile agent`) | Automated detection of clipped text, off-screen elements, or zero-sized containers so the agent doesn't rely on screenshot guesswork. |

---

### Step 02: Make It Beautiful + Add Inline Favourite Hearts ([`.github/prompts/02-make-it-beautiful.prompt.md`](../.github/prompts/02-make-it-beautiful.prompt.md))

Paste `02-make-it-beautiful.prompt.md` into the chat. While the agent works (~5–6 mins), switch to your editor and walk through **Part 3 (Giving the App That Same AI)** below so there is zero dead air.

When Step 02 finishes, show the styled app on screen and highlight the MAUI 11 touches requested in `02-make-it-beautiful.prompt.md`:

| MAUI 11 Feature | Where the prompt applies it |
|---|---|
| **`BoxView.Fill` with `LinearGradientBrush`** | Decorative gradient hero banner on `EventPage.xaml` without custom drawing. |
| **`ScaleToAsync` animation** | Async animation on the new 44×44pt inline favourite heart button (`session-favorite-<id>`) on Agenda talk cards. |
| **Native `<Map />` control** | Embedded `Microsoft.Maui.Controls.Maps` map on the Event tab using global XAML namespaces. |
| **Adaptive & themed layouts** | Portrait phone + wide Mac Catalyst desktop layouts and light/dark themes (`AppThemeBinding`) verified with `maui devflow ui diagnostics`. |

---

## Part 3: Giving the App That Same AI — Code Walkthrough & Live Demo (20 mins)

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

Keep `maui devflow logs --follow --no-json` visible in a terminal (or open the **Logs** tab in the DevFlow Web Inspector). Here is what to look for in the logs during the demo:

| Log Prefix / Category | What it shows |
|---|---|
| `[AI] Indexed 54 session abstracts...` | `SessionIndexingService` populating `VectorStoreCollection<string, Session>` at startup. |
| `[AI] Enriching session ...` | `SessionEnrichmentService` running `SummaryEnricher` + `ClassificationEnricher` in the background (watch topic chips appear live on Agenda cards!). |
| `[AI Search] Query '...' in durban -> Top match: '...'` | `SearchService` running hybrid + ONNX MiniLM cosine-similarity search. |
| `[AI RAG] Injected 3 matching sessions...` | `RagContextProvider` (`TextSearchProvider`) injecting top session summaries before an agent runs. |
| `[AI Tool] get_session` / `get_speaker` / `list_sessions` / `add_favorite` | Source-generated `[AIToolSource]` tools executing on device. |
| `LoggingChatClient` / `LoggingEmbeddingGenerator` / `ChatClientAgent` | Built-in `Microsoft.Extensions.AI` and `Microsoft.Agents.AI` pipeline telemetry enabled in `MauiProgram.cs`. |

---

### 1. Local On-Device Chat Clients (`AI/ChatClients/` — ~3 mins)

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
    return new GeminiNanoChatClient()
        .AsBuilder()
        .UseFunctionInvocation(loggerFactory)
        .UseLogging(loggerFactory)
        .Build();
}
#endif
return new UnavailableChatClient();
```

Point out two important details here:

First, notice how `AppleIntelligenceChatClient` takes `functionInvocationServices: services` directly in its constructor (without `.UseFunctionInvocation()`), because Apple's native Foundation Models runtime executes tools inside its native bridge and uses `functionInvocationServices` to resolve `[FromServices]` parameters. On Android and Windows, `.UseFunctionInvocation(loggerFactory)` adds the managed `Microsoft.Extensions.AI` tool-calling loop.

Second, if the current OS or device doesn't support local AI, `CreateLocalChatClient` returns [`UnavailableChatClient`](../src/ConferenceApp/AI/ChatClients/UnavailableChatClient.cs) instead of throwing at startup. That means `IChatClient`, enrichers, and agents **always resolve cleanly from DI**, while `ChatExtensions.GetModelAvailabilityAsync()` (`Available`, `Disabled`, `Downloading`, `NotReady`, `NotSupported`) lets the UI and background worker gate gracefully.

---

### 2. Semantic Embeddings & Declarative Vector Store (`AI/Embeddings/` — ~4 mins)

Open [`Data/Session.cs`](../src/ConferenceApp/Data/Session.cs), [`AI/Embeddings/EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs), [`AI/Embeddings/SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs), and [`Services/SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs).

#### Declarative Vector Mapping on `Session`
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

#### DI Registration & Background Indexing
[`EmbeddingExtensions.cs`](../src/ConferenceApp/AI/Embeddings/EmbeddingExtensions.cs) registers the ONNX `MiniLmEmbeddingGenerator` with `.UseLogging()`, the in-memory vector store, and the strongly-typed `Session` collection:

```csharp
builder.Services
    .AddEmbeddingGenerator(sp => ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp))
    .UseLogging();

builder.Services.AddInMemoryVectorStore();
builder.Services.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions");
builder.Services.AddSingleton<SessionIndexingService>();
```

At startup, [`SessionIndexingService.cs`](../src/ConferenceApp/AI/Embeddings/SessionIndexingService.cs) loads `db.Sessions` and indexes them in two lines:

```csharp
await sessionVectors.EnsureCollectionExistsAsync();
await sessionVectors.UpsertAsync(sessions);
```

And [`SearchService.cs`](../src/ConferenceApp/Services/SearchService.cs) performs hybrid search—matching literal title/speaker substrings first, then appending semantic vector matches from `VectorStoreCollection<string, Session>` filtered by `CityId`:

```csharp
await foreach (var match in sessionVectors.SearchAsync(
    trimmed,
    top: SemanticResultLimit + results.Count,
    new() { Filter = s => s.CityId == cityId }))
{
    if (seen.Add(match.Record.Id))
        results.Add(match.Record);
}
```

#### Why ONNX `all-MiniLM-L6-v2` instead of Apple `NLEmbeddingGenerator`?
When we first tested Apple's built-in `NLEmbeddingGenerator` (a 512-dim OS word/sentence table rather than a transformer retrieval model), semantic search missed domain synonyms. Because `VectorStoreCollection<string, Session>` depends only on `IEmbeddingGenerator<string, Embedding<float>>`, swapping one DI line to [`MiniLmEmbeddingGenerator.cs`](../src/ConferenceApp/AI/Embeddings/MiniLmEmbeddingGenerator.cs) (`Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` `BertTokenizer` over the 22 MB `all-MiniLM-L6-v2` model in `models/`) fixed retrieval across iOS, Mac Catalyst, Android, and Windows:

| Durban Search Query | Target Session | Apple `NLEmbeddingGenerator` | ONNX `MiniLmEmbeddingGenerator` |
|---|---|---|---|
| `"football forecasts"` | *Developing A .NET Sports Predictor* | Ranked **#5** | Ranked **#1** |
| `"gradient descent"` | *Wait, what does Optimization really entail?* | Ranked **#4** | Ranked **#1** |

#### Live Demo on the Agenda Tab (Durban)
Switch the city to **Durban**, type **`football forecasts`** and **`gradient descent`** into `agenda-search`, and point out `[AI Search]` and `LoggingEmbeddingGenerator` in `maui devflow logs`.

---

### 3. Data Ingestion & Background Enrichment (`AI/Data/` — ~3 mins)

Open [`src/ConferenceApp/AI/Data/DataExtensions.cs`](../src/ConferenceApp/AI/Data/DataExtensions.cs) and [`SessionEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionEnrichmentService.cs).

Putting all 54 full Markdown abstracts into a compact on-device model's context window overflows or degrades accuracy. Using `Microsoft.Extensions.DataIngestion` (`11.0.0-preview.1.26560.3`), `DataExtensions.cs` registers `EnricherOptions` (backed by the local `IChatClient`), `SummaryEnricher`, and `ClassificationEnricher`:

```csharp
builder.Services.AddSingleton(services => new EnricherOptions(services.GetRequiredService<IChatClient>())
{
    BatchSize = 1,
    ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
    LoggerFactory = services.GetRequiredService<ILoggerFactory>()
});

builder.Services.AddSingleton(services =>
    new SummaryEnricher(services.GetRequiredService<EnricherOptions>(), maxWordCount: 24));

builder.Services.AddSingleton(services =>
    new ClassificationEnricher(services.GetRequiredService<EnricherOptions>(), Topics, fallbackClass: "None"));
```

In the background, [`SessionEnrichmentService.cs`](../src/ConferenceApp/AI/Data/SessionEnrichmentService.cs) wraps each talk's `session.EmbeddingText` in an `IngestionChunk<string>`, pipes it through `summaryEnricher` and `classifier`, validates `topic` against `DataExtensions.Topics` (`["AI", "Mobile", "Web", "Cloud", "Data", "IoT"]`) so small-model hallucinations are discarded, updates `session.Summary` and `session.Topic` directly on the in-memory `Session` objects, and sends `SessionsEnrichedMessage` via `WeakReferenceMessenger` so Agenda cards populate their topic chips and summaries live:

```csharp
var chunk = new IngestionChunk<string>(
    session.EmbeddingText,
    new IngestionDocument(session.Id));

await foreach (var enriched in classifier.ProcessAsync(summaryEnricher.ProcessAsync(SingleChunk(chunk))))
{
    session.Summary = ReadMetadata(enriched, SummaryEnricher.MetadataKey);
    var topic = ReadMetadata(enriched, ClassificationEnricher.MetadataKey);
    session.Topic = DataExtensions.Topics.Contains(topic, StringComparer.Ordinal) ? topic : null;
}

WeakReferenceMessenger.Default.Send(new SessionsEnrichedMessage());
```

---

### 4. Source-Generated AI Tool Contexts (`AI/Tools/` — ~3 mins)

Open [`src/ConferenceApp/AI/Tools/ToolExtensions.cs`](../src/ConferenceApp/AI/Tools/ToolExtensions.cs), [`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs), and [`FavoritesTools.cs`](../src/ConferenceApp/AI/Tools/FavoritesTools.cs).

Using `Microsoft.Maui.AI.Attributes`, a one-line `[AIToolSource]` partial class source-generates an AOT-friendly `AIToolContext` (`Default.Tools`) with zero reflection. Tools are split into two stateless contexts so read-only research agents never receive state-mutating tools:

```csharp
[AIToolSource(typeof(ConferenceDataTools))]
public partial class ConferenceDataToolContext : AIToolContext;
```

| Tool Context | Tools | Purpose |
|---|---|---|
| **`ConferenceDataToolContext`** | `search_sessions`, `get_session`, `get_speaker`, `list_sessions` | Read-only schedule, abstract, and speaker bio lookup. |
| **`FavoritesToolContext`** | `get_favorites`, `add_favorite`, `remove_favorite` | Reads and mutates saved favourites in `UserPreferencesService` (visible live in DevFlow's **Storage** tab). |

Point out two `Microsoft.Extensions.AI` features in [`ConferenceDataTools.cs`](../src/ConferenceApp/AI/Tools/ConferenceDataTools.cs):

#### Method-Level DI with `[FromServices]`
`ConferenceDataTools` doesn't inject `SearchService` in its constructor just for one method. Instead, `SearchSessionsAsync` uses `[FromServices] SearchService search` right on the tool parameter, resolved automatically from the `IServiceProvider` configured on the chat client:

```csharp
[ExportAIFunction("search_sessions")]
[Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
public async Task<string> SearchSessionsAsync(
    [Description("Topic, title, or speaker to search for.")] string query,
    [FromServices] SearchService search,
    CancellationToken cancellationToken = default)
```

#### Strongly-Typed `TimeOnly` Tool Schemas
`ListSessionsAsync` takes `TimeOnly start` and `TimeOnly end` directly—`AIJsonUtilities` generates `"format": "time"` in the JSON schema and deserializes `"08:00"` and `"12:00"` without manual string parsing:

```csharp
[ExportAIFunction("list_sessions")]
[Description("List sessions in the current city between two 24-hour daytime times (for example 08:00 to 12:00).")]
public async Task<string> ListSessionsAsync(
    [Description("Start time in 24-hour HH:mm format (e.g. 08:00).")] TimeOnly start,
    [Description("End time in 24-hour HH:mm format (e.g. 12:00).")] TimeOnly end,
    CancellationToken cancellationToken = default)
```

---

### 5. Context Providers, Multi-Agent Workflows & Semantic Routing (`AI/Agents/` — ~4 mins)

Open [`src/ConferenceApp/AI/Agents/AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs) and the files in `AI/Agents/`.

#### Keyed `AIContextProvider` Singletons
In `AgentExtensions.AddAgents()`, three composable context providers are registered as keyed `AIContextProvider` singletons so each agent or workflow can request only the context slices it needs:

```csharp
builder.Services.AddKeyedSingleton<AIContextProvider, UserContextProvider>("user");
builder.Services.AddKeyedSingleton<AIContextProvider, ScheduleContextProvider>("schedule");
builder.Services.AddKeyedSingleton<AIContextProvider>("rag", (services, _) => RagContextProvider.Create(services));
builder.Services.AddKeyedChatClient("assistant", CreateAssistantRouter);
```

| Key | File | What it injects into `AIContext` |
|---|---|---|
| `"user"` | [`UserContextProvider.cs`](../src/ConferenceApp/AI/Agents/UserContextProvider.cs) | Active city, event date, current conference time (`08:30`), venue name/address, and saved favourites with `[Topic]` tags. |
| `"schedule"` | [`ScheduleContextProvider.cs`](../src/ConferenceApp/AI/Agents/ScheduleContextProvider.cs) | Full day schedule (`Time \| Track [Topic] \| Title (Speakers)`) for the active city so agents answer schedule and clash questions without extra tool calls. |
| `"rag"` | [`RagContextProvider.cs`](../src/ConferenceApp/AI/Agents/RagContextProvider.cs) | `TextSearchProvider` (`BeforeAIInvoke`) that filters to the latest user message, finds the top 3 talks via `SearchService`, and injects their IDs, titles, and AI-enriched summaries (`session.Summary`). |

Notice how the whole pipeline connects: `SessionEnrichmentService` pre-computes compact `session.Summary` strings $\rightarrow$ `RagContextProvider` injects the top 3 summaries into context $\rightarrow$ the agent calls `get_session` / `get_speaker` only when it needs a full abstract or bio. Every agent is also configured with `UseProvidedChatClientAsIs = true`, `Temperature = 0`, and tight `MaxOutputTokens` budgets tailored for an on-device model.

#### Three Non-Overlapping Agent & Workflow Architectures

##### Default Assistant ([`ConferenceAssistantAgent.cs`](../src/ConferenceApp/AI/Agents/ConferenceAssistantAgent.cs))
A single `AIAgent` configured with `[userContext, scheduleContext, ragContext]` and both tool contexts (`ConferenceDataToolContext` + `FavoritesToolContext`) for fast schedule lookups, speaker bios, clash checks, and adding/removing favourites.

##### Session Deep-Dive Workflow ([`SessionDeepDiveWorkflow.cs`](../src/ConferenceApp/AI/Agents/SessionDeepDiveWorkflow.cs) — `BuildSequential` + `BuildConcurrent`)
Combines sequential and concurrent orchestration using `Microsoft.Agents.AI.Workflows`. First, `SessionResearcher` calls `get_session` and `get_speaker` to gather the target talk's full abstract and speaker biography into a factual brief. Next, `AgentWorkflowBuilder.BuildConcurrent` fans out in parallel to **`TakeawaysAgent`** (what to expect & 3 key technical takeaways) and **`QuestionsCoachAgent`** (2 things to think about & 2 smart Q&A questions for the speaker):

```csharp
var parallelAnalysis = AgentWorkflowBuilder
    .BuildConcurrent("DeepDiveParallelAnalysis", [takeawaysAgent, questionsCoachAgent])
    .AsAIAgent(name: "DeepDiveParallelAnalysis");

return AgentWorkflowBuilder.BuildSequential(
    "SessionDeepDiveWorkflow",
    [sessionResearcher, parallelAnalysis]);
```

##### Talk Recommendation & Debate Workflow ([`TalkRecommendationWorkflow.cs`](../src/ConferenceApp/AI/Agents/TalkRecommendationWorkflow.cs) — `CreateGroupChatBuilderWith`)
A multi-turn 6-agent group chat managed by `RoundRobinGroupChatManager`:

```text
Turn 1 (Pause):  Concierge ──► (pauses at IterationCount >= 1 for user confirmation)
Turn 2 (Debate): Concierge ──► Timekeeper ──► DepthAdvocate ──► BreadthAdvocate ──► PracticalAdvocate ──► Moderator
```

On **Turn 1**, `Concierge` checks the attendee's `Saved Favourites` (as a starting point) and asks them to confirm or pick their topics and any arrival/departure time constraints; the custom `shouldTerminateFunc` pauses the group chat after `Concierge` (`IterationCount >= 1`) so the user can reply.

On **Turn 2**, `Concierge` outputs the confirmed topics and 24-hour time window (e.g. `08:00 to 12:00`), `Timekeeper` calls `list_sessions(start, end)` *first* so only talks inside that window are on the table, `DepthAdvocate`, `BreadthAdvocate`, and `PracticalAdvocate` each pitch 1–2 talks from `Timekeeper`'s list, and `Moderator` wraps up with a conflict-free recommended lineup.

#### Two-Layer Routing: `RoutingChatClient.Create` + `SemanticRoutingChatClient` ([`AgentExtensions.cs`](../src/ConferenceApp/AI/Agents/AgentExtensions.cs))
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

### 6. The UI Side of Multi-Agent Streaming ([`Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs) — ~3 mins)

Open [`src/ConferenceApp/Services/ChatService.cs`](../src/ConferenceApp/Services/ChatService.cs) to show how a mobile UI makes multi-agent workflows feel fast and readable:

#### Progress Status & Hiding Internal Handoff Steps
Internal research/filtering turns (`SessionResearcher`, `Timekeeper`, and `Concierge`'s 2-line handoff on Turn 2) update `reply.Progress` (bound to `chat-progress` in `ChatPage.xaml`, e.g. *"Timekeeper: filtering talks to your time window..."*) instead of dumping raw intermediate tool output into the chat bubble. Their actual tool calls (`[AI Tool] list_sessions(start: 08:00, end: 12:00)`) remain visible in `maui devflow logs`.

#### De-Interleaving Concurrent Streams (`BuildConcurrent`)
When `TakeawaysAgent` and `QuestionsCoachAgent` run in parallel via `BuildConcurrent`, both stream tokens at the same time. `ChatService` streams `TakeawaysAgent` live into the bubble under **`**Key Takeaways:**`** while buffering `QuestionsCoachAgent` into a `StringBuilder`, then appends **`**Things to Think About & Ask:**`** cleanly once `TakeawaysAgent` finishes.

#### Preserving `AuthorName` Across Turns & Attaching Tappable `Session` Cards
At the end of the stream, `ChatService` stores `updates.ToChatResponse().Messages` on `reply.ResponseMessages` so each message's `AuthorName` (`"Concierge"`, `"Moderator"`, etc.) is preserved in `history` for `RoutingChatClient` on the next turn. Finally, `AddReferencedSessionsAsync` scans the reply for any mentioned talk titles or IDs in the current city and populates `reply.Sessions`, which `ChatPage.xaml` renders as tappable `chat-session-card-{Id}` cards that navigate straight to `SessionPage`!

---

### Live Demo Script on the Assistant Tab

1. **Default Assistant + Schedule Context + Tools:** Tap **`When is Matthew's talk?`** or **`Do my favourites clash?`**. Point out `[AI RAG]` in the logs and tap the resulting session card underneath the reply.
2. **Sequential + Concurrent Fan-Out Workflow (`BuildSequential` + `BuildConcurrent`):** Tap **`Prepare me for Matthew's talk: takeaways and questions`**. Watch the progress indicator show `SessionResearcher` reading the abstract and speaker bio (`[AI Tool] get_session` + `get_speaker` in logs), followed by `TakeawaysAgent` and `QuestionsCoachAgent` running in parallel!
3. **Multi-Turn Group Chat Workflow (`CreateGroupChatBuilderWith`):** Tap **`Can you suggest some talks for me?`** (or the **`Plan my day`** button). When `Concierge` asks to confirm your topics and time window, reply with **`AI and Cloud, and I have to leave before noon`**. Watch the progress status show `Timekeeper` filtering the schedule (`[AI Tool] list_sessions(start: 08:00, end: 12:00)` in logs) before `DepthAdvocate`, `BreadthAdvocate`, `PracticalAdvocate`, and `Moderator` debate only morning talks and produce tappable session cards!
