---
description: Restyle the wireframe app into a stunning Festival Pass conference app following DESIGN.md and PRODUCT.md
---

Load the `impeccable` skill and restyle the wireframe app in `src/ConferenceApp/` into a bold, dynamic conference app following `DESIGN.md` and `PRODUCT.md`.

## Key Design & Feature Ideas

1. **Surface Depth, Gradients & Elevation (`Resources/Styles/Colors.xaml` & `Styles.xaml`):**
   - Use tinted cool paper (`#F3F1FA` light) and deep midnight (`#131124` dark) page backgrounds with soft purple ambient shadows (`CardElevationShadow` and `HeroElevationShadow`) so cards genuinely pop off the page.
   - Define multi-stop brand gradients (`HeroGradientBrush`, `AccentGradientBrush`, and `PhotoScrimBrush` for photo overlays).
   - Set `BackgroundColor="Transparent"` on the base `BoxView` style so `BoxView.Color` / `BoxView.Fill` dividers and rails never render with a dark background behind them, and never set `LineHeight` on `Label` styles so multi-line text never clips on Mac Catalyst/iOS.

2. **Custom Vector SVG Tab Icons (`AppShell.xaml` & `Resources/Images/tab_*.svg`):**
   - Create crisp 24×24 vector SVG icons (`tab_agenda.svg`, `tab_event.svg`, `tab_speakers.svg`, `tab_assistant.svg`) in `src/ConferenceApp/Resources/Images/` and wire them to the four `ShellContent` tabs (`Icon="tab_*.png"`) so the tab bar has native tinted icons across platforms.

3. **Event Page (`Views/EventPage.xaml`) — Festival Pass Hero, Sponsor Cards & Edge-to-Edge Venue Photo:**
   - **Hero Pass Banner:** `RoundRectangle 24` hero card with `HeroGradientBrush`, decorative ambient glow orbs, frosted glass `SOUTH AFRICA · 2026` and active city pills, 34pt `.NET Conf 2026 South Africa` display heading, and a frosted bottom metadata strip showing **Host Venue** and **Event Date**.
   - **Sponsors Section:** Elevated sponsor cards with a crisp white logo stage (`#FFFFFF` in both light and dark themes), tier pill, sponsor name, and `›` chevron.
   - **Venue Showcase Card:** Edge-to-edge venue photo at the top of the card with a bottom dark scrim (`PhotoScrimBrush`) and the city pill, venue name, and address overlaid in white text on the photo, followed by the Markdown description, rounded native `<Map />`, and `Accent` **Open in Maps** button.

4. **Agenda Page (`Views/AgendaPage.xaml`) — Milestone Rows vs. Swipeable Talk Cards with Favourite Rail & Pill:**
   - **Break / Milestone Rows (`!IsTalk`):** Compact tinted row with a time pill (`Time`) and title for Registration, Welcome, Lunch, and Q&A.
   - **Talk Cards (`IsTalk`) with `SwipeView` + Inline Save Pill + Favourite Rail:**
     - Wrap each talk card in an enabled `<SwipeView>` with `LeftItems` and `RightItems` containing a `SwipeItem` (`Text="{IsFavorite ? 'Unfavourite' : 'Favourite'}"`, using MAUI 11 `TextColor` and `IconColor` in white on `Accent` pink, bound to `ToggleFavoriteCommand`).
     - Include a left 6pt favourite indicator rail (`BoxView`) that is subtle lavender/slate by default and lights up in vibrant `Accent` pink (`#E63888`) with a matching pink card border when `IsFavorite` is true.
     - Add a dedicated left time column (`Start` stacked above `End` in `HH:mm`) separated by a 1pt vertical `BoxView` divider.
     - On the right details column, show the track pill, AI topic pills (`Topics`), an inline tappable `☆ Save` / `★ Saved` pill (`AutomationId="{$'session-favorite-{Id}'}"`, bound to `ToggleFavoriteCommand`), bold title, summary (`Summary`), and a `<Grid ColumnDefinitions="Auto,*">` speaker strip with circular 28pt speaker avatars (`BindableLayout.ItemsSource="{Binding Speakers}"`) beside `SpeakerNames` (using a `Grid` so multi-speaker names truncate cleanly on narrow phone screens).

5. **Speakers Page (`Views/SpeakersPage.xaml`) — Responsive Multi-Column Grid with Gradient Avatar Rings:**
   - Use `GridItemsLayout` (`Span="{OnIdiom Phone=1, Default=2}"`) with 68pt circular speaker photos framed inside a `HeroGradientBrush` ring, initials fallback, speaker name, tagline, uppercase city pills, and `›` chevron.

6. **Session & Speaker Detail Pages (`Views/SessionPage.xaml` & `Views/SpeakerPage.xaml`) — Hero Pass Headers:**
   - Use `HeroGradientBrush` pass headers with frosted glass metadata/topic pills, a 112pt gradient-ringed circular portrait on `SpeakerPage`, gradient-ringed speaker cards on `SessionPage`, and elevated Markdown abstract/bio cards.

7. **Assistant Page (`Views/ChatPage.xaml`) — AI Concierge Hero Empty State & Rich Chat Bubbles:**
   - Style `CollectionView.EmptyView` with an `ON-DEVICE AI CONCIERGE` hero banner and word-wrapping (`LineBreakMode="WordWrap"`) starter suggestion buttons.
   - Style user messages (`IsUser`) as right-aligned `HeroGradientBrush` bubbles and assistant replies (`!IsUser`) as left-aligned elevated cards with a tinted progress pill (`chat-progress`) and tappable `chat-session-card-{Id}` cards below.

## Technical Guardrails & Verification

- Keep the city switcher in the top-right toolbar on every tab (`Durban`, `Cape Town`, `Bloemfontein`).
- Use .NET 11 / MAUI 11 features naturally: `GlobalXmlns.cs` (no `xmlns:` prefixes in XAML), C# Expressions in XAML (`{IsTalk}`, `{Topics.Count > 0}`, `{IsFavorite ? '★ Saved' : '☆ Save'}`), AOT-safe `RelativeSource` ViewModel bindings, `SwipeItem` `TextColor`/`IconColor`, and `BoxView.Fill` with `LinearGradientBrush`.
- Preserve all existing `AutomationId`s, build and redeploy to Mac Catalyst / simulator / device, and verify light/dark themes and `maui devflow ui diagnostics --profile agent`.

