# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

The companion app and speaker guide for .NET Conf 2026 South Africa.

## What runs on `main`

`main` is a **.NET 11 / .NET MAUI 11** conference app:

- **Agenda:** city-scoped semantic search with packaged ONNX embeddings; All / Favourites filter; swipeable session cards and inline favourite controls.
- **Event:** conference details, sponsors, venue photos, and an embedded map.
- **Speakers:** profiles, biographies, and links to their sessions.
- **Assistant:** conference tools, contextual agents, and multi-agent workflows on the native on-device model. Launched through the Aspire AppHost, chat tries the AI gateway first and falls back to on-device AI.
- **Local state:** selected city and favourites in `Preferences`. Conference content for **Durban, Cape Town, and Bloemfontein** ships with the app.

The schedule, biographies, images, search, and favourites work offline. Map tiles, external maps, and sponsor websites can require network access. The layouts support portrait phones and wide desktop windows, with light and dark themes. Debug builds register the MAUI DevFlow agent.

## Repository map

| Path | Contents |
|---|---|
| [`ConferenceApp.slnx`](ConferenceApp.slnx), [`src/ConferenceApp/`](src/ConferenceApp/) | MAUI app, XAML views, view models, and platform projects |
| [`Data/`](src/ConferenceApp/Data/) | `ConferenceDbProvider`, read-only `ConferenceDb`, preload service, and conference models |
| [`Services/`](src/ConferenceApp/Services/) | Semantic search, user preferences, streamed chat, and background-service base |
| [`src/ConferenceApp.AppHost/`](src/ConferenceApp.AppHost/) | Aspire orchestration: LiteLLM gateway, Azure OpenAI, Ollama, and the MAUI app |
| [`.github/prompts/`](.github/prompts/) | `00-prepare-demo`, `01-explore-app`, and `02-bug-hunt` |
| [`docs/`](docs/README.md) | .NET 11 notes, DevFlow runbook, AI walkthrough, and gateway runbook |
| [`.github/copilot-instructions.md`](.github/copilot-instructions.md) | Agent guidance for the whole demo, including AI conventions |
| [`DESIGN.md`](DESIGN.md), [`PRODUCT.md`](PRODUCT.md) | Design system and product brief |
| [`data/`](data/README.md) | Packaged JSON catalogue, Markdown descriptions, and real conference images |

## Demo branches

The optional [bug-hunt PR #33](https://github.com/mattleibow/dotnetconf2026/pull/33) uses **`mattleibow-demo-bugs-for-devflow-bug-hunt`**. Prepare that branch in an isolated worktree/session and ensure it is rebased onto the latest fetched `main` before the demo. Its seed adds the speaker, favourites, and map regressions; the input-border artifact is also present on `main`. Run [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md) on that baseline, not on `main`; see [the bug-hunt preflight](docs/02-agents-building-apps.md#step-02-optional-bug-hunt).

The AI walkthrough was built as a stack of PRs, ordered **bottom to top**:

| Layer | Pull request | Adds |
|---|---|---|
| 1 | [#34](https://github.com/mattleibow/dotnetconf2026/pull/34) | Platform chat clients and availability checks |
| 2 | [#35](https://github.com/mattleibow/dotnetconf2026/pull/35) | ONNX embeddings, vector indexing, and semantic search |
| 2b | [#42](https://github.com/mattleibow/dotnetconf2026/pull/42) | Source-generated tools |
| 3 | [#39](https://github.com/mattleibow/dotnetconf2026/pull/39) | Typed background session and speaker enrichment |
| 4 | [#36](https://github.com/mattleibow/dotnetconf2026/pull/36) | Assistant context and agent integration |
| 5 | [#37](https://github.com/mattleibow/dotnetconf2026/pull/37) | Multi-agent workflows and routing |
| 6 | [#41](https://github.com/mattleibow/dotnetconf2026/pull/41) | Experimental Windows tool calling |

These layers are now merged. Use their PRs as a historical walkthrough, or walk through the combined code directly.

## Gateway demo

Run `aspire start` from the repository root and follow the [gateway runbook](docs/04-ai-gateway.md) for first-run setup. Aspire runs a LiteLLM gateway in front of Azure OpenAI and a local Ollama server, plus the app on Mac, iOS and Android. The app's default `IChatClient` tries the gateway first and falls back to native on-device AI on any failure; background enrichment stays on-device.

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
4. Follow [Part 3](docs/03-giving-the-app-ai.md) for local chat, semantic search, typed enrichment, tools, and workflows.

```sh
maui devflow list
maui devflow logs --follow --no-json
```

Queries such as `football forecasts` demonstrate semantic retrieval after the packaged catalogue has been indexed.

## On-device AI

Local chat needs Apple Intelligence (iOS / Mac Catalyst 26+), Gemini Nano (Android), or a packaged launch on a Windows Copilot+ PC. Without the Aspire AppHost, `ai-gateway` doesn't resolve, so chat falls back to on-device AI.

## Conference data

The snapshot is already included; building and browsing do not scrape the website or download models. To deliberately refresh the conference snapshot, see [`data/README.md`](data/README.md) and run the importer from the repository root:

```sh
dotnet run data/ImportConference.cs
```

The optional model preparation scripts are grouped under [`models/`](models/README.md).
The embedding model is required to build the app; the chat model is used on Windows.
