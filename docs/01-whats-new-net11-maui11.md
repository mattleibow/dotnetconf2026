# Part 1: What's New in .NET 11, C# 15 & .NET MAUI 11 (5–8 mins)

Open these files before [Prompt 01](../.github/prompts/01-explore-app.prompt.md) to show a small, modern baseline app.

**Branch boundary:** examples below describe `main`. The complete guide also covers the [separate native AI stack](../README.md#demo-branches), whose model assets and registration are called out explicitly.

Sources: [What's new in .NET 11](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview) and [What's new in .NET MAUI 11](https://learn.microsoft.com/dotnet/maui/whats-new/dotnet-11). Check the installed SDK/workload and native toolchain rather than inferring compatibility from a framework name.

## 1. Small solution, central packages, clean outputs

- [`ConferenceApp.slnx`](../ConferenceApp.slnx): compact XML solution format; easier to read and merge, not a guarantee against conflicts.
- [`Directory.Build.props`](../Directory.Build.props): `UseArtifactsOutput=true` puts build outputs under the root `artifacts` directory.
- [`Directory.Packages.props`](../Directory.Packages.props): central package versions, including CommunityToolkit.Mvvm **8.4.2**, with versionless references in the app project. MAUI packages use the workload's `MauiVersion`.

`.slnx`, central package management, file-based apps, primary constructors, and observable partial properties are useful modern .NET features, not all inventions of .NET 11. C# 15 is the default language version for .NET 11 targets.

## 2. CoreCLR, XAML source generation, and real offline assets

[`ConferenceApp.csproj`](../src/ConferenceApp/ConferenceApp.csproj) targets .NET 11. CoreCLR is the default MAUI runtime; leave runtime defaults alone rather than copying old Mono/interpreter workarounds. On Apple mobile platforms, CoreCLR uses precompiled code and an interpreter where JIT is unavailable—“CoreCLR” does not mean desktop-style JIT everywhere.

XAML Source Generation (`MauiXamlInflator=SourceGen`), implicit XAML namespace declarations, and lazy resource dictionary creation are defaults. XAML Incremental Hot Reload is enabled by default for **Debug**, not Release/publish. Preview **C# expressions in XAML** still require `EnablePreviewFeatures=true`, which this project sets.

The baseline links the conference snapshot directly as raw assets:

```xml
<MauiAsset Include="..\..\data\conference\**"
           LogicalName="conference/%(RecursiveDir)%(Filename)%(Extension)" />
```

[`ConferenceDb`](../src/ConferenceApp/Data/ConferenceDb.cs) opens `conference/conference.json` with `FileSystem.OpenAppPackageFileAsync`. JSON, Markdown abstracts/bios, and real images are packaged; the app does not import data at startup. Map tiles and external links are separate network-dependent features.

Only the AI stack adds the `models/all-MiniLM-L6-v2` ONNX assets. See [Part 3](03-giving-the-app-ai.md); there is no `models` directory on `main`.

## 3. Global XAML namespaces and compiled bindings

[`GlobalXmlns.cs`](../src/ConferenceApp/GlobalXmlns.cs) registers app namespaces and `Microsoft.Maui.Controls.Maps` under the MAUI global namespace. XAML can use `<Map />`, `<BasePage />`, view models, models, and converters without repetitive namespace aliases. An AI namespace registration is present for the later demo but does not imply an AI implementation on `main`.

Keep the correct `x:DataType` on pages and templates. .NET 11 compiles resolvable `RelativeSource` ancestor bindings in an AOT-safe way; target the view model directly rather than reaching through a page's `BindingContext`.

For example, the actual pattern in [`AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) is:

```xml
<TapGestureRecognizer
    Command="{Binding ToggleFavoriteCommand, Source={RelativeSource AncestorType={x:Type AgendaViewModel}}, x:DataType=AgendaViewModel}"
    CommandParameter="{Binding .}" />
```

That recognizer operates in a `Session`-typed item template. Tappable layouts have `Background="Transparent"` so hit testing works on iOS and Mac Catalyst.

## 4. C# expressions and current control APIs

Use expressions for small display decisions, not hidden business logic:

```xml
AutomationId="{$'session-{Id}'}"
IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"
```

[`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml) uses `CollectionView.EmptyView` for suggestions and the availability/busy expression to disable inputs. On `main`, the unavailability reason is always set; the presence of chat controls is not evidence of a connected model.

Other useful MAUI 11 notes (not a checklist of features used by this app):

| API / behavior | Demo guidance |
|---|---|
| `BoxView.Fill` accepts a brush, including gradients | Decorative surfaces don't require custom drawing |
| `FadeToAsync`, `ScaleToAsync`, `TranslateToAsync` | Prefer the cancellable async animation APIs; old non-`Async` variants are obsolete |
| `LongPressGestureRecognizer` | Built-in; no gesture framework needed |
| `SwipeItem.IconColor` / `TextColor` | Explicit theme-aware swipe-action colors |
| `Window.StatusBarTheme` | Status-bar icon contrast is independent of the app theme on Android / iOS |
| `UseMaterial3=true` | This app opts into Android Material 3 rather than adding custom renderers |
| `MauiAndroidSystemBarsUseMauiChrome=true` | This app opts into Android system-bar background tinting from Shell chrome |
| Shell route templates | Available, but this app uses ordinary `session?id=...` and `speaker?id=...` detail routes |
| `TabbedPage.BadgeText` | A `TabbedPage` API, not a Shell-tab badge API |

Use handlers, not removed compatibility renderers. For navigation-transition-dependent work on the new Apple `NavigationPage` handler, consider `NavigatedTo`; this is not a blanket rule to replace every page's `Appearing`.

## 5. Simple MVVM and navigation

[`BaseViewModel`](../src/ConferenceApp/ViewModels/BaseViewModel.cs) centralizes data access, preferences, dispatcher, logger, city changes, and load/city-picker commands. Page view models use primary constructors, `[ObservableProperty]` partial properties, and `[RelayCommand]`.

[`BasePage`](../src/ConferenceApp/Views/BasePage.cs) sets the binding context and runs `LoadCommand` from `OnAppearing`. Most page code-behind just accepts its view model and initializes XAML. This lifecycle choice describes the current app, not a guarantee that a navigation transition has finished.

[`AppShell.xaml`](../src/ConferenceApp/AppShell.xaml) defines separate Agenda, Event, Speakers, and Assistant tabs. [`AppShell.xaml.cs`](../src/ConferenceApp/AppShell.xaml.cs) registers `session` and `speaker`; view-model commands navigate with query-string IDs. The app does not fake tabs with visibility switches.

## 6. File-based C# utilities

[`data/ImportConference.cs`](../data/ImportConference.cs) is a file-based app, not another project:

```csharp
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:package AngleSharp@1.10.1
```

`dotnet run data/ImportConference.cs` deliberately refreshes the snapshot and needs website access. It is not a build or app-startup prerequisite. The separate AI stack adds a model-download utility; don't run a nonexistent `models/DownloadModels.cs` on `main`.

## 7. Startup, DevFlow, and the data layer

[`MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) registers:

```csharp
builder.Services.AddSingleton<ConferenceDbProvider>();
builder.Services.AddSingleton<BaseBackgroundService, DatabasePreloadService>();
builder.Services.AddSingleton<UserPreferencesService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<ChatService>();
```

Pages and view models are registered separately with `AddTransient<Page>().AddTransient<ViewModel>()`. In Debug, it enables logging and `AddMauiDevFlowAgent` with layout diagnostics and profiling. There is **no `AddConferenceAI()` call on `main`**.

[`App.xaml.cs`](../src/ConferenceApp/App.xaml.cs) starts the registered `IEnumerable<BaseBackgroundService>` at app creation; on `main` this is the database preload service. [`ConferenceDbProvider`](../src/ConferenceApp/Data/ConferenceDbProvider.cs) shares one lazy asynchronous load. [`ConferenceDb`](../src/ConferenceApp/Data/ConferenceDb.cs) caches Markdown content, links city/venue and session/speaker relationships, and orders the data.

[`UserPreferencesService`](../src/ConferenceApp/Services/UserPreferencesService.cs) stores only local user state: `selected_city` and comma-separated `favorites`. [`SearchService`](../src/ConferenceApp/Services/SearchService.cs) is literal substring search; [`ChatService`](../src/ConferenceApp/Services/ChatService.cs) returns an explicit unavailable reason.

**On the AI stack**, `builder.AddConferenceAI()` adds clients, embeddings, enrichment, tools, and agents. Vector indexing is independent of enrichment. The enrichment revision uses **two parallel background services**—one processes sessions, the other speakers—with a nested typed output model in each, not the older single sequential `ConferenceEnrichmentService`. Final service/type names are being revised in [#39](https://github.com/mattleibow/dotnetconf2026/pull/39); see [Part 3](03-giving-the-app-ai.md#3-typed-app-data--background-enrichment-aidata--3-mins).

### Cooperative shutdown, not a process-exit promise

`App` owns a shutdown `CancellationTokenSource` and passes its token to `BaseBackgroundService.StartAsync`. A single `Window.Destroying` handler cancels it; there is no extra `MauiProgram` lifecycle hook.

On Android that handler cancels only when `window.Handler?.PlatformView is Android.App.Activity { IsFinishing: true }`, not when Android is recreating an activity. For the MAUI 11 window lifecycle, destruction corresponds to Android `OnDestroy`, iOS / Mac Catalyst `SceneDidDisconnect` (not the legacy `WillTerminate` path), and Windows `Closed`. See [MAUI app lifecycle](https://learn.microsoft.com/dotnet/maui/fundamentals/app-lifecycle?view=net-maui-11.0).

`Stopped` / `OnSleep` means backgrounding, not exit; permanently cancelling these once-only services there would leave them stopped after resuming. Mobile operating systems can kill a process without calling cleanup callbacks. In particular, Android [`Application.OnTerminate`](https://learn.microsoft.com/dotnet/api/android.app.application.onterminate) is not called on production devices.

Cancellation is cooperative: expected token cancellation is quiet, other errors are logged. The preload shares `ConferenceDbProvider`'s uncancellable load task, so an already-running database load can finish. Derived background services must observe the token themselves; this mechanism cannot guarantee native work or cleanup completes before process termination.

Next: [**Part 2 — Agents Building Apps**](02-agents-building-apps.md)
