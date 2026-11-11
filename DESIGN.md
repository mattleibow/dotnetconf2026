---
name: .NET Conf 2026 South Africa
description: Offline companion app for .NET Conf 2026 across Cape Town, Johannesburg, Pretoria, and Durban
colors:
  primary: "#512BD4"
  primary-dark: "#3B1F99"
  primary-text: "#512BD4"
  secondary: "#6C3DD8"
  accent: "#E63888"
  accent-text: "#C2185B"
  page-bg: "#F8F9FA"
  surface: "#FFFFFF"
  text-primary: "#1A1A2E"
  text-secondary: "#5C5C70"
  divider: "#E6E6EE"
typography:
  display:
    fontFamily: "OpenSansSemibold"
    fontSize: "32"
    fontWeight: 700
    lineHeight: 1.15
  title:
    fontFamily: "OpenSansSemibold"
    fontSize: "22"
    fontWeight: 700
    lineHeight: 1.2
  subtitle:
    fontFamily: "OpenSansSemibold"
    fontSize: "17"
    fontWeight: 600
    lineHeight: 1.3
  body:
    fontFamily: "OpenSansRegular"
    fontSize: "15"
    fontWeight: 400
    lineHeight: 1.45
  caption:
    fontFamily: "OpenSansRegular"
    fontSize: "13"
    fontWeight: 400
    lineHeight: 1.35
  label:
    fontFamily: "OpenSansSemibold"
    fontSize: "12"
    fontWeight: 600
    letterSpacing: "0.5"
rounded:
  sm: "8"
  md: "16"
  pill: "999"
spacing:
  xs: "4"
  sm: "8"
  md: "12"
  lg: "16"
  xl: "24"
  xxl: "32"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    typography: "{typography.subtitle}"
    rounded: "{rounded.md}"
    padding: "14 20"
  button-accent:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.surface}"
    typography: "{typography.subtitle}"
    rounded: "{rounded.md}"
    padding: "14 20"
  chip-city-selected:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.primary}"
    typography: "{typography.caption}"
    rounded: "{rounded.pill}"
    padding: "6 14"
  card-session:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: "16"
---

# Design System: .NET Conf 2026 South Africa

## Overview

**Creative North Star: "The Festival Pass"**

Confident, colourful, and calm enough to read on a phone in a busy hallway between sessions. The brand's electric purple and magenta live in the top hero header, track badges, and favourite actions, while schedules, abstracts, speaker bios, and assistant replies sit on crisp, quiet cards with generous breathing room.

Speaker photos, sponsor logos, and venue photography do the visual work—never stock illustrations, emoji, or decorative chrome.

**Key Characteristics:**
- **Bold brand, quiet content:** One signature gradient per screen; clean white/slate cards underneath.
- **Hallway scanability:** Time slots, room chips, and speaker names read at a glance.
- **Adaptive native ergonomics:** Bottom tabs and single-column cards on phones; multi-column grids and split detail views on wide desktop windows.

## Colors

The .NET Conf 2026 brand palette pairs deep .NET purple with vibrant magenta against cool paper neutrals in light mode and deep indigo-slate surfaces in dark mode.

### Primary
- **.NET Purple** (`Primary` — Light `#512BD4`, Dark `#8454E4`): Brand headers, primary buttons, selected states, user chat bubbles, and Track 1 accents.
- **Deep Violet** (`PrimaryDark` — `#3B1F99`): Pressed states and gradient anchors.
- **Purple Ink** (`PrimaryText` — Light `#512BD4`, Dark `#B79BFF`): High-contrast purple text on 12% tinted chips and links.

### Secondary
- **Electric Indigo** (`Secondary` — Light `#6C3DD8`, Dark `#8454E4`): Avatar fallback backgrounds and Track 2 accents.

### Tertiary
- **Conference Pink** (`Accent` — Light `#E63888`, Dark `#F05A9E`): Favourite hearts, Track 3 accents, and primary favourite CTAs.
- **Crimson Rose** (`AccentText` — Light `#C2185B`, Dark `#FF8FC0`): High-contrast pink text on 12% tinted chips.

### Neutral
- **Cool Paper / Midnight Slate** (`PageBackground` — Light `#F8F9FA`, Dark `#1A1A2E`): Root page background.
- **Pure White / Indigo Surface** (`Surface` — Light `#FFFFFF`, Dark `#24243E`): Cards, sheets, and assistant chat bubbles.
- **Deep Ink / Crisp White** (`TextPrimary` — Light `#1A1A2E`, Dark `#F8F9FA`): Session titles and body copy.
- **Muted Slate / Soft Lavender** (`TextSecondary` — Light `#5C5C70`, Dark `#B4B4C8`): Times, rooms, and speaker taglines.
- **Hairline Grey / Indigo Border** (`Divider` — Light `#E6E6EE`, Dark `#34344F`): Card borders in dark mode and subtle dividers.

### Named Rules
**The Single Gradient Rule.** Use the 135° signature gradient (`Primary` to `Accent`) at most once per screen—for the top hero header—and nowhere else.

**The Tinted Chip Contrast Rule.** Track and topic chips use the accent colour at 12% opacity for the background paired with `PrimaryText` or `AccentText` for the label, never the raw base colour on its own tint.

## Typography

**Display & UI Font:** `OpenSansSemibold`
**Body Font:** `OpenSansRegular`

