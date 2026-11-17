---
description: Pre-build, launch on the booted iOS simulator, and open the Mobile Device and DevFlow Inspector canvases before a live demo
---

Prepare this workspace for a live **MAUI DevFlow** demo on the booted iOS simulator:

1. Load the `maui-devflow-debug` skill.
2. Open the **Mobile Device** canvas (`open_canvas` with `canvasId: "mobile-device"`) and select the booted iOS simulator (`select_device`) so it is visible in the side panel.
3. Build `src/ConferenceApp/ConferenceApp.csproj` for `net11.0-ios` (`-t:Build` targeting the booted simulator UDID), install and launch the `.app` on the booted simulator with `xcrun simctl`, and wait for the agent with `maui devflow wait`.
4. Clear any saved preferences with `maui devflow storage preferences clear` so the app starts in a clean state on the **Agenda** tab.
5. Open the **MAUI DevFlow Web Inspector** (`http://localhost:19223/inspector/<agent-id>/` from `maui devflow list`) in a **Browser** canvas (`open_canvas` with `canvasId: "browser"`, `title: "DevFlow Inspector"`), and switch the Inspector to **Inspect** mode (or release its writer lease via `POST /inspector/<agent-id>/api/control` with `{"action":"release"}`) so `maui devflow` CLI commands can drive the app without a lease conflict.
6. Leave the simulator and app running and ready for the live demo. Do not modify or commit any files, and do not build other platforms.
