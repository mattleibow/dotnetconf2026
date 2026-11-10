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

## Refreshing

From the repository root, with the .NET 11 SDK:

```sh
dotnet run data/ImportConference.cs
```

`ImportConference.cs` downloads the website into `data/.cache/` (ignored by Git), builds and validates a new snapshot there, and replaces `conference/` only if everything succeeded. If the site is down, the existing snapshot is untouched.

The site has one AVIF logo, which the importer converts to PNG using macOS `sips`, so refresh on a Mac. The snapshot itself works everywhere.
