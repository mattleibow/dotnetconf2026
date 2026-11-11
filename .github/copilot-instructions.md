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

The app should look like a professional, shipping conference app from the first build.

### Style guide

The .NET Conf 2026 brand, from the event website:

| Token | Colour | Use |
|---|---|---|
| Primary | `#512BD4` | .NET purple: headers, primary buttons, Track 1 |
| Secondary | `#6C3DD8` | Gradients, Track 2 |
| Light | `#8454E4` | Highlights on dark backgrounds |
| Dark | `#3B1F99` | Pressed states, dark headers |
| Accent | `#E63888` | Pink: favourites, Track 3, calls to action |
| Dark background | `#1A1A2E` | Dark mode page background |
| Light background | `#F8F9FA` | Light mode page background |

- Signature gradient: 135°, primary purple to pink accent.
- Cards: white (or a dark surface in dark mode), 16 px corners, a soft purple shadow.
- Spacing on a 4 px grid (4, 8, 12, 16, 24, 32).
- Use the template's Open Sans fonts; bold for titles, regular for body, a small caps-style label for metadata such as times and tracks.

### Rules

- **Theme first.** Put the style guide colours in `Resources/Styles/Colors.xaml`, and a small type ramp (display, title, subtitle, body, caption), spacing and card styles in `Styles.xaml`. Pages use these resources, never raw colours or font sizes. Support light and dark mode with `AppThemeBinding` in the resources.
- **Real content.** Show speaker photos (circular, with initials as the fallback), sponsor logos on white tiles, and the venue photo. Use the event's real name and dates; don't invent slogans.
- **Agenda.** Group sessions by time slot, show the track or room as a coloured chip, speaker photos and names, and a favourite (heart) toggle on each card. Breaks, registration and other general items are compact one-line rows with no favourite toggle and no empty sections.
- **Details.** A header with the brand gradient, then time and room chips, speakers with photos, the abstract, and a clear favourite button.
- **City picker.** Three city chips (or a segmented control) in the header, not a dropdown.
- **Navigation.** Tabs for Agenda, Speakers, Favourites and Venue (with sponsors). Pages with their own branded header hide the Shell navigation bar, so the title isn't shown twice.
- **Adaptive layout.** It must look right on a phone in portrait and in a large desktop window:
  - Phones: bottom tabs, single-column lists, full-width cards.
  - Wide windows (about 800 px and up): a sidebar if it's simple in Shell (tabs are fine otherwise), multi-column card grids, content limited to about 1,100 px wide and centred, and two-column detail pages.
  - Switch with `AdaptiveTrigger` visual states or the window width, in one place, not per control.
- Use `CollectionView` for lists, with useful empty, loading, and error states.
- Keep accessibility in mind: semantic descriptions, readable contrast, and platform-appropriate touch targets (at least 44 points on Apple and 48 dp on Android).

## Agent workflow

- Add `Microsoft.Maui.DevFlow.Agent` with the version that matches the installed `maui` CLI (check `maui --version`); `--prerelease` can pick an older, incompatible build. Use its `Microsoft.Maui.DevFlow.Agent` namespace and register it in Debug builds only:

  ```csharp
  #if DEBUG
  builder.AddMauiDevFlowAgent();
  #endif
  ```

- Give interactive and important elements a stable, kebab-case `AutomationId`, such as `session-list`, `favorite-button`, and `sponsors-tab`.
- Make list items tappable with a tap gesture or command on the item, and give each item a stable `AutomationId`; `CollectionView` selection is awkward to drive from automation.
- Work in small loops on Mac Catalyst: change, build, run, check. Build iOS and Android once at the end. Don't re-check the SDK, workloads or Xcode unless a build fails.
- Build one target framework at a time; parallel builds share `obj/` and corrupt each other. A fresh worktree has no `obj/`, so don't use `--no-restore` on its first build.
- Run the Mac app with `dotnet build -f net11.0-maccatalyst -t:Run` after a normal build (`-t:Run` alone may launch a stale app). For iOS, install the `.app` from `bin/Debug/net11.0-ios/iossimulator-arm64/` on an already booted simulator with `xcrun simctl install` and `launch`.
- Drive the app with `maui devflow` without reading `--help` first. Find your app and its agent port with `maui devflow list` (match the app name and process; other apps may be listed), then pipe steps to `batch`:

  ```bash
  printf '%s\n' \
    'MAUI navigate //agenda' \
    'MAUI fill --automationId agenda-search maui' \
    'MAUI tap --automationId session-card-1' \
    'MAUI query --automationId session-title --format compact' \
    'MAUI tree --depth 3' \
    'MAUI screenshot --output files/agenda.png' \
    | maui devflow batch --agent-port <port> --delay 500
  ```

  `maui devflow ui resize <width> <height>` sets the desktop window size, and `maui devflow logs --agent-port <port> --limit 50` shows app logs. Keep `query` and `tree` output small with `--format compact` and `--depth`. If a simulator screenshot shows the wrong device, use `xcrun simctl io <UDID> screenshot <file>`.
- Check what you changed, not everything: run the steps in the prompt, take a phone and a desktop screenshot of the changed screens, and look at them yourself. Fix anything broken, clipped or off-brand. A second reviewer isn't needed unless I ask for one.
- Apple's on-device model is small: keep structured-output types flat with no nullable properties (use an empty string instead), and keep tool sets small with clearly distinct names.
- Reuse a booted simulator instead of creating new ones. When you're done, quit the apps you launched and shut down any simulator you booted, so the machine isn't left full of running copies.
- Don't change shared toolchain settings or install workloads without approval. If device or model execution is blocked, report the blocker and what remains unverified; don't claim success or retry indefinitely.
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
