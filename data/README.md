# Conference data

An offline snapshot of [.NET Conf 2026 Community Edition South Africa](https://cptmsdug.dev/dotnet-conf-2026.html), held in Durban, Cape Town and Bloemfontein. The app is built from this folder, so the demo doesn't need the website.

```text
conference/
  conference.json      # structure and links: cities, agenda, speakers, venues, sponsors
  sessions/{id}.md
  speakers/{id}.md     # plus {id}.png or {id}.jpg
  venues/{id}.md       # plus image
  sponsors/{id}.md     # plus logo
```

## conference.json

- `cities`: ID, name, date, venue and registration link.
- `sessions`: one entry per scheduled session **per city**, so a talk given in two cities has two IDs (such as `durban-…` and `cape-town-…`). Each has `start`/`end` (ISO 8601 with a `+02:00` offset), `track`, `speakerIds`, and `kind` (`session` for talks, `general` for registration, lunch and so on). Each talk in a lightning round is its own session.
- `speakers`: name, tagline, social links, and the cities they speak in.
- `venues`: one per city, with address and map link.
- `sponsors`: tier and the cities they sponsor.

Every entity has a `content` path to its Markdown, and most have an `image` path. Paths are relative to `conference/`.

## Markdown

Each file is an `# H1` with the title or name, then the published abstract, bio or description. There's no front matter; the JSON holds the structured data.

Some files are only a heading, because the website has no text for them (for example, lunch). `hasAbstract` and `hasBio` in the JSON say which ones have text.

## Brand

From the event website's stylesheet:

| Token | Colour | Use |
|---|---|---|
| Primary | `#512BD4` | .NET purple: headers, primary buttons, Track 1 |
| Secondary | `#6C3DD8` | Gradients, Track 2 |
| Light | `#8454E4` | Highlights on dark backgrounds |
| Dark | `#3B1F99` | Pressed states, dark headers |
| Accent | `#E63888` | Pink accent: favourites, Track 3, calls to action |
| Dark background | `#1A1A2E` | Dark mode surfaces |
| Light background | `#F8F9FA` | Light mode page background |

The signature look is a 135° gradient from primary purple to pink, white cards with 15px corners, and soft purple shadows. City icons are in the website, not in this snapshot.

## Refreshing

From the repository root, with the .NET 11 SDK:

```sh
dotnet run data/ImportConference.cs
```

It deletes and rewrites `conference/`. Review the changes with `git diff`; if a run fails or the site is down, `git restore data/conference` brings the previous snapshot back.

The site has one AVIF logo, which the importer converts to PNG using macOS `sips`, so refresh on a Mac. The snapshot itself works everywhere.
