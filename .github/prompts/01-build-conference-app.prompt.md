---
description: Build the conference companion app from the bundled demo data
---

Build a beautiful .NET MAUI 11 companion app from the existing `data/conference/` snapshot.

The data has already been imported from the conference website. Start by reading `data/conference/conference.json` and a few of its Markdown files and images to understand the event and its branding. Don't scrape the website, rewrite the importer, or refresh the data during this step. This demo must not depend on the website being available.

Create the MAUI project in its own directory (for example `src/ConferenceApp/`) so it doesn't compile `tools/ImportConference.cs`. Copy the existing data folder into the app's `Resources/Raw/conference/`, keeping its layout:

```text
conference/
  conference.json          # the catalogue: structure and links only
  sessions/{id}.md
  speakers/{id}.md    # images beside Markdown as {id}.png or {id}.jpg
  venues/{id}.md
  sponsors/{id}.md
```

- `conference.json` holds cities/dates, session occurrences (`cityId`, `start`, `end`, `track`, `speakerIds`), speaker details, venues, sponsor scope, and relative `content`/`image` paths. Each session ID identifies a city-specific occurrence, including individual lightning talks; don't merge repeated titles across cities. Times include their UTC offset. The JSON doesn't contain abstracts or bios and also acts as the asset list.
- Each Markdown file has an H1 title/name followed by the published prose, without front matter. `hasAbstract`/`hasBio` indicate whether descriptive text was available; some documents intentionally contain only a heading. Don't invent missing information.
- Resolve each `content`/`image` path relative to `conference/`, not the app-package root. Images are PNG or JPG. Keep the existing IDs and city-specific relationships.

Load this through a small app service: deserialize `conference/conference.json` with `System.Text.Json` (source-generated) and read documents with `FileSystem.OpenAppPackageFileAsync`, using forward-slash paths such as `conference/sessions/{id}.md`. The template's `Resources/Raw/**` `MauiAsset` item already bundles the files; don't add these images as `MauiImage`. Display raw images through stream-backed image sources with a fresh stream per load, and use initials when a speaker image is missing. Detail pages can show Markdown as simple text paragraphs; don't add a rendering library. The app must work offline. It doesn't need ingestion, embeddings, or AI yet; a later step indexes the same session Markdown.

The app should include:

- an agenda of all sessions with their speakers
- for multi-city events, a city selector that shows the relevant sessions, venue, and other city-specific information. Offer to choose the nearest city; explain the benefit, request only foreground location permission when the user chooses this, and always allow manual selection if location is declined or unavailable. Remember the user's choice
- session and speaker details
- favourite sessions, saved on the device
- a sponsors page
- a venue page

Make it feel polished and on-brand for the event, while celebrating .NET 11 and .NET MAUI 11. Keep the code simple and clean; this is a demo app, not a production product.

Use MAUI DevFlow to run, inspect, and interact with the app as you go. Aim for an app that builds, runs, and looks great in screenshots. If the host toolchain or device prevents runtime verification, report that explicitly rather than claiming the UI was verified.
