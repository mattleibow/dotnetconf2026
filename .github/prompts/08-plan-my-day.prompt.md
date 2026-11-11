---
description: Plan my day with an Agent Framework workflow
---

Add a "Plan my day" feature in the Assistant using an Agent Framework workflow (`Microsoft.Agents.AI.Workflows`) on the on-device model:

1. A preferences step turns my request ("I'm a mobile dev, into AI, leaving at 3") into structured preferences. Check times in plain code as a fallback.
2. Find candidate talks for the selected city using our semantic search service (plain code).
3. Pick a conflict-free schedule with no overlapping time slots (plain code).
4. For each picked talk, run an agent step concurrently to write one short line on why it fits me.
5. Present the recommended schedule in chat and ask for approval before adding all of them to my favourites.

Docs: [Agent Framework in .NET MAUI](https://learn.microsoft.com/dotnet/maui/ai/agent-framework) and [Agent Framework workflows](https://learn.microsoft.com/agent-framework/).

Use DevFlow on Mac Catalyst to run "Plan my day", approve the plan, and verify the sessions are favourited.
