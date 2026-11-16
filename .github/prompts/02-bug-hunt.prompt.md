---
description: Use MAUI DevFlow Logs, Storage, UI Diagnostics, and Screenshots to find and fix four app issues
---

Users reported four issues in `src/ConferenceApp/`, none of which crash the app:

1. **Speaker details page is blank:** Tapping a speaker on the Speakers tab or a Session page opens an empty Speaker page with no name, bio, or sessions.
2. **Favourites filter is always empty:** Tapping *"Add to favourites"* on a session flips the button state, but switching Agenda to **Showing: Favourites** shows an empty list.
3. **Venue map is invisible:** The Event tab loads the venue name and address, but the embedded map doesn't appear.
4. **Search and chat text boxes have a double border:** The `Entry` inputs inside the rounded search and chat bars draw a second inner native border/underline inside the `Border`.

Load the `maui-devflow-debug` skill, launch the app, and use **MAUI DevFlow** rather than guessing from source code first:

1. Reproduce the Speaker page issue and inspect `maui devflow logs` (or the **Logs** tab in the DevFlow Web Inspector) to find the logged exception and stack trace.
2. Favourite a session and inspect `maui devflow storage preferences list` (or the **Storage** tab in the DevFlow Web Inspector) to see what keys and values are actually stored in `Preferences`.
3. Inspect the Event tab with `maui devflow ui tree` and `maui devflow ui diagnostics --profile agent` to see why `venue-map` has zero height.
4. Take a screenshot with `maui devflow ui screenshot` to inspect the `Entry` text boxes inside `SearchContainerBorder` and strip the inner native border/underline via `EntryHandler.Mapper`.
5. Fix all four issues, redeploy, and verify each fix live with `maui devflow`.
