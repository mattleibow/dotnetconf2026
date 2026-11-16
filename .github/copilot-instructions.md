# Copilot instructions

This repository is a demo conference app built live with coding agents. It is rebuilt from scratch for each event, so keep it **simple, clean, working, and beautiful** rather than feature-complete.

## Stack

- Target **.NET 11** (`net11.0-*`) and **.NET MAUI 11**. Verify the available SDK, workload, and platform-tool versions; don't assume .NET 11 is already stable or that the installed Xcode is compatible.
- Primary demo targets are Android and iOS; Mac Catalyst is a convenient local target. Do not add platform targets that aren't needed.
- Use C# with XAML, compiled bindings (correct `x:DataType` for binding contexts and data templates), and MVVM. `CommunityToolkit.Mvvm` is fine; use a current compatible release (8.4.2 or newer for the tested C# 15 partial-property setup). Avoid other frameworks unless asked.
- Use modern C# (C# 15 is available) where it makes the code clearer. Do not force new language features into the app.

## .NET 11 / MAUI 11 notes

Prefer current APIs over training-data habits:

- CoreCLR is the default MAUI runtime. Leave the default runtime settings alone; don't copy old Mono or interpreter configuration workarounds.
- XAML Source Generation (`MauiXamlInflator=SourceGen`), XAML Incremental Hot Reload, Lazy `ResourceDictionary`, and implicit XAML namespaces are enabled by default in .NET 11.
- C# Expressions in XAML are enabled via `<EnablePreviewFeatures>true</EnablePreviewFeatures>`: use inline C# expressions (`IsVisible="{Topics.Count > 0}"`, `IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"`, `Text="{IsFavorite ? 'Remove from favourites' : 'Add to favourites'}"`, `AutomationId="{$'chat-session-card-{Id}'}"`) instead of writing `HasXyz` / `InverseBooleanConverter` / `StringFormat` boilerplate properties on Models and ViewModels.
- `src/ConferenceApp/GlobalXmlns.cs` registers `ConferenceApp.AI`, `ConferenceApp.Converters`, `ConferenceApp.Data`, `ConferenceApp.Services`, `ConferenceApp.ViewModels`, `ConferenceApp.Views`, and `Microsoft.Maui.Controls.Maps` into `http://schemas.microsoft.com/dotnet/maui/global`. Do not add `xmlns`, `xmlns:x`, `xmlns:vm`, `xmlns:m`, `xmlns:views`, or `xmlns:maps` declarations to XAML files—use unprefixed type names directly (`x:DataType="AgendaViewModel"`, `<Map ... />`, `{DataTemplate EventPage}`). Keep `GlobalXmlns.cs` updated if you add a new XAML namespace.
- Use .NET 11 AOT-safe compiled `RelativeSource` bindings inside `DataTemplate`s by targeting the ViewModel directly (`Source={RelativeSource AncestorType={x:Type AgendaViewModel}}, x:DataType=AgendaViewModel`) rather than `BindingContext.` on `ContentPage`.
- Use the `Async` animation methods (`FadeToAsync`, `ScaleToAsync`, `TranslateToAsync`, and so on); the non-`Async` variants are obsolete. They accept a `CancellationToken`.
- Shell supports route templates such as `Routing.RegisterRoute("session/{id}", typeof(SessionPage))`. Route templates currently require absolute navigation.
- Several core Android controls use Material 3 styling by default. Don't fight it with custom renderers.
- `BoxView.Fill` accepts any `Brush`, including gradients. Use it for decorative surfaces before reaching for custom drawing.
- `LongPressGestureRecognizer` is built in.
- `TabbedPage` children support badges through attached properties such as `TabbedPage.BadgeText`. This isn't a Shell tab badge API.
- `SwipeItem` supports `IconColor` and `TextColor`.
- `Window.StatusBarTheme` controls status bar contrast independently of the app theme on Android and iOS.
- Use handlers, not renderers. `Microsoft.Maui.Controls.Compatibility` no longer ships.
- With the new iOS/Mac Catalyst `NavigationPage` handler, use `NavigatedTo` rather than `Appearing` for work that assumes the navigation transition has completed. Don't apply this as a blanket lifecycle rule for every page.

When unsure about a .NET 11 or MAUI 11 API, check Microsoft Learn first:

- https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview
- https://learn.microsoft.com/dotnet/maui/whats-new/dotnet-11

## App architecture

- Keep the structure flat and easy to read on a projector: `AI/`, `Converters/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, and `Resources/`.
- The app lives in `src/ConferenceApp/` with `ConferenceDbProvider` and `ConferenceDb` in `Data/` (read-only conference data access), `UserPreferencesService` (city and favourites in `Preferences`), `SearchService` (literal + ONNX semantic search over `VectorStoreCollection<string, Session>`), `ChatService` (streaming on-device assistant), and `builder.AddConferenceAI()` in `AI/ConferenceAIExtensions.cs` (`AddChatClients()`, `AddEmbeddings()`, `AddAITools()`, and `AddAgents()`).
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

- `Microsoft.Maui.DevFlow.Agent` is registered in Debug only with `builder.AddMauiDevFlowAgent();`, and `Platforms/MacCatalyst/Entitlements.plist` includes `com.apple.security.network.server` so the DevFlow agent can listen on Mac Catalyst.
- Give interactive elements and list items a stable kebab-case `AutomationId` (`agenda-search`, `session-card-<id>`, `favorite-button`), and make list items tappable with a tap gesture or command rather than `CollectionView` selection. Always set `Background="Transparent"` on layouts that have a `TapGestureRecognizer` so taps register on iOS and Mac Catalyst.
- When running in the GitHub Copilot app with canvases available, open the **Mobile Device** canvas (`canvasId: "mobile-device"`) and select the booted iOS simulator (`select_device`) so the live device is visible in the side panel, and open the **MAUI DevFlow Web Inspector** (`http://localhost:19223/inspector/<agent-id>/` from `maui devflow list`) in a **Browser** canvas (`canvasId: "browser"`, `title: "DevFlow Inspector"`).
- Work in small loops on Mac Catalyst: change, build, run, check with DevFlow. Build iOS and Android once at the end. Build one target framework at a time, and don't use `--no-restore` on a fresh worktree's first build.
- Check what you changed: run the steps in the prompt, take a phone and a desktop screenshot of the changed screens, and look at them against the design guide. Fix anything broken, clipped or off-brand.
- Apple's on-device model is small: keep structured-output types flat with no nullable properties, and keep tool sets small with clearly distinct names.
- Reuse a booted simulator. When you're done, quit the apps you launched and shut down any simulator you booted.
- Don't change shared toolchain settings or install workloads without approval. If something can't run, say what's unverified rather than retrying indefinitely.

## Code style

- Keep code small and obvious; optimize for a live demo audience.
- Avoid speculative abstractions and generic repositories.
- Comment only where the reason isn't obvious.
- Remove template boilerplate that the app doesn't use.
