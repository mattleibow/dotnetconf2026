---
description: Use MAUI DevFlow logs, storage, UI diagnostics, and screenshots to find and fix four app issues
---

Users reported four issues in `src/ConferenceApp/`, none of which crash the app:

1. **Speaker details page is blank:** Tapping a speaker on the Speakers tab or a Session page opens an empty Speaker page with no name, bio, or sessions.
2. **Favourites filter is always empty:** Tapping *"Add to favourites"* on a session flips the button state, but switching Agenda to **Showing: Favourites** shows an empty list.
3. **Venue map is invisible:** The Event tab loads the venue name and address, but the embedded map doesn't appear.
4. **Search and chat text boxes have visual artifacts:** The `Entry` inputs in the search and chat bars look strange and have visual artifacts inside their containers.

Work on the **`mattleibow-demo-bugs-for-devflow-bug-hunt`** branch (it should already contain the latest `main`). Load the `maui-devflow-debug` skill and use the iOS app built from this workspace.

## Reproduce with DevFlow first

Gather **MAUI DevFlow** evidence for each issue before reading source:

1. Favourite a session, inspect the stored keys and IDs with `maui devflow storage preferences list`, and switch Agenda to **Showing: Favourites**.
2. Open a speaker from the Speakers tab and from a session page, and find the exception in `maui devflow logs`.
3. On the Event tab, check the bounds of `venue-map` and run `maui devflow ui diagnostics --profile agent --root venue-map`.
4. Screenshot the Agenda search box and the Assistant chat box.

## Fix and verify

Make the smallest coherent fixes using current .NET 11 / MAUI 11 APIs. Redeploy, then verify on the running build: the favourite reads back and appears under Favourites, both speaker entry points load without a new exception, the map has a positive height and is visible, and both inputs have clean borders.

Leave the fixed app running on Agenda. Summarize causes, fixes, and anything unverified. Don't build other platforms or commit changes.
