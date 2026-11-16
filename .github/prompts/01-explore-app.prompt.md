---
description: Launch the app with MAUI DevFlow, favourite the .NET MAUI session, and open its speaker profile
---

Load the `maui-devflow-debug` skill, build and launch `src/ConferenceApp/` on Mac Catalyst (or the active simulator/device), and open the **MAUI DevFlow Web Inspector** (`http://localhost:19223/inspector/<agent-id>/` from `maui devflow list`) in a **Browser** canvas (`open_canvas` with `canvasId: "browser"`, `title: "DevFlow Inspector"`).

Then use `maui devflow` to complete this task in the running app:

1. Navigate to the **Agenda** tab, find the **.NET MAUI** session, and open its session detail page.
2. Add the session to **Favourites** and verify in DevFlow Storage (`maui devflow storage preferences list`) that the favourite ID is persisted.
3. From the session detail page, tap the speaker to open their **Speaker** profile page and take a screenshot.
4. Navigate back to **Agenda**, toggle the filter to **Showing: Favourites**, and confirm the session appears in the filtered list.
