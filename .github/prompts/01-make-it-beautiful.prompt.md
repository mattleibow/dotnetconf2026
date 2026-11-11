---
description: Make the wireframe app look like a polished conference app
---

Load the `impeccable` skill and make the wireframe app in `src/ConferenceApp/` look like a polished, shipping conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.
- Style all four tabs (**Event** with its hero header, sponsors first, then venue + embedded map; **Agenda** with its All/Favourites filter; **Speakers**; and **Assistant**) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- Keep the city switcher in the top-right toolbar on every tab so the user can always see and switch the active city (`Durban`, `Cape Town`, `Bloemfontein`) without duplicating a second picker inside the page body.
- Use the real speaker photos, sponsor logos, and venue photo from the packaged `conference/` assets.
- Keep layouts simple and native, preserve existing `AutomationId`s, and don't change how the app works.

Deploy and run the app on the booted iOS simulator (and Mac Catalyst for wide desktop checks), open the **Mobile Device** canvas (`mobile-device`) selected on that simulator, and open the **MAUI DevFlow Inspector** (`http://localhost:19223/inspector/<agent-id>/`) in the **Browser** canvas (`browser`) while you use DevFlow to check every tab, a session detail page, and a speaker detail page in both phone and wide desktop window sizes.
