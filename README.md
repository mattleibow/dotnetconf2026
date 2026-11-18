# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

The companion app and speaker guide for .NET Conf 2026 South Africa.

## What runs on `main`

`main` is a complete, styled **.NET 11 / .NET MAUI 11** conference app, not the in-app AI implementation:

- **Agenda:** city-scoped, case-insensitive substring search over titles, abstracts, and speaker names; All / Favourites filter; swipeable session cards and inline favourite controls.
- **Event:** conference details, sponsors, venue photos, and an embedded map.
- **Speakers:** profiles, biographies, and links to their sessions.
- **Assistant:** the chat UI and starter suggestions, with a deterministic “On-device AI isn't connected yet” availability message. Inputs are disabled; this is not a working model or a rule-based conference assistant.
- **Local state:** selected city and favourites in `Preferences`. Conference content for **Durban, Cape Town, and Bloemfontein** ships with the app.

The schedule, biographies, images, search, and favourites work offline. Map tiles, external maps, and sponsor websites can require network access. The layouts support portrait phones and wide desktop windows, with light and dark themes. Debug builds register the MAUI DevFlow agent.

The full demo guide intentionally lives on `main`, including [Part 3's AI walkthrough](docs/03-giving-the-app-ai.md). Its AI source links point to the separate stack, not nonexistent files on `main`.

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

The optional [bug-hunt PR #33](https://github.com/mattleibow/dotnetconf2026/pull/33) introduces the four issues in [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md). They are not intentional defects on `main`.

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

Use the corresponding branch for each walkthrough, or the [top-of-stack source](https://github.com/mattleibow/dotnetconf2026/tree/mattleibow-ai-6-windows-tool-calling) for the combined demo. Stack files such as `AI/`, `models/`, `packages/`, and `NuGet.config` are **not prerequisites for `main`**.

## Check tooling, then build one target

From the repository root, inspect the installed toolchain before changing anything:

```sh
dotnet --info
dotnet --list-sdks
dotnet workload list
maui doctor
```

Use a .NET 11 SDK, compatible MAUI 11 workloads, and the platform tools reported by the workload release notes. Do not assume an installed Xcode or Android SDK matches the selected workload. The MAUI CLI (`maui`) and [MAUI Labs skills](https://github.com/dotnet/maui-labs) support the live agent demo; a coding agent is not needed to build the app. No SDK/workload installation commands are required by this guide.

Build the project for **one target framework at a time**, on a host configured for it. Let the first build restore:

```sh
# macOS: convenient desktop demo target
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-maccatalyst

# Android: use separately, with the Android toolchain configured
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-android
```

For iOS, use `-f net11.0-ios` with a configured Mac/Xcode and the intended device or simulator runtime identifier. The [prepare prompt](.github/prompts/00-prepare-demo.prompt.md) targets the already-booted simulator. Windows has a conditional packaged target:

```powershell
dotnet build .\src\ConferenceApp\ConferenceApp.csproj -f net11.0-windows10.0.19041.0
```

If using an already-provisioned isolated SDK on Windows, select it only for the current PowerShell process (replace the placeholder with your SDK directory):

```powershell
$env:DOTNET_ROOT = 'C:\path\to\isolated\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --info
```

Do not change machine-wide settings or install workloads as part of a demo check. See [Part 1](docs/01-whats-new-net11-maui11.md) for the framework features and [the runbook](docs/README.md) for device preparation.

## Live demo

1. [`00-prepare-demo.prompt.md`](.github/prompts/00-prepare-demo.prompt.md): build/launch the iOS simulator app and open the device and DevFlow Inspector canvases. **Clears saved preferences.**
2. [`01-explore-app.prompt.md`](.github/prompts/01-explore-app.prompt.md): find the MAUI talk, favourite it, inspect storage, and open the speaker profile.
3. On the optional bug branch, [`02-bug-hunt.prompt.md`](.github/prompts/02-bug-hunt.prompt.md): diagnose the blank speaker, empty favourites filter, invisible map, and input-border artifacts.
4. On the native AI stack, follow [Part 3](docs/03-giving-the-app-ai.md) for local chat, semantic search, typed enrichment, tools, and workflows.

```sh
maui devflow list
maui devflow logs --follow --no-json
```

On `main`, search is literal and chat is unavailable. Queries such as `football forecasts` demonstrate semantic retrieval **only on the AI stack**, after indexing.

## AI-stack requirements and Windows caveats

The stack uses `Microsoft.Extensions.AI`, MAUI Essentials AI, bundled ONNX embeddings, and Agent Framework workflows. Typed session and speaker enrichment replaces the older ingestion-enricher approach: `SessionEnrichmentService` and `SpeakerEnrichmentService` run independently, each with its own nested output model. [Part 3](docs/03-giving-the-app-ai.md) covers the revision and its timeout behavior.

Local chat needs a supported, available device model: Apple Intelligence on supported iOS / Mac Catalyst 26+ devices, Gemini Nano on supported Android hardware, or Windows AI on a compatible Copilot+ PC. An emulator capable of running ONNX search does not necessarily support chat. No cloud fallback or automatic model download is configured.

**Windows layer 6:** use the packaged launch profile on Windows 11 build 26100+, with MSIX identity, `systemAIModels`, a compatible Windows App SDK runtime, and any required Phi Silica access approval. The [experimental package provenance and reproduction guide](https://github.com/mattleibow/dotnetconf2026/blob/mattleibow-ai-6-windows-tool-calling/packages/README.md) records the source commit, license, and checksum. Tool calling is unreliable and can be slow; manual navigation and favourites remain the reliable route. An unavailable model shows the availability message instead of invoking the agent. `-p:WindowsPackageType=None` is only for checking the stack's unpackaged unavailable path.

## Conference data

The snapshot is already included; building and browsing do not scrape the website or download models. To deliberately refresh the conference snapshot, see [`data/README.md`](data/README.md) and run the importer from the repository root:

```sh
dotnet run data/ImportConference.cs
```

The AI stack separately contains `models/DownloadModels.cs`; it is not present or needed on `main`.
