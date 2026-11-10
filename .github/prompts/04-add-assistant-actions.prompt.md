---
description: Let the assistant navigate and change favourites, with approval
---

Let the assistant do things, not just chat: open a session, speaker, sponsor or venue page, and add or remove favourites.

- Expose navigation and favourites from the existing services as tools. Use "set favourite" rather than a toggle, so a retry can't undo it.
- Changing favourites needs my approval: mark those tools `ApprovalRequired` and show an approve/deny card first. Navigation doesn't need approval.
- On Apple, tools run natively and skip the middleware's approval step, so enforce the confirmation in the app's own action code too.
- Denying or cancelling leaves favourites unchanged. Tell the model what actually happened.
- If the current model can't call tools, turn actions off rather than faking them.

Docs: [app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md) and [tool approval](https://learn.microsoft.com/dotnet/ai/ai-extensions).

Use DevFlow to navigate from chat, approve a favourite, deny one, and check favourites survive a restart.
