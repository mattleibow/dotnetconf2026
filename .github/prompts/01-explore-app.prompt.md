---
description: Launch the app with MAUI DevFlow, favourite the .NET MAUI session, and open its speaker profile
---

Load the `maui-devflow-debug` skill, connect to `src/ConferenceApp/` on the booted iOS simulator (reuse the running app from `maui devflow list` if already launched, or build and launch `net11.0-ios` on the booted simulator), and ensure the **Mobile Device** canvas (`canvasId: "mobile-device"`) and **MAUI DevFlow Web Inspector** (`http://localhost:19223/inspector/<agent-id>/` in a **Browser** canvas with `title: "DevFlow Inspector"`) are open.

Then use `maui devflow` (`maui devflow batch` for multi-step UI flows) to complete this task in the running app:

1. Navigate to the **Agenda** tab, find the **.NET MAUI** session, and open its session detail page.
2. Add the session to **Favourites** and verify in DevFlow Storage (`maui devflow storage preferences list`) that the favourite ID is persisted.
3. From the session detail page, tap the speaker to open their **Speaker** profile page and take a screenshot.
4. Navigate back to **Agenda**, toggle the filter to **Showing: Favourites**, and confirm the session appears in the filtered list. Leave the app running on the simulator when done.
