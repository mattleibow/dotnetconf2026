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
- Create the MAUI project in its own directory, such as `src/ConferenceApp/`, so the standalone `tools/*.cs` apps aren't compiled into it.
- Register pages, view models, and services with dependency injection in `MauiProgram.cs`.
- Use Shell for top-level navigation and routes for detail pages.
- Start from the existing `data/conference/` snapshot. Copy it into the app's `Resources/Raw/conference/` as raw `MauiAsset` files: `conference.json` holds structure and links; `{kind}/{id}.md` holds prose (H1 title, then body), with local PNG/JPG images beside it. The .NET 11 file-based app `tools/ImportConference.cs` refreshes the snapshot before the demo, not during app creation. Don't rewrite the importer, scrape at app runtime, or require website access to use the snapshot. Load assets with `FileSystem.OpenAppPackageFileAsync`; paths in the catalogue are relative to its directory. Don't duplicate prose in JSON.
- Store user state such as favourites locally with `Preferences` or a small JSON file. Don't add a database unless it's actually needed.
- Put app behavior in services with small, intention-revealing methods, such as `GetSessionsAsync`, `GetSessionAsync`, `SetFavoriteAsync`, `SearchSessions`, and navigation helpers. These should be useful on their own and easy to expose to an in-app AI assistant later.
- Do not add AI features unless explicitly asked.

## Design

- Make it look polished: a strong theme, good typography, generous spacing, rounded cards, clear hierarchy, light and dark mode, and subtle motion.
- Use the event's branding where available, while making the app feel like a celebration of .NET 11 and .NET MAUI 11.
- Use `CollectionView` for lists, with useful empty, loading, and error states.
- Images from the event website are fine. Provide graceful fallbacks, such as initials for missing speaker photos.
- Keep accessibility in mind: semantic descriptions, readable contrast, and platform-appropriate touch targets (at least 44 points on Apple and 48 dp on Android).

## Agent workflow

- Discover a compatible prerelease of `Microsoft.Maui.DevFlow.Agent` when needed. Use its `Microsoft.Maui.DevFlow.Agent` namespace and register it in Debug builds only:

  ```csharp
  #if DEBUG
  builder.AddMauiDevFlowAgent();
  #endif
  ```

- Give interactive and important elements a stable, kebab-case `AutomationId`, such as `session-list`, `favorite-button`, and `sponsors-tab`.
- After a meaningful change, build and run the app, then use MAUI DevFlow to inspect the visual tree, interact with the UI, and take screenshots. Fix what you see; don't stop at "it compiles."
- Verify SDK/workload/Xcode compatibility before a long build. Don't change shared toolchain settings or install workloads without approval. If device or model execution is blocked, report the blocker and what remains unverified; don't claim success or retry indefinitely.
- Use available skills when relevant:
  - **dotnet-maui**: `dotnet-maui-doctor`, `maui-shell-navigation`, `maui-collectionview`, `maui-data-binding`, `maui-theming`, `maui-safe-area`, `maui-dependency-injection`, and `maui-app-lifecycle`.
  - **MAUI DevFlow**: `maui-devflow-onboard` and `maui-devflow-debug`.
  - **MAUI Labs, when available**: `maui-current-apis`, `maui-device-capabilities`, and `maui-app-assets-lifecycle`.
  - **dotnet11**: use it for new .NET 11 APIs when relevant.
  - **In-app AI, only when requested**: `maui-essentials-ai` for local clients and embeddings, and `maui-ai-tool-bindings` for source-generated app tools. Verify experimental APIs and platform capabilities against current MAUI Labs docs; installed skills may lag behind.
- Prefer CLI tooling (`dotnet`, `maui`, and `maui devflow`) over manual project-file surgery.

## Code style

- Keep code small and obvious; optimize for a live demo audience.
- Avoid speculative abstractions, base classes, and generic repositories.
- Comment only where the reason isn't obvious.
- Remove template boilerplate that the app doesn't use.
