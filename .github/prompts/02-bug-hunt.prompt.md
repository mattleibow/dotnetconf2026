---
description: Use MAUI DevFlow Logs, Storage, UI Diagnostics, and Screenshots to find and fix four app issues
---

Users reported four issues in `src/ConferenceApp/`, none of which crash the app:

1. **Speaker details page is blank:** Tapping a speaker on the Speakers tab or a Session page opens an empty Speaker page with no name, bio, or sessions.
2. **Favourites filter is always empty:** Tapping *"Add to favourites"* on a session flips the button state, but switching Agenda to **Showing: Favourites** shows an empty list.
3. **Venue map is invisible:** The Event tab loads the venue name and address, but the embedded map doesn't appear.
4. **Search and chat text boxes have visual artifacts:** The `Entry` inputs in the search and chat bars look strange and have visual artifacts inside their containers.

Work fast and keep tool calls minimal:

- First check `git status --short`: if the four fixes are already present in the working tree (for example during a synthesis pass) and the fixed app is already running on the iOS simulator, do **not** re-run the DevFlow reproduction, rebuild, or verification—inspect `git diff` and summarize the findings and fixes immediately.
- Otherwise, load the `maui-devflow-debug` skill, reuse the running iOS simulator app from `maui devflow list`, and diagnose all four issues in one fast pass with **MAUI DevFlow** before reading source code:
  1. Favourite a session, check `maui devflow storage preferences list`, and toggle **Showing: Favourites**.
  2. Navigate to `//speakers`, tap a speaker card, and inspect `maui devflow logs --limit 20` to find the logged exception and stack trace.
  3. Navigate to `//event` and inspect `venue-map` with `maui devflow ui tree` and `maui devflow ui diagnostics --profile agent` to see why it has zero height.
  4. Take a screenshot with `maui devflow ui screenshot` to see the inner border artifacts on the search/chat `Entry` boxes.
- Read the four affected files in parallel, apply all four fixes in a single batch of edits, and rebuild/redeploy once to the booted iOS simulator (`dotnet build -f net11.0-ios --no-restore` + `xcrun simctl install` + `xcrun simctl launch` + `maui devflow wait`).
- Leave the fixed app running on the simulator and stop immediately—do not run a post-fix click-through loop, do not build other platforms, and do not commit changes.
