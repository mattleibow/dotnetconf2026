---
description: Add an on-device conference assistant
---

Add an assistant page where people can ask about sessions, speakers, sponsors and the venue. Answers must come from the app's data, never made up, and suggested sessions should be tappable.

Use the local chat clients from `Microsoft.Maui.Essentials.AI` behind `IChatClient`. Start with whichever one works on the device we have. No cloud yet.

- Give the model the app's services, especially search, as read-only tools using `Microsoft.Maui.AI.Attributes`.
- On Apple, tools run inside the native client, so pass the service provider with `functionInvocationServices`.
- If the model can't call tools, look the data up in app code and put it in the prompt instead.
- Check whether the model is available and show a friendly message if not. Don't download models without asking.
- Stream replies, allow cancelling, and let me start a new chat.
- Read-only for now: it can suggest, but not change anything.

Check the current docs first:

- [Local chat clients](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Provider capabilities and tools](https://github.com/dotnet/maui-labs/blob/main/docs/ai/local-chat-provider.md)
- [Source-generated app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md)

Use DevFlow to ask about a specific session, an interest ("I like AI"), the venue, and something the data can't answer. If the device can't run the model, say so.
