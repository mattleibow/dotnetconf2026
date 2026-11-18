# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Users

Attendees at .NET Conf 2026 South Africa (Durban, Cape Town, Bloemfontein) browsing the schedule on their phone in a busy conference hallway or on a laptop between talks.

## Product Purpose

An offline companion app for .NET Conf 2026 South Africa that lets attendees switch cities, browse sessions and speakers, save favourites, and check venue details and sponsors. The separate native AI demo stack adds an on-device assistant; `main` includes its UI with an explicit unavailable message, not model-backed answers.

## Positioning

A real, offline-first .NET 11 / .NET MAUI 11 conference app built and enhanced live on stage with coding agents. The [native AI stack](README.md#demo-branches) adds `Microsoft.Extensions.AI` typed structured outputs, MAUI Essentials AI, semantic search, and agent workflows. The complete demo guide lives on `main`; the AI implementation does not.

## Operating Context

Used live during one-day developer events across three South African cities, as well as by workshop attendees running the preparation, exploration, and optional bug-hunt prompts in this repository.

## Capabilities and Constraints

- Offline conference data packaged in `data/conference/` (`conference.json`, Markdown abstracts, speaker photos, sponsor logos, venue photos).
- Four primary tabs: **Agenda** (initial tab, literal search, swipe/inline favourites, and All / Favourites filter), **Event** (hero header, sponsors, and venue with embedded map), **Speakers**, and **Assistant** (unavailable on `main`).
- Agenda search on `main` uses case-insensitive substrings across session titles, abstracts, and speaker names within the selected city; semantic retrieval is stack-only.
- City switching lives in the toolbar on each top-level tab and refreshes the city-scoped views. Selected city (`selected_city`) and favourite session IDs (`favorites`) are stored locally with `Preferences`.
- No network is needed for packaged conference content or manual favourites. Map tiles, external maps, and sponsor websites can need connectivity.
- AI-stack chat requires a supported device model; there is no cloud fallback.
- Must look great on both a phone in portrait and a wide desktop window (Mac Catalyst).

## Brand Commitments

- Follow `DESIGN.md` as the pinned design system and visual brief (.NET Conf 2026 purple `#512BD4` and pink `#E63888` palette, Open Sans type ramp, light and dark themes).
- Real speaker photos, sponsor logos, and venue photos—never fabricated claims, stock art, or emoji.

## Evidence on Hand

- `data/conference/conference.json` and Markdown content for all three cities (Durban, Cape Town, Bloemfontein), speakers, venues, and sponsors.
- Real speaker avatars (`data/conference/speakers/`), sponsor logos (`data/conference/sponsors/`), and venue photos (`data/conference/venues/`).

## Product Principles

- Fast hallway scanability: times, tracks, rooms, and speaker names are effortless to read at a glance.
- Bold brand in headers and primary accents; quiet, high-contrast surfaces for reading schedules and chat replies.
- Native platform ergonomics across phone and desktop layouts.
