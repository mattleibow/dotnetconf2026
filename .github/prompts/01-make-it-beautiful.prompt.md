---
description: Make the wireframe app look like a polished conference app
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.
- Style all four tabs (**Event** with its hero header, sponsors first, then venue + embedded map; **Agenda** with its All/Favourites filter; **Speakers**; and **Assistant**) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- Keep the city switcher in the top-right toolbar on every tab so the user can always see and switch the active city (`Durban`, `Cape Town`, `Bloemfontein`) without duplicating a second picker inside the page body.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Keep layouts simple and native, preserve existing `AutomationId`s, and don't change how the app works.

## Live preview and inspection

Always run and show the app in the side-panel canvases so we can watch it live:

1. Deploy and launch the app on the booted iOS simulator (and on Mac Catalyst for wide desktop layout checks).
2. Open the **Mobile Device** canvas (`open_canvas` with `canvasId: "mobile-device"`) and select the booted iOS simulator (`select_device`) so the live simulator stream is visible in the panel.
3. Find the running app's DevFlow inspector URL from `http://localhost:19223/inspector/` (or `maui devflow list`) and open `http://localhost:19223/inspector/<agent-id>/` in a **Browser** canvas (`open_canvas` with `canvasId: "browser"`, `title: "DevFlow Inspector"`).
4. Use `maui devflow` to navigate through all four tabs (**Event**, **Agenda**, **Speakers**, **Assistant**), a session detail page, and a speaker detail page in both phone and wide desktop window sizes, checking `ui diagnostics` and screenshots as you go.
