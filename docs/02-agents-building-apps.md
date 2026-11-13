# Part 2: Agents Building Apps — Live Prompts `01` & `02` (12–15 mins)

This part demonstrates how GitHub Copilot, the Mobile Device canvas, MAUI DevFlow, and the `impeccable` design skill inspect, interact with, and style a running .NET MAUI 11 app.

---

## Step 01: Inspect and Drive ([`.github/prompts/01-inspect-and-drive.prompt.md`](../.github/prompts/01-inspect-and-drive.prompt.md))

Paste `01-inspect-and-drive.prompt.md` into the chat. While the agent runs (~2–3 mins), highlight the two side-by-side canvases and diagnostics:

| Surface | What to show on stage |
|---|---|
| **Mobile Device Canvas** (`mobile-device`) | Live iOS Simulator (or Android Emulator) embedded directly inside the Copilot side panel so the audience watches every tap and transition. |
| **DevFlow Inspector — UI Tree** (`browser` canvas) | Live visual hierarchy, element bounds, visibility, and stable `AutomationId`s (`agenda-search`, `favorites-filter`, `session-<id>`). |
| **DevFlow Inspector — Logs** (`browser` canvas) | Real-time `ILogger` stream from inside the app showing `[AI]`, `LoggingChatClient`, `LoggingEmbeddingGenerator`, and `ChatClientAgent`. |
| **DevFlow Inspector — Storage** (`browser` canvas) | Inspecting and editing sandboxed `Preferences` (`selected_city`, `favorites`) live without writing debug UI. |
| **Layout Diagnostics** (`maui devflow ui diagnostics --profile agent`) | Automated detection of clipped text, off-screen elements, or zero-sized containers so the agent doesn't rely on screenshot guesswork. |

### Driving Multiple UI Steps with `maui devflow batch`

Point out how the agent chains UI interactions over stdin with `maui devflow batch` so a single DevFlow session drives navigation, search, and chat without round-trip overhead:

```sh
printf "ui navigate //agenda\nui fill agenda-search \"football forecasts\"\n" \
  | maui devflow batch --delay 1500 --human
```

---

## Step 02: Make It Beautiful + Add Inline Favourite Hearts ([`.github/prompts/02-make-it-beautiful.prompt.md`](../.github/prompts/02-make-it-beautiful.prompt.md))

Paste `02-make-it-beautiful.prompt.md` into the chat. While the agent works (~5–6 mins), switch to your editor and walk through [**Part 3 (`03-giving-the-app-ai.md`)**](03-giving-the-app-ai.md) so there is zero dead air.

When Step 02 finishes, show the styled app on screen and highlight the MAUI 11 touches requested in `02-make-it-beautiful.prompt.md`:

| MAUI 11 Feature | Where the prompt applies it |
|---|---|
| **`BoxView.Fill` with `LinearGradientBrush`** | Decorative gradient hero banner on `EventPage.xaml` without custom drawing. |
| **`ScaleToAsync` animation** | Async animation on the new 44×44pt inline favourite heart button (`session-favorite-<id>`) on Agenda talk cards. |
| **Native `<Map />` control** | Embedded `Microsoft.Maui.Controls.Maps` map on the Event tab using global XAML namespaces. |
| **`CollectionView.EmptyView` suggestions** | Styled starter prompt cards on `ChatPage.xaml` that disappear automatically once the conversation starts. |
| **Adaptive & themed layouts** | Portrait phone + wide Mac Catalyst desktop layouts and light/dark themes (`AppThemeBinding`) verified with `maui devflow ui diagnostics`. |

Next: [**Part 3 — Giving the App That Same AI (`03-giving-the-app-ai.md`)**](03-giving-the-app-ai.md)
