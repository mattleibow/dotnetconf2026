# We Removed the Agents from Our Agentic Workflow, and It Worked Better

How many agents does an agentic application actually need?

Using .NET and Microsoft Agent Framework, we built the same AI assistant four different ways: classifier-based routing, specialist handoffs, manager-led orchestration, and a single agent that loads skills as they are needed. Every version used the same model, instructions, tools, and user requests, allowing us to compare the architectures rather than the quality of their prompts.

The multi-agent approaches looked great on a diagram, but ordinary requests exposed some uncomfortable trade-offs. Routing completed only part of a task while still reporting success. Handoffs either stopped at a safety limit or kept passing control around. The manager-led workflow completed the task, but used roughly 2.4 times the model calls, tokens, and time of the skills-based version.

The lesson was not simply that fewer agents are better. It was that specialization does not always require separate agents. Often, it is more about controlling which instructions and tools enter the model’s context, and when.

In this live coding and demo-driven session, we will explore how these workflows are implemented with .NET, ASP.NET Core, and Microsoft Agent Framework. We will run the same requests through each architecture and compare task completion, model calls, token usage, tool exposure, and coordination overhead.

We will also examine a result that challenged our preferred conclusion: progressive disclosure reduced context size, but did not always reduce total token usage at this scale.

You will leave with practical guidance for choosing an agent architecture, measuring its real behaviour, and deciding when multiple agents are genuinely worth the additional complexity.
