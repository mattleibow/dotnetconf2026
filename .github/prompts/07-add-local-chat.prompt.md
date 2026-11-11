---
description: Add an on-device assistant primed with the agenda
---

Add an assistant page using the on-device chat client from `Microsoft.Maui.Essentials.AI` behind `IChatClient`. Start with whichever provider works on the device we have. No cloud.

Put what it needs to know in the system instructions: the event, the selected city, its date and venue, the current time, my favourites, and the city's agenda as one compact line per session (time, track, title, speakers, topic, summary). Apple's model has only about 4,000 tokens, so keep it tight. Rebuild the instructions when the city, favourites or summaries change.

- Answer only from this data, and say so when it doesn't know.
- Stream replies, allow cancelling, and let me start a new chat.
- Check whether the model is available and show a friendly message if not. Don't download models without asking.
- In Debug, add a setting to pretend it's a given time on event day, so "what's next?" works before the event.

Docs: [local chat clients](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md).

Use DevFlow to ask "When is Matthew's talk?", "What's on after lunch?", "Anything on AI agents?", and "What's Niels' background?" (it should admit it doesn't know).
