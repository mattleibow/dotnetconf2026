---
description: Make the wireframe app look like a polished conference app
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.
- Style all four tabs (**Event** with its hero header, sponsors first, then venue + embedded map; **Agenda** with its search bar and All/Favourites filter; **Speakers**; and **Assistant**) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- Keep the city switcher in the top-right toolbar on every tab so the user can always see and switch the active city (`Durban`, `Cape Town`, `Bloemfontein`) without duplicating a second picker inside the page body.
- Use .NET 11 / MAUI 11 features where they fit naturally: `GlobalXmlns.cs` (no `xmlns:` prefixes in XAML), AOT-safe `RelativeSource` ViewModel bindings, `BoxView.Fill` with a `LinearGradientBrush` for the hero banner, and `ScaleToAsync` on the favourite heart.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Keep layouts simple and native, preserve existing `AutomationId`s, and don't change how the app works.

## Live preview and inspection

Keep the **Mobile Device** canvas (`mobile-device`) and the **MAUI DevFlow Inspector** (`browser` canvas at `http://localhost:19223/inspector/<agent-id>/`) open as you redeploy:

1. Deploy and launch the styled app on the booted iOS simulator (and on Mac Catalyst for wide desktop layout checks).
2. Use `maui devflow` to navigate through all four tabs (**Event**, **Agenda**, **Speakers**, **Assistant**), a session detail page, and a speaker detail page in both phone and wide desktop window sizes.
3. Toggle dark and light themes (`maui devflow theme set dark` / `light`) and run `maui devflow ui diagnostics --profile agent` to verify there is no clipped, truncated, or overlapping text.
