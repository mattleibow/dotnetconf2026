# Your .NET Agent Found the Evidence. Should It Answer?

A customer asks whether a refund can be approved. Your AI agent calls the right tools, finds the purchase, confirms the payment, and confidently says yes. There's just one problem: it never checked the return policy.

The information it found was relevant, but it wasn't enough to justify the answer.

This is an easy mistake to miss when building tool-using AI applications. We test whether an agent calls the right tools and returns the expected answer, but rarely ask whether the evidence actually supports its conclusion.

In this session, I'll show how to evaluate that distinction in a small C# agent built with Microsoft Agent Framework. Using controlled tool responses, we'll compare what happens when the agent receives complete evidence, partial evidence, and information that looks useful but doesn't answer the question.

We'll then turn those scenarios into repeatable tests that check not just the final response, but the evidence behind it and whether the agent knows when to say, "I don't have enough information."

The approach comes from my work on evaluating evidence-seeking AI agents. Attendees will leave with a practical pattern for testing tool-using .NET agents before trusting their decisions in real applications.
