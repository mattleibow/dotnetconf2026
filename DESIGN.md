---
name: .NET Conf 2026 South Africa
description: Offline companion app for .NET Conf 2026 across Durban, Cape Town, and Bloemfontein
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
  title:
    fontFamily: "OpenSansSemibold"
    fontSize: "22"
    fontWeight: 700
  subtitle:
    fontFamily: "OpenSansSemibold"
    fontSize: "17"
    fontWeight: 600
  body:
    fontFamily: "OpenSansRegular"
    fontSize: "15"
    fontWeight: 400
  caption:
    fontFamily: "OpenSansRegular"
    fontSize: "13"
    fontWeight: 400
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
- **Display** (Bold, 32pt): Hero headings (event name, page header).
- **Title** (Bold, 22pt): Page and section titles, speaker names on detail views.
- **Subtitle** (Semibold, 17pt): Card titles and session titles (max 2 lines on cards, full length on detail pages).
- **Body** (Regular, 15pt): Abstracts, speaker bios, venue descriptions, and chat messages (constrained to ~70ch on wide windows).
- **Caption** (Regular, 13pt): Speaker names on cards and secondary metadata.
- **Label** (Semibold, 12pt, uppercase, character spacing 0.5): Time slots, track chips, room badges, and section headers.

## Layout

Spacing follows a strict 4pt grid: `4`, `8`, `12`, `16`, `24`, `32`.
- **Phones (<800pt):** 16pt page padding, 4 bottom tabs (**Event**, **Agenda**, **Speakers**, **Assistant**), single-column lists, full-width cards.
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

### Event Page, Hero Header & City Switcher
- **Event Page (Tab 1):** Starts with the 135° `Primary`-to-`Accent` hero banner displaying `.NET Conf 2026 South Africa` in `Display` and the selected city and date in `Label`, followed by the **Sponsors** section (logos centred on white tiles grouped by tier) and then the **Venue** section (venue photo, name, address, description, embedded native `Map` with pin, and "Open in Maps" button).
- **Global City Switcher:** Every tab (Event, Agenda, Speakers, Assistant) exposes the city switcher in the top-right toolbar showing the active city (`Durban`, `Cape Town`, or `Bloemfontein`) that opens the native city action sheet—do not put a second city picker inside the page body.

### Agenda & Session Cards
- **Search & Filter Bar:** Top bar on Agenda with a rounded search input (`AutomationId="agenda-search"`) and the **Showing: All Sessions** / **Showing: Favourites** filter toggle (`AutomationId="favorites-filter"`).
- **Talk Cards:** 16pt rounded `Surface` card with time and track metadata (`Meta`), horizontal AI topic pills (`BindableLayout.ItemsSource="{Binding Topics}"`, `IsVisible="{Topics.Count > 0}"`) along the top, session title in `Subtitle` (max 2 lines), AI summary (`Summary`, `IsVisible="{!string.IsNullOrWhiteSpace(Summary)}"`, max 2 lines), and speaker names (`SpeakerNames`) in `Caption`. Entire card is tappable with `Background="Transparent"` on hit targets.
- **General Items** (registration, breaks, lunch): Compact row with time and title.

### Detail Pages (Session & Speaker)
- **Session Page:** Header with title, time/track metadata (`CityMeta`), horizontal topic pills (`Topics` when `Topics.Count > 0`), tappable speaker rows (`AutomationId="{$'speaker-{Id}'}"`) with circular 40pt photos, full abstract in `Body` (`MarkdownToFormattedStringConverter`), and a full-width `Accent` favourite button (`AutomationId="favorite-button"`).
- **Speaker Page:** Centered 96pt circular photo, name in `Title`, tagline in `Caption`, bio in `Body`, and their sessions rendered as tappable cards (`AutomationId="{$'speaker-session-{Id}'}"`) with `CityMeta`, horizontal topic pills (`Topics` when `Topics.Count > 0`), title, and AI summary (`Summary`).

### Assistant
- **Empty State Suggestions (`CollectionView.EmptyView`):** Centered vertical stack of tappable starter suggestion buttons (`BindableLayout.ItemsSource="{Binding Suggestions}"`, `Surface` background, `PrimaryText` label, 1pt `Divider` stroke) shown when `Messages` is empty.
- **Message Bubbles & Live Progress:** User messages (`IsUser`) right-aligned in `Primary` with white text; assistant replies left-aligned on `Surface` cards with `TextPrimary` text bound via `MarkdownToFormattedStringConverter` and an inline italic progress status label (`Progress`, `IsVisible="{!string.IsNullOrWhiteSpace(Progress)}"`, `AutomationId="chat-progress"`).
- **Referenced Session Cards:** Tappable session cards (`BindableLayout.ItemsSource="{Binding Sessions}"`, `AutomationId="{$'chat-session-card-{Id}'}"`) rendered cleanly below the assistant reply bubble.
- **Bottom Input Bar:** Pinned bottom bar with the **New chat** button (`AutomationId="chat-new"`), rounded `Surface` `Entry` (`AutomationId="chat-input"`), and `Primary` **Send** button (`AutomationId="chat-send"`).

## Do's and Don'ts

### Do:
- **Do** define all colours in `Resources/Styles/Colors.xaml` using `AppThemeBinding` and all typography/card/chip styles in `Resources/Styles/Styles.xaml`.
- **Do** set `Background="Transparent"` on any layout with a `TapGestureRecognizer` so taps register reliably on iOS and Mac Catalyst.
- **Do** maintain at least 44×44pt touch targets on Apple platforms and 48×48dp on Android, with `SemanticProperties.Description` on icon buttons.

### Don't:
- **Don't** put raw hex colours, magic font sizes, or ad-hoc spacing in individual page XAML files.
- **Don't** set `LineHeight` on `Label` styles or controls (let the native platform typography metrics handle line spacing so multi-line labels never clip on Mac Catalyst/iOS).
- **Don't** place pink text on purple backgrounds or use white text smaller than 18pt bold on `Accent` pink.
- **Don't** use emoji, stock illustrations, or invented conference slogans anywhere in the UI.
