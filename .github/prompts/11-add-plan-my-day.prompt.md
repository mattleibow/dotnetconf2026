---
description: Plan my day with an Agent Framework workflow
---

Add a "Plan my day" feature using an Agent Framework workflow (`Microsoft.Agents.AI.Workflows`) on the on-device model:

1. A preferences agent turns my request ("I'm a mobile dev, into AI, leaving at 3") into structured preferences.
2. Find candidate talks with our search service (plain code).
3. Pick talks that don't clash (plain code).
4. For each picked talk, an agent writes one line on why it fits me. Run these concurrently.
5. Show the plan and ask before favouriting them all.

Docs: [Agent Framework in .NET MAUI](https://learn.microsoft.com/dotnet/maui/ai/agent-framework) and [Agent Framework workflows](https://learn.microsoft.com/agent-framework/).

Use DevFlow to plan a day, approve it, and check the favourites.
