---
description: Launch the wireframe app in the Mobile Canvas and DevFlow Inspector and drive it live
---

Load the `maui-devflow-debug` skill, deploy and launch the wireframe app in `src/ConferenceApp/` on the booted iOS simulator, and walk through it live with me:

1. **Open the Mobile Device canvas:** Call `open_canvas` (`canvasId: "mobile-device"`) and select the booted iOS simulator (`select_device`) so we can watch the live simulator screen in the side panel.
2. **Open the MAUI DevFlow Web Inspector:** Find the running iOS agent in `http://localhost:19223/inspector/` (or `maui devflow list`) and open `http://localhost:19223/inspector/<agent-id>/` in a **Browser** canvas (`open_canvas` with `canvasId: "browser"`, `title: "DevFlow Inspector"`).
3. **Inspect the visual tree & layout diagnostics:** Run `maui devflow ui tree --depth 4 --format compact` and `maui devflow ui diagnostics --profile agent` to inspect the wireframe's visual tree and layout health.
4. **Drive the UI and inspect `Preferences`:**
   - Navigate to the **Agenda** tab (`//agenda`), open a talk, and tap `favorite-button` to favourite it.
   - Run `maui devflow storage preferences list` (and `get favorites`) to show the saved favourite IDs inside the app's sandboxed `Preferences`.
   - Navigate back to **Agenda**, tap `favorites-filter` to filter down to **Favourites**, and use `agenda-search` to run a semantic query such as `"football forecasts"` or `"gradient descent"`.
   - Run `maui devflow logs --limit 25 --no-json` to show the live `[AI]` and `[AI Search]` logs captured by the DevFlow agent.
   - Switch to **Speakers** (`//speakers`), open a speaker detail page, and then check **Assistant** (`//assistant`).

Don't change any code in this step—just summarize what you found in the visual tree, diagnostics, `Preferences`, and AI logs so we're ready to style the app in the next step.
