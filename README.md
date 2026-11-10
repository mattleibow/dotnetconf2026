# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

An almost-empty starter for a live demo: GitHub Copilot builds a polished .NET MAUI 11 conference app, then adds an on-device assistant that can use the app's services and take real actions.

Reused across four cities. Simple code, great design, no finished app checked in.

## Demo data

The included [`data/conference/`](data/conference/) snapshot works without the website: a JSON catalogue, Markdown descriptions, and local images. Prompt 01 copies it into the app's `Resources/Raw/conference/` as `MauiAsset` files. Nothing needs scraping during the demo.

To refresh before an event, run the .NET 11 / C# 15 file-based app from the repository root:

```sh
dotnet run tools/ImportConference.cs
```

Each run clears ignored `data/.cache/`, downloads the [event website](https://cptmsdug.dev/dotnet-conf-2026.html) (including embedded abstracts and bios), and builds the replacement there. Only after downloading and validating all content does it replace `data/conference/`. A failed refresh leaves the existing demo data intact. No flags or configuration are needed; the included snapshot doesn't need the website, cache, or importer during the demo.

Refresh currently uses macOS `sips` to convert the site's AVIF sponsor logo to PNG; the resulting data is platform-independent. The importer targets this site's markup, not arbitrary event websites.

## Copy the prompts

Run these in order, one at a time:

1. [Build the conference app](.github/prompts/01-build-conference-app.prompt.md) - start from the included offline demo data, not the live website.
2. [Add semantic search](.github/prompts/02-add-semantic-search.prompt.md) - on-device where available, keyword search elsewhere.
3. [Add local chat](.github/prompts/03-add-local-chat.prompt.md) - a grounded, on-device assistant.
4. [Add assistant actions](.github/prompts/04-add-assistant-actions.prompt.md) - navigation and favourites, with approval.
5. [Route to the cloud](.github/prompts/05-add-cloud-routing.prompt.md) - optional: send complex questions to a cloud model.

The [Copilot instructions](.github/copilot-instructions.md) provide shared .NET 11, MAUI 11, design, and DevFlow guidance.

## Before starting

Have a compatible .NET 11 SDK, MAUI workload, platform tooling, and simulator or device ready; check the workload's required Xcode version on Apple. Install the [MAUI Labs CLI](https://github.com/dotnet/maui-labs) (`dotnet tool install -g Microsoft.Maui.Cli --prerelease`) and run `maui doctor`. Enable the relevant [.NET agent skills](https://github.com/dotnet/skills) and MAUI DevFlow skills in your coding agent.

The AI prompts use experimental [MAUI Labs local clients](https://github.com/dotnet/maui-labs/tree/main/src/AI/Microsoft.Maui.Essentials.AI) and [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions). Local models require supported hardware and model availability; a simulator alone may not be enough.
