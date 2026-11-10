# Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI

> Use AI to build the app, then give the app that same AI.

An almost-empty starter for a live demo: GitHub Copilot builds a polished .NET MAUI 11 conference app, then adds an on-device assistant that can use the app's services and take real actions.

Reused across four cities. Simple code, great design, no finished app checked in.

## Demo data

[`data/`](data/README.md) holds an offline snapshot of the event: a JSON catalogue, Markdown descriptions and images. The app is built from it, so the demo doesn't depend on the website. To refresh it before an event:

```sh
dotnet run data/ImportConference.cs
```

## Copy the prompts

Run these in order, one at a time:

1. [Build the conference app](.github/prompts/01-build-conference-app.prompt.md) - from the offline demo data.
2. [Add semantic search](.github/prompts/02-add-semantic-search.prompt.md) - on-device where available, keyword search elsewhere.
3. [Add local chat](.github/prompts/03-add-local-chat.prompt.md) - a grounded, on-device assistant.
4. [Add assistant actions](.github/prompts/04-add-assistant-actions.prompt.md) - navigation and favourites, with approval.
5. [Route to the cloud](.github/prompts/05-add-cloud-routing.prompt.md) - optional: send complex questions to a cloud model.

The [Copilot instructions](.github/copilot-instructions.md) provide shared .NET 11, MAUI 11, design, and DevFlow guidance.

## Before starting

Have a compatible .NET 11 SDK, MAUI workload, platform tooling, and simulator or device ready; check the workload's required Xcode version on Apple. Install the [MAUI Labs CLI](https://github.com/dotnet/maui-labs) (`dotnet tool install -g Microsoft.Maui.Cli --prerelease`) and run `maui doctor`. Enable the relevant [.NET agent skills](https://github.com/dotnet/skills) and MAUI DevFlow skills in your coding agent.

The AI prompts use experimental [MAUI Labs local clients](https://github.com/dotnet/maui-labs/tree/main/src/AI/Microsoft.Maui.Essentials.AI) and [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions). Local models require supported hardware and model availability; a simulator alone may not be enough.
