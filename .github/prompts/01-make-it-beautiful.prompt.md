---
description: Make the wireframe app look like a polished conference app
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.
- Style all four tabs (**Event** with its hero header, venue + embedded map, and sponsors; **Agenda** with its All/Favourites filter; **Speakers**; and **Assistant**) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- Keep the city switcher in the top-right toolbar on every tab so the user can always see and switch the active city (`Durban`, `Cape Town`, `Bloemfontein`) without duplicating a second picker inside the page body.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Keep layouts simple and native, preserve existing `AutomationId`s, and don't change how the app works.

Use DevFlow on Mac Catalyst to check every tab, a session detail page, and a speaker detail page in both phone and wide desktop window sizes.
