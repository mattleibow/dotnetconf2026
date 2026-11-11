# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Users

Attendees at .NET Conf 2026 South Africa (Cape Town, Johannesburg, Pretoria, Durban) browsing the schedule on their phone in a busy conference hallway or on a laptop between talks.

## Product Purpose

An offline companion app for .NET Conf 2026 South Africa that lets attendees switch cities, browse sessions and speakers, save favourites, check venue details and sponsors, and ask an on-device AI assistant questions about the schedule.

## Positioning

A real, offline-first .NET MAUI 11 conference app built and enhanced live on stage with coding agents and on-device AI (`Microsoft.Extensions.AI`, `.NET MAUI Essentials AI`, and `Microsoft.Extensions.DataIngestion`).

## Operating Context

Used live during one-day developer events across four South African cities, as well as by workshop attendees running the numbered prompts in this repository.

## Capabilities and Constraints

- Offline conference data packaged in `data/conference/` (`conference.json`, Markdown abstracts, speaker photos, sponsor logos, venue photos).
- Six primary tabs: Agenda, Speakers, Favourites, Venue (with embedded map), Sponsors, and Assistant.
- City switching updates Agenda, Speakers, Favourites, Venue, and Assistant context.
- Must look great on both a phone in portrait and a wide desktop window (Mac Catalyst).

## Brand Commitments

- Follow `DESIGN.md` as the pinned design system and visual brief (.NET Conf 2026 purple `#512BD4` and pink `#E63888` palette, Open Sans type ramp, light and dark themes).
- Real speaker photos, sponsor logos, and venue photos—never fabricated claims, stock art, or emoji.

## Evidence on Hand

- `data/conference/conference.json` and Markdown content for all four cities, speakers, venues, and sponsors.
- Real speaker avatars (`data/conference/images/speakers/`), sponsor logos (`data/conference/images/sponsors/`), and venue photos (`data/conference/images/venues/`).

## Product Principles

- Fast hallway scanability: times, tracks, rooms, and speaker names are effortless to read at a glance.
- Bold brand in headers and primary accents; quiet, high-contrast surfaces for reading schedules and chat replies.
- Native platform ergonomics across phone and desktop layouts.
