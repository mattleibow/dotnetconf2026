# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

The companion app and speaker guide for .NET Conf 2026 South Africa.

## What runs on `main`

`main` is a **.NET 11 / .NET MAUI 11** conference app:

- **Agenda:** city-scoped, case-insensitive substring search over titles, abstracts, and speaker names; All / Favourites filter; swipeable session cards and inline favourite controls.
- **Event:** conference details, sponsors, venue photos, and an embedded map.
- **Speakers:** profiles, biographies, and links to their sessions.
- **Assistant:** chat UI and starter suggestions, ready for the AI walkthrough.
- **Local state:** selected city and favourites in `Preferences`. Conference content for **Durban, Cape Town, and Bloemfontein** ships with the app.

The schedule, biographies, images, search, and favourites work offline. Map tiles, external maps, and sponsor websites can require network access. The layouts support portrait phones and wide desktop windows, with light and dark themes. Debug builds register the MAUI DevFlow agent.

## Repository map

| Path | Contents |
|---|---|
| [`ConferenceApp.slnx`](ConferenceApp.slnx), [`src/ConferenceApp/`](src/ConferenceApp/) | MAUI app, XAML views, view models, and platform projects |
| [`Data/`](src/ConferenceApp/Data/) | `ConferenceDbProvider`, read-only `ConferenceDb`, preload service, and conference models |
| [`Services/`](src/ConferenceApp/Services/) | Literal search, user preferences, chat availability stub, and background-service base |
| [`.github/prompts/`](.github/prompts/) | `00-prepare-demo`, `01-explore-app`, and `02-bug-hunt` |
| [`docs/`](docs/README.md) | .NET 11 notes, DevFlow runbook, and the separate AI-stack walkthrough |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Agent guidance for the whole demo, including stack-only AI conventions |
| [`DESIGN.md`](DESIGN.md), [`PRODUCT.md`](PRODUCT.md) | Design system and product brief |
| [`data/`](data/README.md) | Packaged JSON catalogue, Markdown descriptions, and real conference images |

## Demo branches

The optional [bug-hunt PR #33](https://github.com/mattleibow/dotnetconf2026/pull/33) uses **`mattleibow-demo-bugs-for-devflow-bug-hunt`**. Prepare that branch in an isolated worktree/session and ensure it is rebased onto the latest fetched `main` before the demo. Its seed adds the speaker, favourites, and map regressions; the input-border artifact is also present on `main`. Run [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md) on that baseline, not on `main`; see [the bug-hunt preflight](docs/02-agents-building-apps.md#step-02-optional-bug-hunt).

The native on-device AI stack is ordered **bottom to top**:

| Layer | Pull request | Adds |
|---|---|---|
| 1 | [#34](https://github.com/mattleibow/dotnetconf2026/pull/34) | Platform chat clients and availability checks |
| 2 | [#35](https://github.com/mattleibow/dotnetconf2026/pull/35) | ONNX embeddings, vector indexing, and semantic search |
| 2b | [#42](https://github.com/mattleibow/dotnetconf2026/pull/42) | Source-generated tools |
| 3 | [#39](https://github.com/mattleibow/dotnetconf2026/pull/39) | Typed background session and speaker enrichment |
| 4 | [#36](https://github.com/mattleibow/dotnetconf2026/pull/36) | Assistant context and agent integration |
| 5 | [#37](https://github.com/mattleibow/dotnetconf2026/pull/37) | Multi-agent workflows and routing |
| 6 | [#41](https://github.com/mattleibow/dotnetconf2026/pull/41) | Experimental Windows tool calling |

Use the corresponding branch for each walkthrough, or the [top-of-stack source](https://github.com/mattleibow/dotnetconf2026/tree/mattleibow-ai-6-windows-tool-calling) for the combined demo.

## Check tooling, then run one target

From the repository root, inspect the installed toolchain before changing anything:

```sh
dotnet --info
dotnet --list-sdks
dotnet workload list
maui doctor
```

Use a .NET 11 SDK, compatible MAUI 11 workloads, and the platform tools reported by the workload release notes. The MAUI CLI (`maui`) and [MAUI Labs skills](https://github.com/dotnet/maui-labs) support the live agent demo.

For the iOS demo, reuse the booted simulator (`xcrun simctl list devices booted`). **.NET 11 `dotnet run` handles device selection, building, installation, and launch**:

```sh
cd src/ConferenceApp
dotnet run --framework net11.0-ios --list-devices
dotnet run --framework net11.0-ios --device <booted-simulator-UDID> --no-launch-profile
```

Run from the project directory so project-relative launch paths resolve correctly with this repo's `UseArtifactsOutput`. `--no-launch-profile` bypasses the Windows-only profile. Let the first run restore, keep its process alive, and wait for the app with `maui devflow wait --wait-platform iOS`. Do not hardcode a `.app` path or manually install/launch with `simctl` for the normal .NET 11 workflow. See [Apple build targets](https://learn.microsoft.com/dotnet/ios/building-apps/build-targets) for device selection.

Build **one target framework at a time** when build-only validation is wanted. These are separate alternatives from the repository root, not extra steps in the iOS demo:

```sh
# macOS desktop, or Android on a configured host
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-maccatalyst
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-android
```

Windows has a conditional packaged target:

```powershell
dotnet build .\src\ConferenceApp\ConferenceApp.csproj -f net11.0-windows10.0.19041.0
```

See [Part 1](docs/01-whats-new-net11-maui11.md) for framework features and [the runbook](docs/README.md) for device preparation.

## Live demo

1. [`00-prepare-demo.prompt.md`](.github/prompts/00-prepare-demo.prompt.md): use .NET 11 `dotnet run` on the booted iOS simulator and open the available device/Inspector canvases. **Clears saved preferences and resets live Agenda search/filter state.**
2. [`01-explore-app.prompt.md`](.github/prompts/01-explore-app.prompt.md): find the MAUI talk, favourite it, inspect storage, and open the speaker profile.
3. On `mattleibow-demo-bugs-for-devflow-bug-hunt`, [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md): diagnose, fix, and verify the blank speaker, empty favourites filter, invisible map, and input-border artifacts.
4. On the native AI stack, follow [Part 3](docs/03-giving-the-app-ai.md) for local chat, semantic search, typed enrichment, tools, and workflows.

```sh
maui devflow list
maui devflow logs --follow --no-json
```

On `main`, search is literal and chat is unavailable. Queries such as `football forecasts` demonstrate semantic retrieval **only on the AI stack**, after indexing.

## On-device AI

Local chat needs Apple Intelligence (iOS / Mac Catalyst 26+), Gemini Nano (Android), or a packaged launch on a Windows Copilot+ PC.

## Conference data

The snapshot is already included; building and browsing do not scrape the website or download models. To deliberately refresh the conference snapshot, see [`data/README.md`](data/README.md) and run the importer from the repository root:

```sh
dotnet run data/ImportConference.cs
```

The optional model preparation scripts are grouped under [`models/`](models/README.md).
The app on `main` does not use them; the AI stack does.
