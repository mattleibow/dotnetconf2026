---
description: Make the wireframe app look like a polished conference app and add inline favourite hearts on Agenda cards
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml` (never set `LineHeight` on `Label` styles so multi-line text never clips on Mac Catalyst/iOS).
- Style all four tabs (**Event** with its hero header, sponsors first, then venue + embedded map; **Agenda** with its search bar, All/Favourites filter, AI topic chips, and AI summaries; **Speakers**; and **Assistant** with its Plan my day button, suggestion chips, message bubbles, live progress status, session cards, and approval cards) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- **Add an inline favourite toggle on Agenda talk cards:** Add a `ToggleFavoriteCommand` on `AgendaViewModel` and a 44×44pt heart button (`AutomationId="session-favorite-<id>"`) on each talk card (`Session.IsGeneral == false`) that toggles the favourite in `UserPreferencesService`, animates with `ScaleToAsync`, and updates `IsFavorite` without opening the detail page.
- Keep the city switcher in the top-right toolbar on every tab so the user can always see and switch the active city (`Durban`, `Cape Town`, `Bloemfontein`) without duplicating a second picker inside the page body.
- Use .NET 11 / MAUI 11 features where they fit naturally: `GlobalXmlns.cs` (no `xmlns:` prefixes in XAML), AOT-safe `RelativeSource` ViewModel bindings, `BoxView.Fill` with a `LinearGradientBrush` for the hero banner, and `ScaleToAsync` on the favourite heart.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Keep layouts simple and native and preserve all existing `AutomationId`s.

## Live preview and inspection

Keep the **Mobile Device** canvas (`mobile-device`) and the **MAUI DevFlow Inspector** (`browser` canvas at `http://localhost:19223/inspector/<agent-id>/`) open as you redeploy:

1. Deploy and launch the styled app on the booted iOS simulator (and on Mac Catalyst for wide desktop layout checks).
2. Use `maui devflow` to test the new inline favourite heart button (`session-favorite-<id>`) on Agenda, verify `maui devflow storage preferences get favorites` updates immediately, and filter down to **Favourites** (`favorites-filter`).
3. Navigate through all four tabs (**Event**, **Agenda**, **Speakers**, **Assistant**), a session detail page, and a speaker detail page in both phone and wide desktop window sizes.
4. Toggle dark and light themes (`maui devflow theme set dark` / `light`) and run `maui devflow ui diagnostics --profile agent` to verify there is no clipped, truncated, or overlapping text.
