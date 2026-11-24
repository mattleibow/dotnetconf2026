# Copilot instructions

This repository is a demo conference app built live with coding agents. It is rebuilt from scratch for each event, so keep it **simple, clean, working, and beautiful** rather than feature-complete.

## Stack

- Target **.NET 11** (`net11.0-*`) and **.NET MAUI 11**. Verify the selected SDK, workload, and platform-tool versions are compatible; don't infer the installed toolchain or Xcode compatibility from the target framework alone.
- Primary demo targets are Android and iOS; Mac Catalyst is a convenient local target. Do not add platform targets that aren't needed.
- Use C# with XAML, compiled bindings (correct `x:DataType` for binding contexts and data templates), and MVVM. `CommunityToolkit.Mvvm` is fine; use a current compatible release (8.4.2 or newer for the tested C# 15 partial-property setup). Avoid other frameworks unless asked.
- Use modern C# (C# 15 is available) where it makes the code clearer. Do not force new language features into the app.

## .NET 11 / MAUI 11 notes

Prefer current APIs over training-data habits:

- CoreCLR is the default MAUI runtime. Leave the default runtime settings alone; don't copy old Mono or interpreter configuration workarounds.
- XAML Source Generation (`MauiXamlInflator=SourceGen`), lazy `ResourceDictionary`, and implicit XAML namespaces are defaults in .NET 11. XAML Incremental Hot Reload is enabled by default in **Debug**, not Release/publish, and requires a Hot Reload host such as `dotnet watch` or an IDE; plain `dotnet run` does not watch edits.
- C# Expressions in XAML are enabled via `<EnablePreviewFeatures>true</EnablePreviewFeatures>`: use inline C# expressions (`IsVisible="{Topics.Count > 0}"`, `IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"`, `Text="{IsFavorite ? 'Remove from favourites' : 'Add to favourites'}"`, `AutomationId="{$'chat-session-card-{Id}'}"`) instead of writing `HasXyz` / `InverseBooleanConverter` / `StringFormat` boilerplate properties on Models and ViewModels.
- `src/ConferenceApp/GlobalXmlns.cs` registers `ConferenceApp.AI`, `ConferenceApp.Converters`, `ConferenceApp.Data`, `ConferenceApp.Services`, `ConferenceApp.ViewModels`, `ConferenceApp.Views`, and `Microsoft.Maui.Controls.Maps` into `http://schemas.microsoft.com/dotnet/maui/global`. Do not add `xmlns`, `xmlns:x`, `xmlns:vm`, `xmlns:m`, `xmlns:views`, or `xmlns:maps` declarations to XAML files—use unprefixed type names directly (`x:DataType="AgendaViewModel"`, `<Map ... />`, `{DataTemplate EventPage}`). Keep `GlobalXmlns.cs` updated if you add a new XAML namespace.
- Use .NET 11 AOT-safe compiled `RelativeSource` bindings inside `DataTemplate`s by targeting the ViewModel directly (`Source={RelativeSource AncestorType={x:Type AgendaViewModel}}, x:DataType=AgendaViewModel`) rather than `BindingContext.` on `ContentPage`.
- Use the `Async` animation methods (`FadeToAsync`, `ScaleToAsync`, `TranslateToAsync`, and so on); the non-`Async` variants are obsolete. They accept a `CancellationToken`.
- Shell supports route templates such as `Routing.RegisterRoute("session/{id}", typeof(SessionPage))`. Route templates currently require absolute navigation.
- `<UseMaterial3>true</UseMaterial3>` in the `.csproj` enables Material 3 styling across Android controls and `Shell` (`Maui.Material3.Theme.NoActionBar`). Don't fight it with custom renderers.
- `BoxView.Fill` accepts any `Brush`, including gradients. Use it for decorative surfaces before reaching for custom drawing.
- `LongPressGestureRecognizer` is built in.
- `TabbedPage` children support badges through attached properties such as `TabbedPage.BadgeText`. This isn't a Shell tab badge API.
- `SwipeItem` supports `IconColor` and `TextColor`.
- `Window.StatusBarTheme` controls status bar contrast independently of the app theme on Android and iOS.
- `<MauiAndroidSystemBarsUseMauiChrome>true</MauiAndroidSystemBarsUseMauiChrome>` in the `.csproj` enables .NET 11 Android edge-to-edge system bar background tinting from MAUI `Shell` chrome (`Shell.BackgroundColor` for the top `AppBarLayout`/status bar and `Shell.TabBarBackgroundColor` for the bottom navigation bar).
- Use handlers, not renderers. `Microsoft.Maui.Controls.Compatibility` no longer ships.
- With the new iOS/Mac Catalyst `NavigationPage` handler, use `NavigatedTo` rather than `Appearing` for work that assumes the navigation transition has completed. Don't apply this as a blanket lifecycle rule for every page.
- .NET 11 `dotnet run` supports `--list-devices` and `--device <id>` for mobile targets; see [Running the app](#running-the-app).

When unsure about a .NET 11 or MAUI 11 API, check Microsoft Learn first:

- https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview
- https://learn.microsoft.com/dotnet/maui/whats-new/dotnet-11

## App architecture

- Keep the structure flat and easy to read on a projector: `AI/`, `Converters/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, and `Resources/`.
- The app lives in `src/ConferenceApp/` with `ConferenceDbProvider` and `ConferenceDb` in `Data/` (read-only conference data access), `UserPreferencesService` (city and favourites in `Preferences`), `SearchService`, and `ChatService`. `builder.AddConferenceAI()` in `AI/ConferenceAIExtensions.cs` adds ONNX semantic search over `VectorStoreCollection<string, Session>`, streaming chat, tools, and agents. Launched by the Aspire AppHost (`src/ConferenceApp.AppHost`), the default `IChatClient` tries the LiteLLM gateway first and falls back to on-device AI; otherwise it is on-device only.
- Register pages, view models, and services with dependency injection in `MauiProgram.cs`. Never call `AddTransient<Page, ViewModel>()`: register them separately with `AddTransient<Page>().AddTransient<ViewModel>()`.
- Use Shell for navigation: one page per top-level section, and routes for detail pages. Never fake tabs by toggling `IsVisible` on one page.
- One view model per page, with commands (`CommunityToolkit.Mvvm`) rather than event handlers in code-behind.
- Conference content is linked in `ConferenceApp.csproj` directly from `data/conference/` as raw `MauiAsset` files (`LogicalName="conference/..."`) and loaded with `FileSystem.OpenAppPackageFileAsync("conference/...")`. Don't scrape the website, change the importer, or need network access for this content.
- Store user state such as favourites locally with `Preferences` or a small JSON file. Don't add a database unless it's actually needed.
- Put app behavior in services with small, intention-revealing methods, such as `GetSessionsAsync`, `GetSessionAsync`, `SetFavorite`, `SearchSessionsAsync`, and navigation helpers.
- All AI services and `Microsoft.Extensions.AI` chat/function pipelines log structured `[AI]`, `[AI Search]`, `[AI Chat]`, `[AI Tool]`, and `[AI Workflow]` entries to `ILogger`, which you can watch live via `maui devflow logs --follow --no-json` or the **Logs** tab in the DevFlow Web Inspector.

## Design

Follow [`DESIGN.md`](../DESIGN.md) and [`PRODUCT.md`](../PRODUCT.md) for colours, type, spacing, components and layouts, using the `impeccable` skill when designing or styling UI. Put tokens into `Colors.xaml` and `Styles.xaml` before styling pages, and make every page work on a phone in portrait and in a wide desktop window.

## Skills

Use the installed skills rather than guessing. Load the ones that match the task before writing code:

- **Design:** `impeccable` (reads `PRODUCT.md`, `DESIGN.md`, and `.impeccable/config.json`).
- **MAUI Labs** ([dotnet/maui-labs](https://github.com/dotnet/maui-labs)): `maui-project-structure`, `maui-app-architecture`, `maui-ui-patterns`, `maui-localization-theming`, `maui-accessibility`, `maui-current-apis`, `maui-app-assets-lifecycle`, `maui-device-capabilities`; DevFlow: `maui-devflow-onboard` and `maui-devflow-debug` (its `references/batch.md` covers driving several UI steps at once).
- **.NET skills** ([dotnet/skills](https://github.com/dotnet/skills), optional): `csharp-expert` and `msbuild` from the `dotnet` plugin.
- **In-app AI, only when asked:** `maui-essentials-ai` and `maui-ai-tool-bindings`. These APIs are experimental, so check the linked MAUI Labs docs too.

## Agent workflow

### Running the app

- Use .NET 11 `dotnet run` device selection instead of manual build/install/launch chains. From `src/ConferenceApp` (so project-relative launch paths resolve with `UseArtifactsOutput`):

  ```sh
  dotnet run --framework net11.0-ios --list-devices
  dotnet run --framework net11.0-ios --device <device-id> --no-launch-profile
  ```

  `--no-launch-profile` bypasses the Windows-only launch profile. Don't hardcode runtime identifiers or `.app` paths, don't use `simctl install`/`launch` for normal runs, and let a fresh worktree's first run restore.
- `dotnet run` keeps running while the app runs: start it in a tool-managed background session and treat DevFlow readiness, not process exit, as success. To redeploy, stop only your own launch process and run it again.
- Plain `dotnet run` doesn't watch edits. Use `dotnet watch` (or an IDE) only when XAML Incremental Hot Reload is wanted, after checking its current iOS requirements.
- Run and build one target framework at a time, only for the requested platform(s). For general cross-platform UI work, Mac Catalyst is a convenient local loop before final iOS/Android checks.
- Reuse a booted simulator (`xcrun simctl list devices booted`). Normally quit apps you launched and shut down simulators you booted, but leave the app, simulator, and launch process running when preparing a live demo. Never stop shared devices or unrelated processes.
- To run with the AI gateway, start the Aspire AppHost (`aspire start` from the repository root) and launch the app resources from the dashboard; see [the gateway runbook](../docs/04-ai-gateway.md).
- Don't change shared toolchain settings or install workloads without approval. If something can't run, say what's unverified rather than retrying indefinitely.

### Driving the app with DevFlow

- `Microsoft.Maui.DevFlow.Agent` is registered in Debug only with `builder.AddMauiDevFlowAgent(...)`, and `Platforms/MacCatalyst/Entitlements.plist` includes `com.apple.security.network.server` so the agent can listen on Mac Catalyst.
- After launching, wait with `maui devflow wait --wait-platform <platform> --timeout 120`, confirm app/platform/device with `maui devflow agent status`, and pass the discovered `--agent-port` to later commands.
- Reuse a running app only when it is known to come from the intended workspace/branch; the same bundle ID can be installed from several branches.
- When available, open the **Mobile Device** canvas (`canvasId: "mobile-device"`) and `select_device` the simulator, and open the agent's Inspector URL from `maui devflow list` in a **Browser** canvas (`canvasId: "browser"`, `title: "DevFlow Inspector"`). Check canvas capabilities first; if a surface is unavailable, say so and continue.
- DevFlow preview writer leases: the Inspector claims one on load, so release it via the Browser canvas's `evaluate_javascript` (`window.dispatchEvent(new Event('pagehide'));`) before CLI-driven UI work. Each separate CLI process also holds a lease for about 10 seconds, so pipe related `ui`/`webview` steps into one `maui devflow batch` with readiness queries (`ui query --automationId <id> --wait-until exists`). Run `storage` and `logs` commands outside batches. Don't force-take another session's lease or toggle `RequireMutationLease` to fix contention; see [preview leases](../docs/02-agents-building-apps.md#devflow-preview-leases).
- Discover actual AutomationIds from the UI tree rather than guessing; list items may not be realized in the current viewport.
- `maui devflow storage preferences clear` doesn't reset live view-model state. Clear search with `ui clear agenda-search` (an empty `ui fill` is rejected) and set filters explicitly; the default `selected_city` may be recreated.
- Correlate `maui devflow logs` with the current reproduction's timestamps; logs from earlier launches persist. Check measured bounds, not just the diagnostics findings count.
- Give interactive elements and list items a stable kebab-case `AutomationId` (`agenda-search`, `session-card-<id>`, `favorite-button`), and make list items tappable with a tap gesture or command rather than `CollectionView` selection. Always set `Background="Transparent"` on layouts that have a `TapGestureRecognizer` so taps register on iOS and Mac Catalyst.

### Verifying changes

- Verify changes on the running build with DevFlow, even if the diff looks correct. Inspect screenshots against the design guide on the requested platform(s); cover phone and wide desktop layouts for cross-platform UI changes. Fix anything broken, clipped or off-brand.
- Apple's on-device model is small: keep structured-output types flat with no nullable properties, and keep tool sets small with clearly distinct names.

## Code style

- Keep code small and obvious; optimize for a live demo audience.
- Avoid speculative abstractions and generic repositories.
- Comment only where the reason isn't obvious.
- Remove template boilerplate that the app doesn't use.
