# Design guide

The app should look like a polished, shipping conference app from the very first build: confident, colourful, and calm enough to read on a phone in a busy hallway. It celebrates .NET Conf 2026, .NET 11 and .NET MAUI 11.

This guide is the single source of truth for the look of the app. Put it into the theme first, then build pages from the theme.

## Personality

- **Bold brand, quiet content.** Purple and pink belong in headers, chips and calls to action. Lists and text stay clean, with plenty of white space.
- **Real, not decorative.** Speaker photos, sponsor logos and the venue photo do the visual work. No stock illustrations, invented slogans or emoji.
- **Native feel.** Bottom tabs on phones, standard back navigation, and platform-sized touch targets. Polish comes from consistency, not custom controls.

## Colour

The .NET Conf 2026 brand, from the event website:

| Resource key | Light | Dark | Use |
|---|---|---|---|
| `Primary` | `#512BD4` | `#8454E4` | .NET purple: headers, primary buttons, selected states, Track 1 |
| `Secondary` | `#6C3DD8` | `#8454E4` | Gradients, Track 2 |
| `PrimaryDark` | `#3B1F99` | `#3B1F99` | Pressed states, dark headers |
| `Accent` | `#E63888` | `#F05A9E` | Pink: favourites, Track 3, the single most important action on a screen |
| `PageBackground` | `#F8F9FA` | `#1A1A2E` | Page background |
| `Surface` | `#FFFFFF` | `#24243E` | Cards and sheets |
| `TextPrimary` | `#1A1A2E` | `#F8F9FA` | Titles and body text |
| `TextSecondary` | `#5C5C70` | `#B4B4C8` | Metadata: times, rooms, captions |
| `Divider` | `#E6E6EE` | `#34344F` | Hairlines and outlines |
| `PrimaryText` | `#512BD4` | `#B79BFF` | Purple text on light tints, such as chips and links |
| `AccentText` | `#C2185B` | `#FF8FC0` | Pink text on light tints |

- **Signature gradient:** 135°, `Primary` to `Accent`. Use it once per screen, for the header or hero, and nowhere else.
- Track chips use the track colour at about 12% opacity as the background, with `PrimaryText` or `AccentText` for the text (the plain colours are too faint on their own tint).
- White text on purple or pink. On `Accent`, white text must be bold and at least 18, because it's only 4:1. Never pink text on purple.
- Check contrast in both themes: body text at least 4.5:1 and large text at least 3:1.

## Typography

Use the template's Open Sans fonts. One type ramp, defined once as styles:

| Style | Size | Weight | Use |
|---|---|---|---|
| `Display` | 32 | Bold | Hero headings: event name, page header |
| `Title` | 22 | Bold | Page and section titles |
| `Subtitle` | 17 | Semibold | Card titles, session titles |
| `Body` | 15 | Regular | Abstracts, bios, descriptions |
| `Caption` | 13 | Regular | Speaker names and secondary text |
| `Label` | 12 | Semibold, uppercase, slight letter spacing | Metadata: times, tracks, section labels |

- Left-align text. Limit abstracts and bios to about 70 characters per line on wide screens.
- Truncate titles at two lines on cards; show everything on detail pages.

## Spacing and shape

- Spacing on a 4 px grid: 4, 8, 12, 16, 24, 32. Page padding is 16 on phones and 24 on wide windows.
- Cards: `Surface` background, 16 px corners (`Border` with `RoundRectangle`), a soft purple shadow (`Primary` at about 15% opacity, 12 px blur, 4 px down) in light mode, and a 1 px `Divider` outline instead of a shadow in dark mode.
- Chips: fully rounded pills, 12 px horizontal and 4 px vertical padding.
- Avatars: circular, 40 px in lists and 96 px on detail pages, with initials on `Secondary` as the fallback.

## Components

- **Header.** The gradient, the event name in `Display`, the selected city and date in `Label`, and the city picker below it. Pages with their own header hide the Shell navigation bar so the title isn't shown twice.
- **City picker.** Three pill chips: the selected city is filled white with `Primary` text, the others are outlined in white. Not a dropdown.
- **Session card.** The time and track chip on top, the title in `Subtitle`, then speaker avatars and names in `Caption`, and a heart toggle aligned top right. The whole card is tappable.
- **General items** (registration, breaks, lunch): a compact single row with an icon, the time and the title. No card, no heart.
- **Time slots.** A `Label` header per slot ("10:25"); parallel sessions sit side by side on wide screens.
- **Detail page.** A gradient header with the title, then time and room chips, speakers with photos (tappable), the abstract, and a full-width favourite button in `Accent`.
- **Speaker page.** A large photo, name in `Title`, tagline in `Caption`, then the bio and their sessions as cards.
- **Sponsors.** Logos centred on white tiles (also white in dark mode, so logos stay legible), grouped by tier.
- **Venue.** The venue photo full-width with rounded corners, the name and address, and an "Open in Maps" button.
- **Assistant.** Suggested question chips at the top, a scrollable message list (user messages right-aligned in `Primary` with white text; assistant replies left-aligned on `Surface` cards), and a pinned bottom input bar with an `Entry` and `Primary` send button.
- **Empty, loading and error states.** A short friendly sentence and, where it helps, a single action. Never a blank screen.

## Icons

- Use simple, single-colour SVG icons as `MauiImage` files (tab icons, heart, map pin, clock), tinted with the theme colours.
- Filled heart in `Accent` for favourites, outline heart in `TextSecondary` otherwise.

## Layout

The same app must look right on a phone in portrait and in a large desktop window.

- **Phones:** bottom tabs (Agenda, Speakers, Favourites, Venue, Assistant), single-column lists, full-width cards.
- **Wide windows (about 800 px and up):** multi-column card grids, content centred with a maximum width of about 1,100 px, and two-column detail pages (header and speakers on one side, the abstract on the other).
- Switch layouts in one place per page with `AdaptiveTrigger` visual states or the window width, not per control.

## Motion

- Keep it subtle: a quick fade-in or scale on the heart when favouriting, and default page transitions.
- Use the `Async` animation methods. No looping or decorative animation.

## Accessibility

- Touch targets at least 44 points on Apple and 48 dp on Android.
- `SemanticProperties.Description` on icons and image-only buttons, and `SemanticProperties.HeadingLevel` on page titles.
- Don't rely on colour alone: favourites use a filled versus outline heart, and tracks show their name.

## Putting it in the theme

- `Resources/Styles/Colors.xaml`: every colour above as a resource, with `AppThemeBinding` for light and dark.
- `Resources/Styles/Styles.xaml`: the type ramp, card, chip, avatar and button styles.
- Pages use these resources only: no raw colours, font sizes or magic numbers in page XAML.
