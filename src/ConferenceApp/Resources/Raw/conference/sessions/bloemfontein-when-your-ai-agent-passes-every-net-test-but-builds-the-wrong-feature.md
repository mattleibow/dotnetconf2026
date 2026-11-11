# When Your AI Agent Passes Every .NET Test but Builds the Wrong Feature

The build is green. Every xUnit test passes. Your coding agent says the feature is ready. So why is the ASP.NET Core application still doing the wrong thing?

The problem isn't always bad code. Sometimes the agent misunderstands a requirement, writes perfectly valid C# around that misunderstanding, and then generates tests that confirm its own assumption.

I'll demonstrate this using an ASP.NET Core order-processing feature. We'll follow a coding agent as it interprets an incomplete business rule, implements it, and builds a reassuring test suite that misses the important edge case.

Then we'll challenge the result. Drawing on Finding Unknowns, an open-source collection of verification skills I've developed, I'll show how to uncover the assumption, identify what the tests never checked, and turn that gap into a reproducible xUnit regression test. We'll also look at how ASP.NET Core integration tests can verify behavior across application boundaries.

This isn't about writing longer prompts or asking AI to double-check itself. It's about making independent verification part of the .NET development workflow.

You'll leave with a practical way to review AI-generated C# code, design stronger tests, and recognize when a passing build is evidence of correctness and when it isn't.
