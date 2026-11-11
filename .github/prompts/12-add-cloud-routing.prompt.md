---
description: Send complex questions to a cloud model
---

Keep simple questions on the device, but send harder ones, like "plan my day around AI agents and data talks with no clashes", to a cloud model.

Use `SemanticRoutingChatClient` from `Microsoft.Extensions.AI` with the embedding generator we already have. It's experimental, so check the docs first. Ask me which cloud service and model to use.

- Never commit or bundle an API key. For the demo, enter a short-lived key at runtime; without one, stay local.
- Cloud is opt-in, and show which model answered.
- Keep the same tools and approval rules on both routes, including the Apple setup.
- No automatic retries for actions that change state.

Docs: [Routing and failover for Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/routing-and-failover-for-microsoft-extensions-ai/).

Use DevFlow to try a simple question that stays local, a complex one that goes to the cloud, and an approved action through the cloud.
