---
description: Restyle the wireframe app into a stunning Festival Pass conference app following DESIGN.md and PRODUCT.md
---

Load the `impeccable` skill and restyle the wireframe app in `src/ConferenceApp/` into a bold, dynamic conference app following `DESIGN.md` and `PRODUCT.md`.

- Start with the theme tokens, gradients, and card elevation in `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml` (never set `LineHeight` on `Label` styles so multi-line text never clips on Mac Catalyst/iOS).
- Style all four tabs (**Event** with its Festival Pass hero header, multi-column sponsor grid, and venue showcase + map; **Agenda** with ticket-stub talk cards featuring a left gradient rail, time column, track/topic pills, and speaker avatars vs compact break rows; **Speakers** with a responsive multi-column grid and gradient-ringed portraits; and **Assistant** with its AI Concierge empty state, message bubbles, live progress status, and session cards) plus the **Session** and **Speaker** detail pages so they look great on both a phone in portrait and a wide desktop window.
- Keep the city switcher in the top-right toolbar on every tab (`Durban`, `Cape Town`, `Bloemfontein`).
- Use .NET 11 / MAUI 11 features where they fit naturally: `GlobalXmlns.cs` (no `xmlns:` prefixes in XAML), C# Expressions in XAML (`{IsTalk}`, `{Topics.Count > 0}`, `{IsFavorite ? 'Remove from favourites' : 'Add to favourites'}`), AOT-safe `RelativeSource` ViewModel bindings, and `BoxView.Fill` with `LinearGradientBrush`.
- Preserve all existing `AutomationId`s and check light/dark themes and `maui devflow ui diagnostics --profile agent` when done.
