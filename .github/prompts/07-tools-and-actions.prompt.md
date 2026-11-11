---
description: Give the assistant tools, retrieval, and approved actions
---

Give the assistant tools and actions using `Microsoft.Maui.AI.Attributes` instead of stuffing every summary into the system prompt:

- Shrink the system instructions back to one compact line per session (time, title, speakers, room) so the model uses tools to look up details.
- Expose read-only tools for the selected city: search sessions (calling our semantic search service), get a session with its full abstract, get a speaker with their bio, list sessions in a time range (24-hour daytime, so "2 to 4" means `14:00`–`16:00`), and get my favourites.
- Expose action tools: open a session, speaker, or the event/venue tab, and separate `add_favorite` and `remove_favorite` tools.
- Changing favourites requires my approval: mark the favourite tools `ApprovalRequired` and show an inline approve/deny card in the chat. On Apple, tools run inside the native client (pass `functionInvocationServices`), so enforce the approval check inside the tool implementation itself.
- Under each assistant reply, show compact tappable session cards only for the sessions actually returned or referenced in that turn.

Docs: [source-generated app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md) and [provider tool support](https://github.com/dotnet/maui-labs/blob/main/docs/ai/local-chat-provider.md).

Use DevFlow on Mac Catalyst to ask "What's Niels' background?", then "Find me a talk about football and favourite it", approve the action, and verify it appears when filtering Agenda by Favourites.
