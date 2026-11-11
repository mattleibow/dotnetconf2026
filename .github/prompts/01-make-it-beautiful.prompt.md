---
description: Make the wireframe app look like a polished conference app
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.
- Style every page (Agenda, Speakers, Favourites, Venue with its embedded map, Sponsors, Assistant, Session detail, and Speaker detail) so they look great on both a phone in portrait and a wide desktop window.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Replace the temporary city button with the city pill chips in the hero header, and add a tiny helper that formats `**bold**`, `*italic*`, `- ` bullets, and `1. ` numbered lines into a `FormattedString` on assistant chat bubbles.
- Keep layouts simple and native, preserve existing `AutomationId`s, and don't change how the app works.

Use DevFlow on Mac Catalyst to check every tab, a session detail page, and a speaker detail page in both phone and wide desktop window sizes.
