---
description: Use MAUI DevFlow Logs, Storage, and UI Diagnostics to find and fix three silent app bugs
---

Users reported three issues in `src/ConferenceApp/`, none of which crash the app:

1. **Speaker details page is blank:** Tapping a speaker on the Speakers tab or a Session page opens an empty Speaker page with no name, bio, or sessions.
2. **Favourites filter is always empty:** Tapping *"Add to favourites"* on a session flips the button state, but switching Agenda to **Showing: Favourites** shows an empty list.
3. **Venue map is invisible:** The Event tab loads the venue name and address, but the embedded map doesn't appear.

Load the `maui-devflow-debug` skill, launch the app, and use **MAUI DevFlow** rather than guessing from source code first:

1. Reproduce the Speaker page issue and inspect `maui devflow logs` (or the **Logs** tab in the DevFlow Web Inspector) to find the logged exception and stack trace.
2. Favourite a session and inspect `maui devflow storage preferences list` (or the **Storage** tab in the DevFlow Web Inspector) to see what keys and values are actually stored in `Preferences`.
3. Inspect the Event tab with `maui devflow ui tree` and `maui devflow ui diagnostics --profile agent` to see why `venue-map` has zero height.
4. Fix all three bugs, redeploy, and verify each fix live with `maui devflow`.
