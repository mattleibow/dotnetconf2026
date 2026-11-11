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

- Keep the structure flat and easy to read on a projector: `Models/`, `Services/`, `ViewModels/`, `Views/`, and `Resources/`.
- Create the MAUI project in its own directory, such as `src/ConferenceApp/`, so it doesn't compile the scripts in `data/` and `models/`.
- Register pages, view models, and services with dependency injection in `MauiProgram.cs`.
- Use Shell for navigation: one page per top-level section, and routes for detail pages. Never fake tabs by toggling `IsVisible` on one page.
- One view model per page, with commands (`CommunityToolkit.Mvvm`) rather than event handlers in code-behind.
- Conference content comes from the `data/conference/` snapshot, described in `data/README.md`. Copy it into the app's `Resources/Raw/conference/` (raw `MauiAsset` files) and load it with `FileSystem.OpenAppPackageFileAsync`. Don't scrape the website, change the importer, or need network access for this content.
- Store user state such as favourites locally with `Preferences` or a small JSON file. Don't add a database unless it's actually needed.
- Put app behavior in services with small, intention-revealing methods, such as `GetSessionsAsync`, `GetSessionAsync`, `SetFavoriteAsync`, `SearchSessions`, and navigation helpers. These should be useful on their own and easy to expose to an in-app AI assistant later.
- Do not add AI features unless explicitly asked.

## Design

Follow [`docs/design.md`](../docs/design.md) for colours, type, spacing, components and layouts. Put it into `Colors.xaml` and `Styles.xaml` before building pages, and make every page work on a phone in portrait and in a wide desktop window.

## Skills

Use the installed skills rather than guessing. Load the ones that match the task before writing code:

- **MAUI Labs** ([dotnet/maui-labs](https://github.com/dotnet/maui-labs)): `maui-project-structure`, `maui-app-architecture`, `maui-ui-patterns`, `maui-localization-theming`, `maui-accessibility`, `maui-current-apis`, `maui-app-assets-lifecycle`, `maui-device-capabilities`; DevFlow: `maui-devflow-onboard` and `maui-devflow-debug` (its `references/batch.md` covers driving several UI steps at once).
- **.NET skills** ([dotnet/skills](https://github.com/dotnet/skills)): `maui-theming`, `maui-shell-navigation`, `maui-collectionview`, `maui-data-binding` and `maui-safe-area` from `dotnet-maui`, and `csharp-expert` and `msbuild` from `dotnet`.
- **In-app AI, only when asked:** `maui-essentials-ai` and `maui-ai-tool-bindings`. These APIs are experimental, so check the linked MAUI Labs docs too.

## Agent workflow

- Add `Microsoft.Maui.DevFlow.Agent` with the version that matches the installed `maui` CLI (`maui --version`); `--prerelease` can pick an older, incompatible build. Register it in Debug only with `builder.AddMauiDevFlowAgent();`.
- Give interactive elements and list items a stable kebab-case `AutomationId` (`agenda-search`, `session-card-<id>`, `favorite-button`), and make list items tappable with a tap gesture or command rather than `CollectionView` selection.
- Work in small loops on Mac Catalyst: change, build, run, check with DevFlow. Build iOS and Android once at the end. Build one target framework at a time, and don't use `--no-restore` on a fresh worktree's first build.
- Check what you changed: run the steps in the prompt, take a phone and a desktop screenshot of the changed screens, and look at them against the design guide. Fix anything broken, clipped or off-brand.
- Apple's on-device model is small: keep structured-output types flat with no nullable properties, and keep tool sets small with clearly distinct names.
- Reuse a booted simulator. When you're done, quit the apps you launched and shut down any simulator you booted.
- Don't change shared toolchain settings or install workloads without approval. If something can't run, say what's unverified rather than retrying indefinitely.

## Code style

- Keep code small and obvious; optimize for a live demo audience.
- Avoid speculative abstractions, base classes, and generic repositories.
- Comment only where the reason isn't obvious.
- Remove template boilerplate that the app doesn't use.
