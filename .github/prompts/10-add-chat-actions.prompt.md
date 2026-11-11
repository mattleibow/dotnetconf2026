---
description: Let the assistant navigate and change favourites, with approval
---

Let the assistant do things, not just answer: open a session, speaker, sponsor or venue page, and add or remove favourites.

- Add these as tools next to the read-only ones. Use separate "add favourite" and "remove favourite" tools rather than a toggle or a true/false flag, so a retry can't undo it and the small model picks the right one.
- Changing favourites needs my approval: mark those tools `ApprovalRequired` and show an approve/deny card first. Navigation doesn't need approval.
- On Apple, tools run natively and skip the middleware's approval step, so enforce the confirmation in the app's own action code too.
- Denying or cancelling leaves favourites unchanged. Tell the model what actually happened.

Use DevFlow to navigate from chat, approve a favourite, deny one, and check favourites survive a restart.