**Character:** Clean, open humanist sans-serif tuned for rapid scanning on mobile screens and comfortable reading across multi-paragraph talk abstracts.

### Hierarchy
- **Display** (Bold, 32pt, line-height 1.15): Hero headings (event name, page header).
- **Title** (Bold, 22pt, line-height 1.2): Page and section titles, speaker names on detail views.
- **Subtitle** (Semibold, 17pt, line-height 1.3): Card titles and session titles (max 2 lines on cards, full length on detail pages).
- **Body** (Regular, 15pt, line-height 1.45): Abstracts, speaker bios, venue descriptions, and chat messages (constrained to ~70ch on wide windows).
- **Caption** (Regular, 13pt, line-height 1.35): Speaker names on cards and secondary metadata.
- **Label** (Semibold, 12pt, uppercase, character spacing 0.5): Time slots, track chips, room badges, and section headers.

## Layout

Spacing follows a strict 4pt grid: `4`, `8`, `12`, `16`, `24`, `32`.
- **Phones (<800pt):** 16pt page padding, bottom tab bar (Agenda, Speakers, Favourites, Venue, Sponsors, Assistant), single-column lists, full-width cards.
- **Wide windows (≥800pt):** 24pt page padding, content centred with a maximum width of 1,100pt, multi-column card grids for parallel sessions and speakers, and two-column detail pages (header and speaker cards on the left, abstract on the right).
- **Responsive switching:** Switch layouts in one place per page using `AdaptiveTrigger` visual states or page width, never per control.

## Elevation & Depth

Cards sit above `PageBackground` using a hybrid light/dark depth model:
- **Light mode:** `Surface` (`#FFFFFF`) background with a soft purple ambient shadow (`Primary` at 15% opacity, 12pt blur, 4pt vertical offset).
- **Dark mode:** `Surface` (`#24243E`) background with a crisp 1pt `Divider` (`#34344F`) stroke instead of drop shadows.

## Shapes

- **Cards & Containers:** 16pt rounded corners (`Border` with `RoundRectangle 16`).
- **Chips & Pills:** Fully rounded (`RoundRectangle 999`), 12pt horizontal and 4pt vertical padding.
- **Avatars:** Circular (`Ellipse` or `RoundRectangle` at half size), 40pt in lists and 96pt on speaker detail pages, falling back to initials on `Secondary`.
- **Sponsor Tiles:** 12pt rounded white tiles (`#FFFFFF` in both light and dark themes so dark sponsor logos stay legible).

## Components

### Hero Header & City Switcher
- **Header:** 135° `Primary`-to-`Accent` gradient banner displaying the event name in `Display`, selected city and date in `Label`, and city pill chips directly underneath. Hide the Shell nav bar on pages that render their own hero header so titles never appear twice.
- **City Chips:** Four horizontal pill chips (Cape Town, Johannesburg, Pretoria, Durban); the selected city is solid white (`#FFFFFF`) with `Primary` text, and unselected cities are outlined in translucent white with white text.

### Session Cards & General Items
- **Talk Cards:** 16pt rounded `Surface` card with time and track chip along the top, session title in `Subtitle` (max 2 lines), speaker avatars + names in `Caption`, and a 44×44pt heart toggle aligned top-right (filled `Accent` heart when favourited, outline `TextSecondary` heart otherwise). Entire card is tappable with `Background="Transparent"` on hit targets.
- **General Items** (registration, breaks, lunch): Compact single row with time and title—no card chrome and no favourite heart.

### Detail Pages (Session & Speaker)
- **Session Page:** Gradient header with title, time and room chips, tappable speaker rows with 40pt photos, full abstract in `Body`, and a full-width `Accent` favourite button.
- **Speaker Page:** Centered 96pt circular photo, name in `Title`, tagline in `Caption`, bio in `Body`, and their sessions rendered as tappable cards.

### Venue & Sponsors
- **Venue:** Full-width venue photo with 16pt rounded corners, venue name, address, description, and a primary "Open in Maps" button.
- **Sponsors:** Logos centred on white tiles grouped by tier.

### Assistant
- **Suggestion Chips:** Horizontal scrollable pill chips at the top (`Surface` background, `PrimaryText` label, 1pt `Divider` stroke).
- **Message Bubbles:** User messages right-aligned in `Primary` with white text; assistant replies left-aligned on `Surface` cards with `TextPrimary` text and formatted bold, italic, and bullet spans.
- **Input Bar:** Pinned bottom bar with a rounded `Surface` `Entry` and a `Primary` Send button.

## Do's and Don'ts

### Do:
- **Do** define all colours in `Resources/Styles/Colors.xaml` using `AppThemeBinding` and all typography/card/chip styles in `Resources/Styles/Styles.xaml`.
- **Do** set `Background="Transparent"` on any layout with a `TapGestureRecognizer` so taps register reliably on iOS and Mac Catalyst.
- **Do** maintain at least 44×44pt touch targets on Apple platforms and 48×48dp on Android, with `SemanticProperties.Description` on icon buttons.

### Don't:
- **Don't** put raw hex colours, magic font sizes, or ad-hoc spacing in individual page XAML files.
- **Don't** place pink text on purple backgrounds or use white text smaller than 18pt bold on `Accent` pink.
- **Don't** use emoji, stock illustrations, or invented conference slogans anywhere in the UI.
