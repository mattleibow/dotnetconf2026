---
description: Connect the Assistant tab to the on-device chat model
---

Connect `ChatService` to the on-device chat model from `Microsoft.Maui.Essentials.AI` behind `IChatClient` (suppress `MAUIAI0001`). Start with whichever provider works on the current device. No cloud.

- Note: `Microsoft.Maui.Essentials.AI` requires Android API 26 (`SupportedOSPlatformVersion` `26.0` for `net11.0-android` in `ConferenceApp.csproj`).
- Wrap the registered `IChatClient` with `.AsBuilder().UseLogging(loggerFactory).Build()` (and set `builder.Logging.SetMinimumLevel(LogLevel.Information)`) so `maui devflow logs` captures every on-device chat turn.
- Give it the event name, the selected city, its date and venue, my favourites, and the city's agenda as one compact line per session (time, track, title, speakers—no abstracts yet) in the system instructions.
- Pass the conversation history on each turn, and rebuild the system instructions when the city or favourites change without losing the conversation.
- Answer only from this data, and say so when the answer isn't in the schedule.
- Stream replies, show an inline `Progress` status while waiting for the first token, allow starting a new chat, and show a friendly message if no on-device model is available.

Docs: [local chat clients](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md).

Use DevFlow on Mac Catalyst to ask "When is Matthew's talk?", "What's on after lunch?", and "Is there anything about football?" (it only sees titles right now, so it shouldn't know which talk covers football), and check `maui devflow logs --limit 20 --no-json`.
