---
description: Let the assistant look things up with tools
---

Let the assistant look things up itself. Expose read-only tools from the existing services using `Microsoft.Maui.AI.Attributes`: search sessions, get a session, list sessions in a time range, get a speaker, my favourites, the venue, and the sponsors. All for the selected city.

- On Apple, tools run inside the native client, so pass the service provider with `functionInvocationServices`.
- Where the model can't call tools, RAG keeps doing the work.
- Make sessions in answers tappable.

Docs: [source-generated app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md) and [provider tool support](https://github.com/dotnet/maui-labs/blob/main/docs/ai/local-chat-provider.md).

Use DevFlow to ask "What's Niels' background?", "What's on between 2 and 4?", and "Which sponsors are here?".
