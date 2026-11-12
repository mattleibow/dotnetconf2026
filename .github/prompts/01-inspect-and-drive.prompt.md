---
description: Launch the wireframe app in the Mobile Canvas and DevFlow Inspector and drive it live
---

Load the `maui-devflow-debug` skill, launch the wireframe app in `src/ConferenceApp/`, and walk through it live with me:

1. **Open the Mobile Device canvas:** Deploy and launch on the booted iOS simulator (`net11.0-ios`), call `open_canvas` (`canvasId: "mobile-device"`), and select the booted iOS simulator (`select_device`) so we can watch the live simulator screen in the side panel.
2. **Open the MAUI DevFlow Web Inspector:** Also launch the Mac Catalyst app (`net11.0-maccatalyst`, where Apple Intelligence is available on macOS) and open its DevFlow Web Inspector (`http://localhost:19223/inspector/<agent-id>/` from `maui devflow list`) in a **Browser** canvas (`open_canvas` with `canvasId: "browser"`, `title: "DevFlow Inspector"`).
3. **Inspect the visual tree & layout diagnostics:** Run `maui devflow ui tree --depth 4 --format compact` and `maui devflow ui diagnostics --profile agent` to inspect the wireframe's visual tree and layout health.
4. **Drive the UI, inspect `Preferences`, and verify all AI logs:**
   - Navigate to the **Agenda** tab (`//agenda`), open a talk, and tap `favorite-button` to favourite it.
   - Run `maui devflow storage preferences list` (and `get favorites`) to show the saved favourite IDs inside the app's sandboxed `Preferences`.
   - Navigate back to **Agenda**, tap `favorites-filter` to filter down to **Favourites**, and use `agenda-search` to run a semantic query such as `"football forecasts"` or `"gradient descent"`.
   - Switch to **Speakers** (`//speakers`), open a speaker detail page, and then switch to **Assistant** (`//assistant`) on Mac Catalyst and ask `"What's Niels' background?"` followed by `"Plan my day: mobile and AI, leaving at 3"` (use `maui devflow batch --delay 6000` when chaining multiple UI mutations).
   - Run `maui devflow logs --limit 50 --no-json` to verify all three log layers are streaming:
     1. **General app AI logs:** `[AI]`, `[AI Search]`, `[AI RAG]`, `[AI Router]`, `[AI Tool]`, and `[AI Workflow]`
     2. **`Microsoft.Extensions.AI` & `Microsoft.Maui.Essentials.AI` logs:** `LoggingChatClient`, `LoggingEmbeddingGenerator`, and `AppleIntelligenceChatClient` tool invocations
     3. **`Microsoft.Agents.AI` Agent Framework logs:** `ChatClientAgent` and `TextSearchProvider`

Don't change any code in this step—just summarize what you found in the visual tree, diagnostics, `Preferences`, and the three log layers so we're ready to style the app in the next step.
