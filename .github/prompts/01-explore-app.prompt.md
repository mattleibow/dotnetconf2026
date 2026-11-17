---
description: Launch the app with MAUI DevFlow, favourite the .NET MAUI session, and open its speaker profile
---

Load the `maui-devflow-debug` skill and reuse the running iOS simulator app from `maui devflow list` (or build and launch `net11.0-ios` on the booted simulator if not running).

Work fast and complete this task in a single pass with `maui devflow` (pipe mutating UI steps into a single `maui devflow batch` call so they share one CLI process lease with zero 10-second lease waits):

1. Navigate to the **Agenda** tab, find the **.NET MAUI** session, and open its session detail page.
2. Add the session to **Favourites** and check `maui devflow storage preferences list` to see the saved favourite ID.
3. From the session detail page, tap the speaker to open their **Speaker** profile page and take a screenshot.
4. Navigate back to **Agenda** and toggle the filter to **Showing: Favourites**. Leave the app running on the simulator and stop immediately—do not run extra verification passes or build other platforms.
