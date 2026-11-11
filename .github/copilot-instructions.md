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
- Build one target framework at a time (`dotnet build -f net11.0-maccatalyst`, then run). Parallel builds of different target frameworks share `obj/` and corrupt each other. Build before `-t:Run`; `-t:Run` on its own may launch a stale or missing app.
- If a simulator screenshot shows the wrong device or the home screen, use `xcrun simctl io <UDID> screenshot <file>` for the simulator running your app.
- When driving the app with the `maui devflow` CLI, prefer `batch` for several steps and pass the agent port explicitly; separate concurrent CLI calls can conflict. The broker may list agents from other apps, so pick yours by app name and process.
- After a meaningful change, build and run the app, then use MAUI DevFlow to inspect the visual tree, interact with the UI, and take screenshots. Check both a phone in portrait and a wide desktop window (Mac Catalyst at about 1200 px wide). Fix what you see; don't stop at "it compiles."
- **Design review.** When a screen is done, take phone and desktop screenshots and ask a second reviewer to critique them: a rubber-duck or code-review sub-agent, ideally on a stronger model than the one building. Give it the screenshots, the style guide above, and what the screen is for. Ask for the top issues in functionality, layout, visual polish and accessibility, then fix the ones that matter. One review round per screen is enough.
- Apple's on-device model is small: keep structured-output types flat with no nullable properties (use an empty string instead), and keep tool sets small with clearly distinct names.
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
